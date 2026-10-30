/// <summary>
/// Runs the workflow's two check steps, as the workflow file has them, against what objdump
/// would say of a library. They are the only thing between a library that will not load on
/// somebody's machine and a pull request proposing it, and nothing else runs them before then.
/// </summary>
public class CheckStepTests
{
    const string containerBuilt = "libpthread.so.0 libdl.so.2 libm.so.6 libX11.so.6 libstdc++.so.6 libgcc_s.so.1 libc.so.6";

    [Test]
    // As a head's library was when built on the runner's own Ubuntu 24.04.
    [Arguments("GLIBC_2.2.5 GLIBC_2.17 GLIBC_2.34 GLIBC_2.38 GLIBCXX_3.4.29", containerBuilt, 1)]
    // As it is built in the container.
    [Arguments("GLIBC_2.2.5 GLIBC_2.3.4 GLIBC_2.17 GLIBC_2.27 GLIBCXX_3.4.21", containerBuilt, 0)]
    [Arguments("GLIBC_2.2.5 GLIBC_2.28", containerBuilt, 0)]
    [Arguments("GLIBC_2.2.5 GLIBC_2.29", containerBuilt, 1)]
    // Versions are not decimals: 2.4 is older than 2.28.
    [Arguments("GLIBC_2.2.5 GLIBC_2.4", containerBuilt, 0)]
    [Arguments("GLIBC_2.2.5 GLIBC_2.27", containerBuilt + " libOpenGL.so.0", 1)]
    public async Task GlibcFloor(string versions, string needed, int expected)
    {
        var result = await RunStep("- name: Check glibc floor", versions, needed);

        await Assert.That(result.ExitCode).IsEqualTo(expected).Because(result.Output);
    }

    [Test]
    [Arguments(containerBuilt, 0)]
    [Arguments(containerBuilt + " ld-linux-aarch64.so.1", 0)]
    [Arguments(containerBuilt + " libGL.so.1", 0)]
    // What the container's linker records without --as-needed.
    [Arguments(containerBuilt + " libSM.so.6 libICE.so.6", 1)]
    // A name that starts with an allowed one is not it.
    [Arguments(containerBuilt + " libX11-xcb.so.1", 1)]
    public async Task NeededLibraries(string needed, int expected)
    {
        var result = await RunStep("- name: Check needed libraries", "GLIBC_2.27", needed);

        await Assert.That(result.ExitCode).IsEqualTo(expected).Because(result.Output);
    }

    static Task<RunResult> RunStep(string step, string versions, string needed)
    {
        var bash = Bash.Find();
        if (bash is null)
        {
            Skip.Test("No bash to run the step with.");
        }

        // objdump -T lists each dynamic symbol with its version in brackets, and objdump -p
        // lists what is needed.
        var symbols = string.Join("\\n", versions.Split(' ').Select(_ => $"0000000000000000      DF *UND*  0000000000000000 ({_}) symbol"));
        var dynamic = string.Join("\\n", needed.Split(' ').Select(_ => $"  NEEDED               {_}"));
        var allowed = string.Join(' ', WorkflowFile.Block("allowed-needed:", "default: >-").Select(_ => _.Trim()));
        var script = new StringBuilder();
        script.Append("objdump() {\n");
        script.Append("  if [ \"$1\" = \"-T\" ]; then\n");
        script.Append($"    printf '{symbols}\\n'\n");
        script.Append("  else\n");
        script.Append($"    printf 'Dynamic Section:\\n{dynamic}\\n  SONAME               libfixture.so\\n'\n");
        script.Append("  fi\n");
        script.Append("}\n");
        script.Append("export LIBRARY=fixture\n");
        script.Append("export GLIBC_FLOOR=GLIBC_2.28\n");
        script.Append($"export ALLOWED='{allowed}'\n");
        script.Append(string.Join('\n', WorkflowFile.Block(step, "run: |")));
        script.Append('\n');

        return Bash.Run(bash, script.ToString(), Path.GetTempPath());
    }
}
