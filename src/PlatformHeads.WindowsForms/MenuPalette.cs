namespace PlatformHeads;

/// <summary>
/// The colours a <see cref="PaletteMenuRenderer" /> draws a menu in, from an app's own palette, so
/// a native popup matches the window it opens over.
/// </summary>
/// <param name="Background">Behind the items, and the image margin.</param>
/// <param name="Border">Around the menu.</param>
/// <param name="ItemBorder">Around the item under the pointer.</param>
/// <param name="Selected">Behind the item under the pointer.</param>
/// <param name="Pressed">Behind an item held down, which in a drop down is one that opens a submenu.</param>
/// <param name="Separator">A separator's line.</param>
/// <param name="Text">An item's text, and the arrow of a submenu.</param>
/// <param name="Dim">A disabled item's text.</param>
public sealed record MenuPalette(
    Color Background,
    Color Border,
    Color ItemBorder,
    Color Selected,
    Color Pressed,
    Color Separator,
    Color Text,
    Color Dim);
