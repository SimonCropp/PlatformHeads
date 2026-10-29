/// <summary>
/// The report an update leaves for the app it starts again.
/// </summary>
public class ToolUpdateOutcomeTests
{
    /// <summary>
    /// What dotnet tool update wrote when a running process held the old version's files.
    /// </summary>
    const string locked =
        """
        failed
        Tool 'sampleapp' failed to update due to the following:
        Failed to uninstall tool package 'sampleapp': Access to the path 'C:\Users\me\.dotnet\tools\.store\sampleapp\0.1.0' is denied.
        """;

    [Test]
    public async Task AFailureIsTakenOnceAndThenRemoved()
    {
        var path = Path.Combine(Path.GetTempPath(), $"PlatformHeads.Outcome.{Guid.NewGuid():N}.log");
        try
        {
            // As Windows PowerShell's Set-Content -Encoding UTF8 writes it, with a byte order mark
            await File.WriteAllTextAsync(path, locked, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var outcome = ToolUpdateOutcome.Take(path)!;

            await Assert.That(outcome.Succeeded).IsFalse();
            await Assert.That(outcome.Reason).IsEqualTo("""Failed to uninstall tool package 'sampleapp': Access to the path 'C:\Users\me\.dotnet\tools\.store\sampleapp\0.1.0' is denied.""");
            await Assert.That(File.Exists(path)).IsFalse();
            await Assert.That(ToolUpdateOutcome.Take(path)).IsNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ASuccessIsTaken()
    {
        var outcome = ToolUpdateOutcome.Parse("ok\nTool 'sampleapp' was successfully updated from version '0.1.0' to version '0.1.1'.\n");

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(outcome.Output).Contains("successfully updated");
    }

    /// <summary>
    /// As Windows PowerShell writes it, line by line with a carriage return.
    /// </summary>
    [Test]
    public async Task WindowsLineEndingsAreRead()
    {
        var outcome = ToolUpdateOutcome.Parse("failed\r\nTool 'sampleapp' failed to update.\r\nIt did not work.\r\n");

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.Reason).IsEqualTo("It did not work.");
    }

    /// <summary>
    /// Read as the failure it could only have been, rather than as a success with a stray first
    /// line.
    /// </summary>
    [Test]
    public async Task OutputWithNoStatusLineIsAFailure()
    {
        var outcome = ToolUpdateOutcome.Parse("Tool 'sampleapp' failed to update due to the following:\nIt did not work.\n");

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.Reason).IsEqualTo("It did not work.");
    }

    [Test]
    public async Task AFailureThatSaidNothingSaysSo() =>
        await Assert.That(ToolUpdateOutcome.Parse("failed\n\n").Reason).IsEqualTo("dotnet tool update failed without saying why.");

    [Test]
    public async Task NoFileIsNoOutcome() =>
        await Assert.That(ToolUpdateOutcome.Take(Path.Combine(Path.GetTempPath(), $"PlatformHeads.Missing.{Guid.NewGuid():N}.log"))).IsNull();
}
