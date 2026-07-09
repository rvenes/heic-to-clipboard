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

    private readonly object _gate = new();
    private readonly HashSet<string> _seenFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _pendingFiles = [];
    private DateTime _lastUpdateUtc;

    private readonly ManualResetEventSlim _stopRequested = new(false);
    private Thread? _serverThread;
    private Mutex? _ownedMutex;
    private CoordinatorRole? _role;
    private bool _disposed;

    public InvocationCoordinator(
        string mutexName,
        string pipeName,
        TimeSpan idleDelay,
        TimeSpan maxInitialWait,
        TimeSpan standaloneFallbackBudget)
    {
        _mutexName = mutexName;
        _pipeName = pipeName;
        _idleDelay = idleDelay;
        _maxInitialWait = maxInitialWait;
        _standaloneFallbackBudget = standaloneFallbackBudget;
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

        var deadlineUtc = DateTime.UtcNow + _standaloneFallbackBudget;
        while (true)
        {
            if (TryBecomePrimary())
            {
                Add(files);
                StartServer();
                _role = CoordinatorRole.Primary;
                return CoordinatorRole.Primary;
            }

            if (TryForward(files))
            {
                _role = CoordinatorRole.Forwarded;
                return CoordinatorRole.Forwarded;
            }

            if (DateTime.UtcNow >= deadlineUtc)
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

        if (_serverThread is null)
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

    private bool TryForward(IReadOnlyList<string> files)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            client.Connect(ForwardConnectTimeoutMilliseconds);

            var payload = JsonSerializer.SerializeToUtf8Bytes(files);
            var lengthBuffer = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, payload.Length);
            client.Write(lengthBuffer);
            client.Write(payload);
            client.Flush();

            return client.ReadByte() == AckByte;
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
        _serverThread = new Thread(ServerLoop)
        {
            IsBackground = true,
            Name = "HeicToClipboard.InvocationCollector"
        };
        _serverThread.Start();
    }

    private void StopServer()
    {
        var serverThread = _serverThread;
        if (serverThread is null)
        {
            return;
        }

        _serverThread = null;
        _stopRequested.Set();

        for (var attempt = 0; attempt < 5 && serverThread.IsAlive; attempt++)
        {
            try
            {
                using var poke = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
                poke.Connect(ForwardConnectTimeoutMilliseconds);
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }

            serverThread.Join(TimeSpan.FromMilliseconds(500));
        }
    }

    private void ServerLoop()
    {
        while (!_stopRequested.IsSet)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte);
                server.WaitForConnection();

                if (_stopRequested.IsSet)
                {
                    // No ack is sent, so a real sender caught in shutdown will retry
                    // and take over as primary once the mutex is released.
                    return;
                }

                var lengthBuffer = new byte[4];
                server.ReadExactly(lengthBuffer);
                var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
                if (payloadLength is <= 0 or > MaxPayloadBytes)
                {
                    continue;
                }

                var payload = new byte[payloadLength];
                server.ReadExactly(payload);
                var forwardedFiles = JsonSerializer.Deserialize<string[]>(payload) ?? [];

                // Record before acknowledging: an ack must guarantee inclusion.
                Add(forwardedFiles);

                server.WriteByte(AckByte);
                server.Flush();
                server.WaitForPipeDrain();
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
                Thread.Sleep(PollDelayMilliseconds);
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
