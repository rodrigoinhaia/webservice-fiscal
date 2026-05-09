using System.Diagnostics;

namespace FiscalService.Api.IntegrationTests;

/// <summary>Verifica se o CLI Docker responde (necessário para Testcontainers).</summary>
internal static class DockerHostGuard
{
    public static bool IsDockerReachable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null)
                return false;
            p.WaitForExit(15_000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
