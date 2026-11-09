/// <summary>
/// The form's icons on its window class, which is what the taskbar shows when the window is slow
/// to answer: without them it showed Windows' stock icon for a head that had not pumped yet.
/// <para>
/// Alone, because every form in the process shares the one class, and a form any other test opens
/// alongside puts its own icon there.
/// </para>
/// </summary>
[TUnit.Core.Executors.STAThreadExecutor]
[NotInParallel]
public partial class ClassIconTests
{
    const int getIcon = 0x007F;
    const int smallKind = 0;
    const int bigSlot = -14;
    const int smallSlot = -34;

    [Test]
    public async Task TheClassCarriesTheWindowsIcons()
    {
        using var icon = new Icon(SystemIcons.Information, 32, 32);
        using var form = new TestForm
        {
            Icon = icon
        };

        form.ShowParked();

        await Assert.That(Slot(form, bigSlot)).IsEqualTo(icon.Handle);
        await Assert.That(Slot(form, smallSlot)).IsEqualTo(WindowIcon(form, smallKind));
        await Assert.That(Slot(form, smallSlot)).IsNotEqualTo(0);
    }

    [Test]
    public async Task AChangedIconIsFollowed()
    {
        using var first = new Icon(SystemIcons.Information, 32, 32);
        using var second = new Icon(SystemIcons.Warning, 32, 32);
        using var form = new TestForm
        {
            Icon = first
        };
        form.ShowParked();

        form.Icon = second;

        await Assert.That(Slot(form, bigSlot)).IsEqualTo(second.Handle);
        await Assert.That(Slot(form, smallSlot)).IsEqualTo(WindowIcon(form, smallKind));
    }

    /// <summary>
    /// A window shown with no icon has none on its class either, as the window has none to give.
    /// </summary>
    [Test]
    public async Task NoIconShownIsNoneOnTheClass()
    {
        using var icon = new Icon(SystemIcons.Information, 32, 32);
        using var form = new TestForm
        {
            Icon = icon
        };
        form.ShowParked();

        form.ShowIcon = false;

        await Assert.That(Slot(form, bigSlot)).IsEqualTo(0);
        await Assert.That(Slot(form, smallSlot)).IsEqualTo(0);
    }

    /// <summary>
    /// The class outlives the window, and is shared with every window of the same styles: a plain
    /// form is one.
    /// </summary>
    [Test]
    public async Task TheClassIsClearedAsTheWindowGoes()
    {
        using var icon = new Icon(SystemIcons.Information, 32, 32);
        using var plain = new Form();
        var form = new TestForm
        {
            Icon = icon
        };
        form.ShowParked();
        var shared = Slot(plain, bigSlot);

        form.CloseForReal();

        await Assert.That(shared).IsEqualTo(icon.Handle);
        await Assert.That(Slot(plain, bigSlot)).IsEqualTo(0);
        await Assert.That(Slot(plain, smallSlot)).IsEqualTo(0);
    }

    [Test]
    public async Task AnotherWindowsIconIsLeftStanding()
    {
        using var firstIcon = new Icon(SystemIcons.Information, 32, 32);
        using var secondIcon = new Icon(SystemIcons.Warning, 32, 32);
        var first = new TestForm
        {
            Icon = firstIcon
        };
        first.ShowParked();
        using var second = new TestForm
        {
            Icon = secondIcon
        };
        second.ShowParked();

        first.CloseForReal();

        await Assert.That(Slot(second, bigSlot)).IsEqualTo(secondIcon.Handle);
        await Assert.That(Slot(second, smallSlot)).IsEqualTo(WindowIcon(second, smallKind));
    }

    static nint Slot(Form form, int slot) =>
        GetClassLongPtr(form.Handle, slot);

    static nint WindowIcon(Form form, int kind) =>
        SendMessage(form.Handle, getIcon, kind, 0);

    // By their exported names: unlike DllImport, LibraryImport does not try the A and W suffixes.
    [LibraryImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static partial nint GetClassLongPtr(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial nint SendMessage(nint window, int message, nint wParam, nint lParam);
}
