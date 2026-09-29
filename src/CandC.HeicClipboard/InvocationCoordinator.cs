using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;

namespace CandC.HeicClipboard;

public enum CoordinatorRole
{
    /// <summary>This instance owns the batch: it collects, converts, and updates the clipboard.</summary>
    Primary,

    /// <summary>The files were delivered to a running primary (ack received); this instance should exit.</summary>
    Forwarded,

    /// <summary>No primary could be reached and the mutex could not be acquired in time;
    /// this instance processes its own files without coordination rather than dropping them.</summary>
    Standalone
}

public interface IFileBatchSource
{
    IReadOnlyList<string> WaitForFirstBatch();

    IReadOnlyList<string> WaitForAdditionalFiles();
}

/// <summary>
/// Merges the one-process-per-file invocations Explorer produces into a single batch.
/// The primary instance holds the mutex and keeps the pipe server alive for its entire
/// lifetime, so stragglers that arrive while it is converting join the same batch instead
/// of becoming a competing primary that overwrites the clipboard with a partial set.
/// Delivery over the pipe is only treated as successful once the server has acknowledged
/// that the files were recorded, so files are never silently dropped.
/// </summary>
public sealed class InvocationCoordinator : IFileBatchSource, IDisposable
{
    private const byte AckByte = 0x06;
    private const int MaxPayloadBytes = 4 * 1024 * 1024;
    private const int ForwardConnectTimeoutMilliseconds = 200;
    private const int RetryDelayMilliseconds = 50;
    private const int PollDelayMilliseconds = 50;

    private static readonly string SessionPipeNameValue =
        $"{AppConstants.PipeName}_{Process.GetCurrentProcess().SessionId}";

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly TimeSpan _idleDelay;
    private readonly TimeSpan _maxInitialWait;
    private readonly TimeSpan _standaloneFallbackBudget;
    private readonly TimeSpan _transferTimeout;

    private readonly object _gate = new();
    private readonly HashSet<string> _seenFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _pendingFiles = [];
    private DateTime _lastUpdateUtc;

    private readonly CancellationTokenSource _stopRequested = new();
    private Task? _serverTask;
    private Mutex? _ownedMutex;
    private CoordinatorRole? _role;
    private bool _disposed;

    public InvocationCoordinator(
        string mutexName,
        string pipeName,
        TimeSpan idleDelay,
        TimeSpan maxInitialWait,
        TimeSpan standaloneFallbackBudget,
        TimeSpan? transferTimeout = null)
    {
        _mutexName = mutexName;
        _pipeName = pipeName;
        _idleDelay = idleDelay;
        _maxInitialWait = maxInitialWait;
        _standaloneFallbackBudget = standaloneFallbackBudget;
        _transferTimeout = transferTimeout ?? TimeSpan.FromSeconds(2);
        _lastUpdateUtc = DateTime.UtcNow;
    }

    public static string SessionPipeName => SessionPipeNameValue;

    public static InvocationCoordinator CreateDefault() => new(
        AppConstants.MutexName,
        SessionPipeNameValue,
        AppConstants.BatchIdleDelay,
        AppConstants.BatchMaxWait,
        AppConstants.StandaloneFallbackBudget);

