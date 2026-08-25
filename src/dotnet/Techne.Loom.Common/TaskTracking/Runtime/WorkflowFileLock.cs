using System.Runtime.InteropServices;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowFileLock
{
    private const int UnixLockExclusive = 2;
    private const int UnixLockNonBlocking = 4;
    private const int UnixLockUnlock = 8;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    public static async Task<IAsyncDisposable> AcquireAsync(string workflowFile, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflowFile))
        {
            throw new ArgumentException("A workflow file is required.", nameof(workflowFile));
        }

        var lockFile = Path.GetFullPath(workflowFile) + ".lock";
        var directory = Path.GetDirectoryName(lockFile);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var stream = new FileStream(
                    lockFile,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 1,
                    options: FileOptions.Asynchronous);
                try
                {
                    AcquirePlatformLock(stream);
                    return new HeldWorkflowFileLock(stream);
                }
                catch
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
            catch (IOException)
            {
                await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
            }
        }
    }

    private static void AcquirePlatformLock(FileStream stream)
    {
        if (OperatingSystem.IsWindows())
        {
            stream.Lock(0, 1);
            return;
        }

        var result = flock(stream.SafeFileHandle.DangerousGetHandle(), UnixLockExclusive | UnixLockNonBlocking);
        if (result == 0)
        {
            return;
        }

        var error = Marshal.GetLastPInvokeError();
        throw new IOException($"Workflow lock is currently held by another process (errno {error}).", error);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int flock(nint fileDescriptor, int operation);

    private sealed class HeldWorkflowFileLock : IAsyncDisposable
    {
        private readonly FileStream _stream;
        private readonly bool _isUnix;

        public HeldWorkflowFileLock(FileStream stream)
        {
            _stream = stream;
            _isUnix = !OperatingSystem.IsWindows();
        }

        public async ValueTask DisposeAsync()
        {
            if (_isUnix)
            {
                flock(_stream.SafeFileHandle.DangerousGetHandle(), UnixLockUnlock);
            }

            await _stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
