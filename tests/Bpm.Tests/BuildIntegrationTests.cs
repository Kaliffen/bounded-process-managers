using System.Diagnostics;

namespace Bpm.Tests;

/// <summary>
/// The "compile-time" in compile-time bounded: an invalid workflow must fail `dotnet build` with the
/// BPM diagnostics, pointing at the source line, even though it is valid C#.
/// </summary>
public class BuildIntegrationTests
{
    [Fact]
    [Trait("Category", "Slow")]
    public void Invalid_definitions_fail_the_build()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "BoundedPM.slnx"))) root = Path.GetDirectoryName(root)!;
        var project = Path.Combine(root, "tests", "Fixtures", "BrokenDefinition", "BrokenDefinition.csproj");

        var psi = new ProcessStartInfo("dotnet", $"build \"{project}\" -nologo -v:q")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var proc = Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        Assert.NotEqual(0, proc.ExitCode);
        Assert.Matches(@"Broken\.cs\(\d+\): error BPM007: worst-case waiting bound T_DAG = 12m exceeds the declared budget 10m", output);
        Assert.Contains("error BPM001", output);
    }
}
