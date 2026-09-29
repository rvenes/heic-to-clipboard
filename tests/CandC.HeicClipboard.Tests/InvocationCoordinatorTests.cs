using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;

namespace CandC.HeicClipboard.Tests;

public sealed class InvocationCoordinatorTests
{
    private static readonly TimeSpan ShortIdle = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ShortMaxWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ShortFallback = TimeSpan.FromSeconds(5);

    private static (string MutexName, string PipeName) CreateUniqueNames()
    {
        var token = Guid.NewGuid().ToString("N");
        return ($@"Local\HeicToClipboardTest_{token}", $"HeicToClipboardTest_{token}");
    }

    private static InvocationCoordinator CreateCoordinator(
        (string MutexName, string PipeName) names,
        TimeSpan? idleDelay = null,
        TimeSpan? fallback = null)
    {
        return new InvocationCoordinator(
            names.MutexName,
            names.PipeName,
            idleDelay ?? ShortIdle,
            ShortMaxWait,
            fallback ?? ShortFallback);
    }

    // Mutex ownership is reentrant per thread, so a simulated secondary must run on
    // its own thread (like the separate process it stands in for) or it would just
    // re-acquire the primary's mutex.
    private static CoordinatorRole RunSecondary((string MutexName, string PipeName) names, params string[] files)
    {
        var task = Task.Run(() =>
        {
            using var secondary = CreateCoordinator(names);
            return secondary.CollectOrForward(files);
        });

        Assert.True(task.Wait(TimeSpan.FromSeconds(15)), "Secondary instance timed out.");
        return task.Result;
    }

    [Fact]
    public void RapidMultiselect_AllFilesEndUpInFirstBatch()
    {
        var names = CreateUniqueNames();
        using var primary = CreateCoordinator(names);

        Assert.Equal(CoordinatorRole.Primary, primary.CollectOrForward([@"C:\Images\a.heic"]));

        var secondaryRoles = new[] { @"C:\Images\b.heic", @"C:\Images\c.heic", @"C:\Images\d.heic" }
            .Select(file => Task.Run(() =>
            {
                using var secondary = CreateCoordinator(names);
                return secondary.CollectOrForward([file]);
            }))
            .ToArray();

        Task.WaitAll(secondaryRoles, TimeSpan.FromSeconds(10));
        Assert.All(secondaryRoles, task => Assert.Equal(CoordinatorRole.Forwarded, task.Result));

        var batch = primary.WaitForFirstBatch();

        Assert.Equal(
            new[] { @"C:\Images\a.heic", @"C:\Images\b.heic", @"C:\Images\c.heic", @"C:\Images\d.heic" },
            batch);
    }

    [Fact]
    public void StragglerDuringConversion_JoinsSameBatchInsteadOfBecomingPrimary()
    {
        // Regression for the split-batch bug: a second invocation arriving well after
        // the collection window closed (originally ~1.6s later, while the primary was
        // converting) became a new primary and overwrote the clipboard with only its
        // own file. It must now forward to the still-alive primary instead.
        var names = CreateUniqueNames();
        using var primary = CreateCoordinator(names);

        Assert.Equal(CoordinatorRole.Primary, primary.CollectOrForward([@"C:\Images\first.heic"]));
        var firstBatch = primary.WaitForFirstBatch();
        Assert.Equal([@"C:\Images\first.heic"], firstBatch);

        // The collection window is long over; the primary is "converting" now.
        Thread.Sleep(1600);

        var stragglerRole = RunSecondary(names, @"C:\Images\late.heic");

        Assert.Equal(CoordinatorRole.Forwarded, stragglerRole);
        Assert.Equal([@"C:\Images\late.heic"], primary.WaitForAdditionalFiles());
        Assert.Empty(primary.WaitForAdditionalFiles());
    }

