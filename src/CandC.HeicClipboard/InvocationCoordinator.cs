using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace CandC.HeicClipboard;

public static class InvocationCoordinator
{
    public static InvocationBatch Collect(string[] args)
    {
        var normalizedFiles = FileSelectionNormalizer.Normalize(args);
        if (normalizedFiles.Count == 0)
        {
            return InvocationBatch.Exit();
        }

        var mutex = new Mutex(true, AppConstants.MutexName, out var isPrimaryInstance);
        if (!isPrimaryInstance)
        {
            try
            {
                ForwardToPrimary(normalizedFiles);
            }
            finally
            {
                mutex.Dispose();
            }

            return InvocationBatch.Exit();
        }

        using (mutex)
        {
            using var collector = new SecondaryInvocationCollector();
            collector.Add(normalizedFiles);
            collector.Start();
            collector.WaitForIdle(AppConstants.BatchIdleDelay, AppConstants.BatchMaxWait);

            return InvocationBatch.Process(collector.GetFiles());
        }
    }

    private static void ForwardToPrimary(IReadOnlyList<string> normalizedFiles)
    {
        var payload = JsonSerializer.Serialize(normalizedFiles);

        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", AppConstants.PipeName, PipeDirection.Out);
                client.Connect(100);

                using var writer = new StreamWriter(client, Encoding.UTF8, 1024, leaveOpen: false);
                writer.Write(payload);
                writer.Flush();
                return;
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }

            Thread.Sleep(100);
        }
    }

    private sealed class SecondaryInvocationCollector : IDisposable
    {
        private readonly ConcurrentDictionary<string, byte> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly ManualResetEventSlim _stopRequested = new(false);
        private readonly Thread _serverThread;
        private DateTime _lastUpdateUtc;

        public SecondaryInvocationCollector()
        {
            _lastUpdateUtc = DateTime.UtcNow;
            _serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "HeicToClipboard.InvocationCollector"
            };
        }

        public void Start() => _serverThread.Start();

        public void Add(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                _files.TryAdd(file, 0);
            }

            _lastUpdateUtc = DateTime.UtcNow;
        }

        public IReadOnlyList<string> GetFiles() => _files.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();

        public void WaitForIdle(TimeSpan idleDelay, TimeSpan maxWait)
        {
            var startedAtUtc = DateTime.UtcNow;
            while (DateTime.UtcNow - startedAtUtc < maxWait)
            {
                if (DateTime.UtcNow - _lastUpdateUtc >= idleDelay)
                {
                    return;
                }

                Thread.Sleep(100);
            }
        }

        public void Dispose()
        {
            _stopRequested.Set();

            try
            {
                using var client = new NamedPipeClientStream(".", AppConstants.PipeName, PipeDirection.Out);
                client.Connect(100);
            }
            catch
            {
            }

            if (_serverThread.IsAlive)
            {
                _serverThread.Join(TimeSpan.FromSeconds(1));
            }

            _stopRequested.Dispose();
        }

        private void ServerLoop()
        {
            while (!_stopRequested.IsSet)
            {
                try
                {
                    using var server = new NamedPipeServerStream(AppConstants.PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte);
                    server.WaitForConnection();

                    if (_stopRequested.IsSet)
                    {
                        return;
                    }

                    using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: false);
                    var payload = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(payload))
                    {
                        continue;
                    }

                    var forwardedFiles = JsonSerializer.Deserialize<string[]>(payload) ?? [];
                    Add(forwardedFiles);
                }
                catch (IOException)
                {
                }
                catch (JsonException)
                {
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }
    }
}

public sealed record InvocationBatch(bool ShouldExitCurrentInstance, IReadOnlyList<string> Files)
{
    public static InvocationBatch Exit() => new(true, Array.Empty<string>());

    public static InvocationBatch Process(IReadOnlyList<string> files) => new(false, files);
}
