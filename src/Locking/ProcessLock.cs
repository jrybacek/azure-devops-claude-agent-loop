namespace AdoClaudeLoop.Locking;

/// <summary>
/// An exclusive, filesystem-based lock used to prevent two concurrent AdoClaudeLoop
/// instances from running a cycle at the same time (e.g. two overlapping Task Scheduler
/// triggers). The lock is released automatically on process exit, including on an
/// unhandled exception, because the underlying handle is closed by garbage collection /
/// process teardown even if <see cref="Dispose"/> is never called explicitly.
/// </summary>
public sealed class ProcessLock : IDisposable
{
    private readonly FileStream _stream;

    private ProcessLock(FileStream stream)
    {
        _stream = stream;
    }

    /// <summary>
    /// Attempts to acquire the lock at <paramref name="path"/>. Returns <c>false</c> without
    /// throwing if another process already holds it.
    /// </summary>
    public static bool TryAcquire(string path, out ProcessLock? processLock)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            // FileShare.None makes acquisition atomic: a second process opening the same
            // path fails with IOException rather than racing on file contents.
            // DeleteOnClose means a clean release (Dispose, or process exit) also removes
            // the lock file so a stale-but-unheld file never lingers.
            var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);

            processLock = new ProcessLock(stream);
            return true;
        }
        catch (IOException)
        {
            processLock = null;
            return false;
        }
    }

    public void Dispose() => _stream.Dispose();
}
