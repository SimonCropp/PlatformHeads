/// <summary>
/// A placement is kept as text, in a file a person can edit.
/// </summary>
public class WindowPlacementTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task WhatIsWrittenIsReadBack(bool maximized)
    {
        var placement = new WindowPlacement(-1920, 40, 1100, 700, maximized);

        await Assert.That(WindowPlacement.Parse(placement.ToString())).IsEqualTo(placement);
    }

    [Test]
    public async Task ItIsFiveNumbers() =>
        await Assert.That(new WindowPlacement(10, 20, 300, 400, true).ToString()).IsEqualTo("10,20,300,400,1");

    /// <summary>
    /// A window opened from a line that makes no sense opens as a new one does.
    /// </summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("10,20,300,400")]
    [Arguments("10,20,300,400,0,0")]
    [Arguments("10,20,wide,400,0")]
    [Arguments("10,20,0,400,0")]
    [Arguments("10,20,300,-1,0")]
    [Arguments("1.5,20,300,400,0")]
    public async Task ALineThatMakesNoSenseIsNoPlacement(string? value) =>
        await Assert.That(WindowPlacement.Parse(value)).IsNull();

    /// <summary>
    /// Whatever culture the machine is in, since the file outlives a change of it.
    /// </summary>
    [Test]
    public async Task TheCultureMakesNoDifference()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new("ar-SA");
            var placement = new WindowPlacement(-5, 6, 700, 800);

            await Assert.That(placement.ToString()).IsEqualTo("-5,6,700,800,0");
            await Assert.That(WindowPlacement.Parse("-5,6,700,800,0")).IsEqualTo(placement);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
