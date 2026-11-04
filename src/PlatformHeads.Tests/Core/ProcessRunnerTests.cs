public class ProcessRunnerTests
{
    /// <summary>
    /// Each program writes its input back and exits only once the input has been closed, so one
    /// that was never closed would run out of time instead. Not Windows' sort.exe, which reads
    /// lines ended by a line feed alone as two question marks.
    /// </summary>
    [Test]
    public async Task TheInputIsWrittenAndClosed()
    {
        var (code, output) = OperatingSystem.IsWindows()
            ? ProcessRunner.Run("findstr", ["."], input: "first\nsecond\n")
            : ProcessRunner.Run("cat", [], input: "first\nsecond\n");

        await Assert.That(code).IsEqualTo(0);
        await Assert.That(output.ReplaceLineEndings("\n")).IsEqualTo("first\nsecond\n");
    }

    [Test]
    public async Task AProgramThatIsNotThereIsAnAnswerNotAThrow()
    {
        var (code, output) = ProcessRunner.Run($"not-a-program-{Guid.NewGuid():N}", []);

        await Assert.That(code).IsEqualTo(-1);
        await Assert.That(output).IsNotEmpty();
    }

    [Test]
    public async Task AProgramThatRunsOverIsEnded()
    {
        var (code, output) = OperatingSystem.IsWindows()
            ? ProcessRunner.Run("ping", ["-n", "30", "127.0.0.1"], timeout: TimeSpan.FromMilliseconds(200))
            : ProcessRunner.Run("sleep", ["30"], timeout: TimeSpan.FromMilliseconds(200));

        await Assert.That(code).IsEqualTo(-1);
        await Assert.That(output).EndsWith("timed out");
    }

    /// <summary>
    /// By a program every machine of each kind has on its PATH, rather than by a PATH of the test's
    /// own: the variable is the process's, and the tests beside this one start programs.
    /// </summary>
    [Test]
    public async Task AProgramOnThePathIsFound()
    {
        var name = OperatingSystem.IsWindows() ? "cmd.exe" : "sh";

        var found = ProcessRunner.OnPath(name);

        await Assert.That(found).IsNotNull();
        await Assert.That(Path.GetFileName(found!)).IsEqualTo(name).IgnoringCase();
    }

    [Test]
    public async Task OneThatIsNotThereIsNull() =>
        await Assert.That(ProcessRunner.OnPath($"not-a-program-{Guid.NewGuid():N}")).IsNull();
}
