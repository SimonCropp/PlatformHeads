public class ClipboardPumpTests
{
    const string prompt = "The triage prompt";

    /// <summary>
    /// What a window does with each set, and what it was asked to take.
    /// </summary>
    sealed class Desktop
    {
        public bool Busy { get; set; }

        public List<string> Asked { get; } = [];

        public bool Set(string text)
        {
            Asked.Add(text);
            return !Busy;
        }
    }

    sealed record Copy(string Text);

    [Test]
    public async Task TextTheDesktopTakesIsTaken()
    {
        var desktop = new Desktop();
        var copy = new Copy(prompt);

        var offer = new ClipboardPump().Offer(copy, copy.Text, desktop.Set);

        await Assert.That(offer).IsEqualTo(ClipboardOffer.Taken);
        await Assert.That(desktop.Asked).IsEquivalentTo([prompt]);
    }

    /// <summary>
    /// The bug the pump exists for: a busy clipboard used to drop the text, and leave the status
    /// saying it had been copied.
    /// </summary>
    [Test]
    public async Task ABusyClipboardIsOfferedAgain()
    {
        var desktop = new Desktop { Busy = true };
        var copy = new Copy(prompt);

        var offer = new ClipboardPump().Offer(copy, copy.Text, desktop.Set);

        await Assert.That(offer).IsEqualTo(ClipboardOffer.Again);
    }

    [Test]
    public async Task AClipboardThatFreesUpInTimeStillTakesIt()
    {
        var desktop = new Desktop { Busy = true };
        var copy = new Copy(prompt);
        var pump = new ClipboardPump();
        pump.Offer(copy, copy.Text, desktop.Set);
        desktop.Busy = false;

        var offer = pump.Offer(copy, copy.Text, desktop.Set);

        await Assert.That(offer).IsEqualTo(ClipboardOffer.Taken);
        await Assert.That(desktop.Asked.Count).IsEqualTo(2);
    }

    /// <summary>
    /// Bounded, because a clipboard someone else owns for good would otherwise be asked sixty times
    /// a second, each attempt a second long inside the toolkit.
    /// </summary>
    [Test]
    public async Task AClipboardThatStaysBusyIsGivenUpOn()
    {
        var desktop = new Desktop { Busy = true };
        var copy = new Copy(prompt);
        var pump = new ClipboardPump();
        List<ClipboardOffer> offers = [];
        for (var attempt = 0; attempt < ClipboardPump.Attempts; attempt++)
        {
            offers.Add(pump.Offer(copy, copy.Text, desktop.Set));
        }

        await Assert.That(offers.Last()).IsEqualTo(ClipboardOffer.GaveUp);
        await Assert.That(offers.Take(ClipboardPump.Attempts - 1).All(_ => _ == ClipboardOffer.Again)).IsTrue();
        await Assert.That(pump.Tries).IsEqualTo(ClipboardPump.Attempts);
    }

    /// <summary>
    /// A second copy while the first is still being tried is a new copy, so it gets its own
    /// attempts rather than inheriting a count that would give up on it at once.
    /// </summary>
    [Test]
    public async Task ASecondCopyStartsItsOwnAttempts()
    {
        var desktop = new Desktop { Busy = true };
        var first = new Copy(prompt);
        var pump = new ClipboardPump();
        for (var attempt = 0; attempt < ClipboardPump.Attempts - 1; attempt++)
        {
            pump.Offer(first, first.Text, desktop.Set);
        }

        var second = new Copy("The log");
        var offer = pump.Offer(second, second.Text, desktop.Set);

        await Assert.That(offer).IsEqualTo(ClipboardOffer.Again);
        await Assert.That(pump.Tries).IsEqualTo(1);
    }

    /// <summary>
    /// The same text copied twice is two copies, told apart by reference as the state tells them.
    /// </summary>
    [Test]
    public async Task TheSameTextCopiedAgainIsAnotherCopy()
    {
        var desktop = new Desktop { Busy = true };
        var pump = new ClipboardPump();
        pump.Offer(new Copy(prompt), prompt, desktop.Set);

        pump.Offer(new Copy(prompt), prompt, desktop.Set);

        await Assert.That(pump.Tries).IsEqualTo(1);
    }

    /// <summary>
    /// A copy given up on is let go of, so the same one offered again starts over.
    /// </summary>
    [Test]
    public async Task ACopyGivenUpOnStartsOverIfOfferedAgain()
    {
        var desktop = new Desktop { Busy = true };
        var copy = new Copy(prompt);
        var pump = new ClipboardPump();
        for (var attempt = 0; attempt < ClipboardPump.Attempts; attempt++)
        {
            pump.Offer(copy, copy.Text, desktop.Set);
        }

        var offer = pump.Offer(copy, copy.Text, desktop.Set);

        await Assert.That(offer).IsEqualTo(ClipboardOffer.Again);
        await Assert.That(pump.Tries).IsEqualTo(1);
    }
}
