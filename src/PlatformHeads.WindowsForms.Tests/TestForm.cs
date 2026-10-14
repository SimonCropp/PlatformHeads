/// <summary>
/// A <see cref="HeadForm"/> with nothing in it, which says what the base reports to a subclass.
/// </summary>
sealed class TestForm(WindowPlacement? placement = null) :
    HeadForm("Test", new(640, 400), placement)
{
    public bool InputWaiting { get; set; }

    public bool Spinning { get; set; }

    public override bool Pending => InputWaiting;

    protected override bool Animating => Spinning;

    public bool CloseRequest() =>
        TakeCloseRequest();

    public WindowPlacement? Settled() =>
        TakeSettled();

    public void StartModal() =>
        EnterModal();

    public void StopModal() =>
        ExitModal();

    /// <summary>
    /// Shown without the keyboard, and where nobody can see it.
    /// </summary>
    public void ShowParked()
    {
        Parked = true;
        Show();
    }
}
