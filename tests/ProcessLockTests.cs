using AdoClaudeLoop.Locking;

namespace AdoClaudeLoop.Tests;

/// <summary>
/// Exercises the exclusive filesystem lock used to stop two concurrent AdoClaudeLoop
/// instances from running a cycle at the same time.
/// </summary>
public class ProcessLockTests
{
    private static string TempLockPath() =>
        Path.Combine(Path.GetTempPath(), $"adoclaudeloop-test-{Guid.NewGuid():N}.lock");

    [Fact]
    public void TryAcquire_WhenUnheld_Succeeds()
    {
        var path = TempLockPath();

        var acquired = ProcessLock.TryAcquire(path, out var processLock);

        Assert.True(acquired);
        Assert.NotNull(processLock);
        Assert.True(File.Exists(path));

        processLock!.Dispose();
    }

    [Fact]
    public void TryAcquire_WhenAlreadyHeld_FailsWithoutThrowing()
    {
        var path = TempLockPath();
        Assert.True(ProcessLock.TryAcquire(path, out var first));

        var acquired = ProcessLock.TryAcquire(path, out var second);

        Assert.False(acquired);
        Assert.Null(second);

        first!.Dispose();
    }

    [Fact]
    public void Dispose_ReleasesAndDeletesLockFile_AllowingReacquisition()
    {
        var path = TempLockPath();
        Assert.True(ProcessLock.TryAcquire(path, out var first));

        first!.Dispose();

        Assert.False(File.Exists(path));
        Assert.True(ProcessLock.TryAcquire(path, out var second));

        second!.Dispose();
    }

    [Fact]
    public void TryAcquire_CreatesMissingParentDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"adoclaudeloop-test-dir-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "adoclaudeloop.lock");

        try
        {
            var acquired = ProcessLock.TryAcquire(path, out var processLock);

            Assert.True(acquired);
            Assert.True(Directory.Exists(directory));

            processLock!.Dispose();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
