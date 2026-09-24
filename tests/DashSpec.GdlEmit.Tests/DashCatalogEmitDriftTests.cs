#nullable enable

using System.Diagnostics;
using Xunit;

namespace DashSpec.GdlEmit.Tests;

/// <summary>W0 — CI drift gate: committed <c>Generated/DashCatalog.g.cs</c> must match <c>gdlc emit</c> (ADR-0052).</summary>
public sealed class DashCatalogEmitDriftTests
{
    [Fact]
    public void DashCatalog_g_cs_matches_gdlc_emit_output()
    {
        var repoRoot = ResolveDashSpecRepoRoot();
        var hostProject = Path.Combine(repoRoot, "src", "DashSpec.Host", "DashSpec.Host.csproj");
        var toolchainRoot = Path.GetFullPath(Path.Combine(repoRoot, "..", "authoring-toolchain"));
        var gdlcProject = Path.Combine(toolchainRoot, "src", "Gdlc.Cli", "Gdlc.Cli.csproj");

        Assert.True(File.Exists(hostProject), $"Host project not found: {hostProject}");
        Assert.True(
            File.Exists(Path.Combine(toolchainRoot, "build", "Platform.Gdl.Emit.props")),
            $"authoring-toolchain sibling required for catalog emit gate: {toolchainRoot}");

        RunProcess("dotnet", $"build \"{gdlcProject}\" -c Release", toolchainRoot);
        RunProcess("dotnet", $"msbuild \"{hostProject}\" -p:Configuration=Release -t:GdlEmitVerify", repoRoot);
    }

    private static void RunProcess(string fileName, string arguments, string workingDirectory)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        });

        Assert.NotNull(process);
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"{fileName} {arguments} failed ({process.ExitCode}).\nstdout:\n{stdout}\nstderr:\n{stderr}");
    }

    private static string ResolveDashSpecRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DashSpec.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve dash-spec repo root.");
    }
}
