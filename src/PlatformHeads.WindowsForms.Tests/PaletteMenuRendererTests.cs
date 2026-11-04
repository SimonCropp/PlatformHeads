/// <summary>
/// A menu drawn in an app's palette rather than the system's colours.
/// </summary>
public class PaletteMenuRendererTests
{
    static readonly MenuPalette palette = new(
        Background: Color.FromArgb(30, 30, 30),
        Border: Color.FromArgb(60, 60, 60),
        ItemBorder: Color.FromArgb(70, 70, 70),
        Selected: Color.FromArgb(0, 90, 160),
        Pressed: Color.FromArgb(40, 40, 40),
        Separator: Color.FromArgb(80, 80, 80),
        Text: Color.FromArgb(230, 230, 230),
        Dim: Color.FromArgb(120, 120, 120));

    [Test]
    public async Task TheColourTableIsThePalettes()
    {
        var table = new PaletteMenuRenderer(palette).ColorTable;

        await Assert.That(table.ToolStripDropDownBackground).IsEqualTo(palette.Background);
        await Assert.That(table.ImageMarginGradientBegin).IsEqualTo(palette.Background);
        await Assert.That(table.ImageMarginGradientMiddle).IsEqualTo(palette.Background);
        await Assert.That(table.ImageMarginGradientEnd).IsEqualTo(palette.Background);
        await Assert.That(table.MenuBorder).IsEqualTo(palette.Border);
        await Assert.That(table.MenuItemBorder).IsEqualTo(palette.ItemBorder);
        await Assert.That(table.MenuItemSelected).IsEqualTo(palette.Selected);
        await Assert.That(table.MenuItemSelectedGradientBegin).IsEqualTo(palette.Selected);
        await Assert.That(table.MenuItemSelectedGradientEnd).IsEqualTo(palette.Selected);
        await Assert.That(table.MenuItemPressedGradientBegin).IsEqualTo(palette.Pressed);
        await Assert.That(table.MenuItemPressedGradientMiddle).IsEqualTo(palette.Pressed);
        await Assert.That(table.MenuItemPressedGradientEnd).IsEqualTo(palette.Pressed);
        await Assert.That(table.SeparatorDark).IsEqualTo(palette.Separator);
        await Assert.That(table.SeparatorLight).IsEqualTo(palette.Separator);
    }

    /// <summary>
    /// Item text is no member of the table: the item hands the renderer the system's colour, which
    /// the renderer replaces.
    /// </summary>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AnItemsTextIsThePalettesAndDimWhenDisabled(bool enabled)
    {
        using var bitmap = new Bitmap(100, 20);
        using var graphics = Graphics.FromImage(bitmap);
        using var item = new ToolStripMenuItem("Open")
        {
            Enabled = enabled
        };
        var args = new ToolStripItemTextRenderEventArgs(graphics, item, "Open", new(0, 0, 100, 20), SystemColors.MenuText, item.Font, TextFormatFlags.Default);

        new PaletteMenuRenderer(palette).DrawItemText(args);

        await Assert.That(args.TextColor).IsEqualTo(enabled ? palette.Text : palette.Dim);
    }

    [Test]
    public async Task ASubmenusArrowIsTheText()
    {
        using var bitmap = new Bitmap(20, 20);
        using var graphics = Graphics.FromImage(bitmap);
        using var item = new ToolStripMenuItem("More");
        var args = new ToolStripArrowRenderEventArgs(graphics, item, new(0, 0, 20, 20), SystemColors.ControlText, ArrowDirection.Right);

        new PaletteMenuRenderer(palette).DrawArrow(args);

        await Assert.That(args.ArrowColor).IsEqualTo(palette.Text);
    }
}