    [Fact]
    public void DelayedSecondaries_ArePickedUpAcrossMultipleRounds()
    {
        var names = CreateUniqueNames();
        using var primary = CreateCoordinator(names);

        primary.CollectOrForward([@"C:\Images\round1.heic"]);
        Assert.Single(primary.WaitForFirstBatch());

        Assert.Equal(CoordinatorRole.Forwarded, RunSecondary(names, @"C:\Images\round2.heic"));

        Assert.Equal([@"C:\Images\round2.heic"], primary.WaitForAdditionalFiles());

        Assert.Equal(CoordinatorRole.Forwarded, RunSecondary(names, @"C:\Images\round3.heic"));

        Assert.Equal([@"C:\Images\round3.heic"], primary.WaitForAdditionalFiles());
        Assert.Empty(primary.WaitForAdditionalFiles());
    }

    [Fact]
    public void LateInvocationAfterPrimaryCompleted_BecomesNewPrimary()
    {
        var names = CreateUniqueNames();

        using (var primary = CreateCoordinator(names))
        {
            primary.CollectOrForward([@"C:\Images\early.heic"]);
            primary.WaitForFirstBatch();
            while (primary.WaitForAdditionalFiles().Count > 0)
            {
            }
        }

        using var late = CreateCoordinator(names);
        var role = late.CollectOrForward([@"C:\Images\late.heic"]);

        Assert.Equal(CoordinatorRole.Primary, role);
        Assert.Equal([@"C:\Images\late.heic"], late.WaitForFirstBatch());
    }

    [Fact]
    public void ForwardedPaths_SurviveSpacesAndNorwegianCharacters()
    {
        var names = CreateUniqueNames();
        using var primary = CreateCoordinator(names);
        primary.CollectOrForward([@"C:\Bilete\vanleg.heic"]);

        var unicodePath = @"C:\tæst møppe æøå\bilete ø nr 1 – «spesial».heic";
        Assert.Equal(CoordinatorRole.Forwarded, RunSecondary(names, unicodePath));

        var batch = primary.WaitForFirstBatch();

        Assert.Contains(unicodePath, batch);
    }

    [Fact]
    public void DuplicateFiles_AreOnlyBatchedOnce()
    {
        var names = CreateUniqueNames();
        using var primary = CreateCoordinator(names);
        primary.CollectOrForward([@"C:\Images\same.heic"]);

        Assert.Equal(CoordinatorRole.Forwarded, RunSecondary(names, @"C:\Images\SAME.heic"));

        Assert.Single(primary.WaitForFirstBatch());
    }

    [Fact]
    public void FileAlreadyProcessed_IsNotReturnedAgainByLaterRounds()
    {
        var names = CreateUniqueNames();
        using var primary = CreateCoordinator(names);
        primary.CollectOrForward([@"C:\Images\once.heic"]);
        Assert.Single(primary.WaitForFirstBatch());

        // The sender gets an ack (the file is already covered by round one),
        // so it exits without spawning a duplicate conversion.
        Assert.Equal(CoordinatorRole.Forwarded, RunSecondary(names, @"C:\Images\once.heic"));

        Assert.Empty(primary.WaitForAdditionalFiles());
    }

    [Fact]
    public void MutexHeldElsewhereWithoutServer_FallsBackToStandalone()
    {
        var names = CreateUniqueNames();
        using var mutexAcquired = new ManualResetEventSlim(false);
        using var releaseRequested = new ManualResetEventSlim(false);

        var holder = new Thread(() =>
        {
            using var mutex = new Mutex(initiallyOwned: true, names.MutexName, out _);
            mutexAcquired.Set();
            releaseRequested.Wait();
            mutex.ReleaseMutex();
        });
        holder.Start();
        mutexAcquired.Wait();

        try
        {
            using var coordinator = CreateCoordinator(names, fallback: TimeSpan.FromMilliseconds(600));
            var role = coordinator.CollectOrForward([@"C:\Images\own.heic"]);

            Assert.Equal(CoordinatorRole.Standalone, role);
            Assert.Equal([@"C:\Images\own.heic"], coordinator.WaitForFirstBatch());
            Assert.Empty(coordinator.WaitForAdditionalFiles());
        }
        finally
        {
            releaseRequested.Set();
            holder.Join();
        }
    }