    public CoordinatorRole CollectOrForward(IReadOnlyList<string> files)
    {
        if (_role is not null)
        {
            throw new InvalidOperationException("CollectOrForward can only be called once.");
        }

        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            if (TryBecomePrimary())
            {
                Add(files);
                StartServer();
                _role = CoordinatorRole.Primary;
                return CoordinatorRole.Primary;
            }

            var remaining = _standaloneFallbackBudget - elapsed.Elapsed;
            if (remaining > TimeSpan.Zero && TryForwardAsync(files, remaining).GetAwaiter().GetResult())
            {
                _role = CoordinatorRole.Forwarded;
                return CoordinatorRole.Forwarded;
            }

            if (elapsed.Elapsed >= _standaloneFallbackBudget)
            {
                Add(files);
                _role = CoordinatorRole.Standalone;
                return CoordinatorRole.Standalone;
            }

            Thread.Sleep(RetryDelayMilliseconds);
        }
    }

    public IReadOnlyList<string> WaitForFirstBatch()
    {
        if (_role == CoordinatorRole.Primary)
        {
            WaitUntilIdle(_idleDelay, _maxInitialWait);
        }

        return TakePendingFiles();
    }

    public IReadOnlyList<string> WaitForAdditionalFiles()
    {
        var batch = TakePendingFiles();
        if (batch.Count > 0)
        {
            return batch;
        }

        if (_serverTask is null)
        {
            return Array.Empty<string>();
        }

        // Give in-flight invocations one quiet period to land before shutting down.
        WaitUntilIdle(_idleDelay, _idleDelay);
        batch = TakePendingFiles();
        if (batch.Count > 0)
        {
            return batch;
        }

        // Stop the server first, then drain once more: anything acknowledged during
        // shutdown is picked up here, and anything not acknowledged is still owned
        // by its sender, which will retry and become primary or standalone itself.
        StopServer();
        return TakePendingFiles();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopServer();
        _stopRequested.Dispose();

        if (_ownedMutex is not null)
        {
            try
            {
                _ownedMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _ownedMutex.Dispose();
            _ownedMutex = null;
        }
    }

    private bool TryBecomePrimary()
    {
        var mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        var owned = createdNew;

        if (!owned)
        {
            // The constructor does not grant ownership when the mutex already exists,
            // so try to take it: it may be free or abandoned by a crashed primary.
            try
            {
                owned = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                owned = true;
            }
        }

        if (!owned)
        {
            mutex.Dispose();
            return false;
        }

        _ownedMutex = mutex;
        return true;
    }

    private async Task<bool> TryForwardAsync(IReadOnlyList<string> files, TimeSpan remaining)
    {
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(files);
            if (payload.Length > MaxPayloadBytes)
            {
                return false;
            }

            using var timeout = new CancellationTokenSource(remaining < _transferTimeout ? remaining : _transferTimeout);
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(ForwardConnectTimeoutMilliseconds, timeout.Token).ConfigureAwait(false);

            var lengthBuffer = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, payload.Length);
            await client.WriteAsync(lengthBuffer, timeout.Token).ConfigureAwait(false);
            await client.WriteAsync(payload, timeout.Token).ConfigureAwait(false);

            var ack = new byte[1];
            await client.ReadExactlyAsync(ack, timeout.Token).ConfigureAwait(false);
            return ack[0] == AckByte;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void StartServer()
    {
        _serverTask = ServerLoopAsync();
    }

    private void StopServer()
    {
        var serverTask = _serverTask;
        if (serverTask is null)
        {
            return;
        }

        // Cancellation interrupts both an idle listener and a connected peer that
        // stopped sending. Join before draining pending files or releasing ownership.
        _stopRequested.Cancel();
        serverTask.GetAwaiter().GetResult();
        _serverTask = null;
    }

    private async Task ServerLoopAsync()
    {
        while (!_stopRequested.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_stopRequested.Token).ConfigureAwait(false);

                if (_stopRequested.IsCancellationRequested)
                {
                    // No ack is sent, so a real sender caught in shutdown will retry
                    // and take over as primary once the mutex is released.
                    return;
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopRequested.Token);
                timeout.CancelAfter(_transferTimeout);
                var lengthBuffer = new byte[4];
                await server.ReadExactlyAsync(lengthBuffer, timeout.Token).ConfigureAwait(false);
                var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
                if (payloadLength is <= 0 or > MaxPayloadBytes)
                {
                    continue;
                }

                var payload = new byte[payloadLength];
                await server.ReadExactlyAsync(payload, timeout.Token).ConfigureAwait(false);
                var forwardedFiles = JsonSerializer.Deserialize<string[]>(payload) ?? [];

                // Record before acknowledging: an ack must guarantee inclusion.
                Add(forwardedFiles);

                await server.WriteAsync(new byte[] { AckByte }, timeout.Token).ConfigureAwait(false);
                // Let the client read the ack and close before closing our end.
                // Unlike WaitForPipeDrain this wait is cancellable and bounded.
                await server.ReadAsync(new byte[1], timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // A stalled peer times out; shutdown cancellation ends the loop.
            }
            catch (EndOfStreamException)
            {
            }
            catch (JsonException)
            {
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (IOException)
            {
                // Includes pipe-name-busy; back off briefly instead of spinning.
                await Task.Delay(PollDelayMilliseconds).ConfigureAwait(false);
            }
        }
    }

    private void Add(IEnumerable<string> files)
    {
        lock (_gate)
        {
            foreach (var file in files)
            {
                if (_seenFiles.Add(file))
                {
                    _pendingFiles.Add(file);
                }
            }

            _lastUpdateUtc = DateTime.UtcNow;
        }
    }

    private IReadOnlyList<string> TakePendingFiles()
    {
        lock (_gate)
        {
            if (_pendingFiles.Count == 0)
            {
                return Array.Empty<string>();
            }

            var batch = _pendingFiles.Order(StringComparer.OrdinalIgnoreCase).ToArray();
            _pendingFiles.Clear();
            return batch;
        }
    }

    private void WaitUntilIdle(TimeSpan idleDelay, TimeSpan maxWait)
    {
        var startedAtUtc = DateTime.UtcNow;
        while (DateTime.UtcNow - startedAtUtc < maxWait)
        {
            DateTime lastUpdateUtc;
            lock (_gate)
            {
                lastUpdateUtc = _lastUpdateUtc;
            }

            if (DateTime.UtcNow - lastUpdateUtc >= idleDelay)
            {
                return;
            }

            Thread.Sleep(PollDelayMilliseconds);
        }
    }
}
