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

    /// <summary>
    /// The version rather than what the tool said, and not as an error, which made an update that
    /// worked look like one that had not.
    /// </summary>
    [Test]
    public async Task ASuccessSaysTheVersionRunning() =>
        await Assert.That(ToolUpdateOutcome.Parse("ok\nTool 'sampleapp' was successfully updated.\n").Notification("SampleApp", "1.2.3"))
            .IsEqualTo(new("SampleApp updated", "Now running 1.2.3.", Kind: TrayNotificationKind.Info));

    [Test]
    public async Task AFailureSaysWhy() =>
        await Assert.That(ToolUpdateOutcome.Parse(locked).Notification("SampleApp", "1.2.3"))
            .IsEqualTo(new("SampleApp did not update", """Failed to uninstall tool package 'sampleapp': Access to the path 'C:\Users\me\.dotnet\tools\.store\sampleapp\0.1.0' is denied."""));

    /// <summary>
    /// Reported once, with the whole of the outcome handed over first for the app's log.
    /// </summary>
    [Test]
    public async Task AReportIsMadeOnceAndTheOutcomeHandedOver()
    {
        var path = Path.Combine(Path.GetTempPath(), $"PlatformHeads.Outcome.{Guid.NewGuid():N}.log");
        try
        {
            await File.WriteAllTextAsync(path, locked);
            var taken = new List<ToolUpdateOutcome>();

            var notification = ToolUpdateOutcome.Report(path, "SampleApp", "1.2.3", taken.Add);

            await Assert.That(notification?.Title).IsEqualTo("SampleApp did not update");
            await Assert.That(taken.Count).IsEqualTo(1);
            await Assert.That(taken[0].Output).Contains("failed to update due to the following");
            await Assert.That(ToolUpdateOutcome.Report(path, "SampleApp", "1.2.3", taken.Add)).IsNull();
            await Assert.That(taken.Count).IsEqualTo(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// A file that cannot be read is told to the app and reports nothing, rather than throwing out
    /// of the app's start. A file open with no sharing is what Windows refuses to read; a Unix lock
    /// keeps nothing out.
    /// </summary>
    [Test]
    [RunOn(TUnit.Core.Enums.OS.Windows)]
    public async Task AnOutcomeThatCannotBeReadIsToldAndNotThrown()
    {
        var path = Path.Combine(Path.GetTempPath(), $"PlatformHeads.Outcome.{Guid.NewGuid():N}.log");
        try
        {
            await File.WriteAllTextAsync(path, locked);
            var failures = new List<Exception>();
            TrayNotification? notification;
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                notification = ToolUpdateOutcome.Report(path, "SampleApp", "1.2.3", failed: failures.Add);
            }

            await Assert.That(notification).IsNull();
            await Assert.That(failures.Count).IsEqualTo(1);
            await Assert.That(failures[0]).IsTypeOf<IOException>();
            await Assert.That(File.Exists(path)).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