    [Fact]
    public void SessionPipeName_IsScopedToTheCurrentSession()
    {
        var sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;

        Assert.Equal($"{AppConstants.PipeName}_{sessionId}", InvocationCoordinator.SessionPipeName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20000)]
    public async Task ConnectedPrimaryThatNeverReadsOrAcknowledges_FallsBackWithinBudget(int fileCount)
    {
        var names = CreateUniqueNames();
        using var ready = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var holder = new Thread(() =>
        {
            using var mutex = new Mutex(true, names.MutexName);
            try
            {
                using var pipe = new NamedPipeServerStream(names.PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                ready.Set();
                pipe.WaitForConnectionAsync(stop.Token).GetAwaiter().GetResult();
                release.Wait(stop.Token);
            }
            catch (OperationCanceledException) { }
            finally { mutex.ReleaseMutex(); }
        }) { IsBackground = true };
        holder.Start();

        try
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(3)));
            var role = await Task.Run(() =>
            {
                using var secondary = CreateCoordinator(names, fallback: TimeSpan.FromMilliseconds(600));
                var files = Enumerable.Range(0, fileCount).Select(i => $@"C:\Images\sample-{i}.heic").ToArray();
                var result = secondary.CollectOrForward(files);
                Assert.Equal(fileCount, secondary.WaitForFirstBatch().Count);
                return result;
            }).WaitAsync(TimeSpan.FromSeconds(4));

            Assert.Equal(CoordinatorRole.Standalone, role);
        }
        finally
        {
            release.Set();
            stop.Cancel();
            Assert.True(holder.Join(TimeSpan.FromSeconds(3)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StalledSender_DoesNotBlockFollowingFiles(bool partialPayload)
    {
        var names = CreateUniqueNames();
        using var primary = new InvocationCoordinator(names.MutexName, names.PipeName,
            ShortIdle, ShortMaxWait, ShortFallback, TimeSpan.FromMilliseconds(250));
        primary.CollectOrForward([@"C:\Images\first.heic"]);
        using var stalled = new NamedPipeClientStream(".", names.PipeName, PipeDirection.InOut);
        stalled.Connect(3000);
        if (partialPayload)
        {
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, 100);
            stalled.Write(header);
            stalled.WriteByte((byte)'[');
        }
        else
        {
            stalled.WriteByte(100); // incomplete length header
        }

        Assert.Equal(CoordinatorRole.Forwarded, RunSecondary(names, @"C:\Images\next.heic"));
        Assert.Equal(new[] { @"C:\Images\first.heic", @"C:\Images\next.heic" }, primary.WaitForFirstBatch());
    }

    [Fact]
    public void Shutdown_CancelsConnectedSenderAndAllowsImmediateNewPrimary()
    {
        var names = CreateUniqueNames();
        using var stalled = new NamedPipeClientStream(".", names.PipeName, PipeDirection.InOut);
        using (var primary = new InvocationCoordinator(names.MutexName, names.PipeName,
            ShortIdle, ShortMaxWait, ShortFallback, TimeSpan.FromSeconds(30)))
        {
            primary.CollectOrForward([@"C:\Images\first.heic"]);
            stalled.Connect(3000);
            stalled.WriteByte(100);
            var elapsed = Stopwatch.StartNew();

            primary.Dispose();

            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2), "Shutdown waited for the stalled sender.");
        }

        using var next = CreateCoordinator(names);
        Assert.Equal(CoordinatorRole.Primary, next.CollectOrForward([@"C:\Images\next.heic"]));
        Assert.Equal([@"C:\Images\next.heic"], next.WaitForFirstBatch());
    }

    [Fact]
    public void CollectOrForward_ThrowsWhenCalledTwice()
    {
        var names = CreateUniqueNames();
        using var coordinator = CreateCoordinator(names);
        coordinator.CollectOrForward([@"C:\Images\a.heic"]);

        Assert.Throws<InvalidOperationException>(() => coordinator.CollectOrForward([@"C:\Images\b.heic"]));
    }
}
