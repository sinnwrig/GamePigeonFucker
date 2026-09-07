using System.Diagnostics;

namespace IMessage;

public sealed class SshInjectorTunnel : IDisposable
{
    private readonly Process _process;

    public string LocalSocketPath { get; }

    private SshInjectorTunnel(Process process, string localSocketPath)
    {
        _process = process;
        LocalSocketPath = localSocketPath;
    }

    public static async Task<SshInjectorTunnel> StartAsync(
        string sshHost,
        string? localSocketPath = null,
        string remoteSocketPath = "/tmp/gamepigeonfucker-injector.sock",
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var localPath = localSocketPath ?? Path.Combine(Path.GetTempPath(), $"gpf-injector-{Guid.NewGuid():N}.sock");
        File.Delete(localPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "ssh",
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-N");
        startInfo.ArgumentList.Add("-L");
        startInfo.ArgumentList.Add($"{localPath}:{remoteSocketPath}");
        startInfo.ArgumentList.Add(sshHost);

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("failed to start ssh");

        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (!File.Exists(localPath))
        {
            if (process.HasExited)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException($"ssh tunnel exited early: {stderr}");
            }

            if (DateTime.UtcNow > deadline)
            {
                process.Kill();
                throw new TimeoutException($"timed out waiting for {localPath} to appear");
            }

            await Task.Delay(200, cancellationToken);
        }

        return new SshInjectorTunnel(process, localPath);
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill();
            _process.WaitForExit();
        }

        _process.Dispose();

        try
        {
            File.Delete(LocalSocketPath);
        }
        catch (IOException)
        {
        }
    }
}
