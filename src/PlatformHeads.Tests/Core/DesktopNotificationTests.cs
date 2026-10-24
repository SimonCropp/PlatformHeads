public class DesktopNotificationTests
{
    /// <summary>
    /// Each argument stays whole, quotes and all, because they go to notify-send as a list rather
    /// than a shell line.
    /// </summary>
    [Test]
    public async Task NotifySendIsHandedTheAppTitleAndMessage() =>
        await Assert.That(DesktopNotification.Arguments("SampleApp", new("Build \"main\" failed", "It's red")))
            .IsEquivalentTo(["--app-name=SampleApp", "--urgency=normal", "Build \"main\" failed", "It's red"]);

    /// <summary>
    /// A quote or a backslash in the text would otherwise end AppleScript's string, and the rest
    /// would be read as script.
    /// </summary>
    [Test]
    public async Task TheScriptEscapesQuotesAndBackslashes() =>
        await Assert.That(DesktopNotification.Script(new("Build \"main\" failed", @"C:\work")))
            .IsEqualTo("display notification \"C:\\\\work\" with title \"Build \\\"main\\\" failed\"");
}
