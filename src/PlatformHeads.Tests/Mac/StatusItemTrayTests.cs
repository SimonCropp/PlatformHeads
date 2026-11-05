/// <summary>
/// The macOS tray's menu across the C ABI. A C struct cannot hold a list of itself, so the menu
/// goes over as one array with each item's parent by index, and the library makes it a tree again.
/// <para>
/// Against libplatformheads_tray.dylib, which the macOS job of build.yml builds from this
/// repository's Swift package and copies beside these tests: an app links the same target into a
/// library of its own name. Every test here names that one library, since a process resolves the
/// tray's P/Invokes to one.
/// </para>
/// </summary>
public class StatusItemTrayTests
{
    const string Library = "platformheads_tray";

    /// <summary>
    /// The library's own reading of what it was handed, every field of every item, against the
    /// menu it was handed. Needs no status bar, so it runs on a CI runner.
    /// </summary>
    [Test]
    [RunOn(TUnit.Core.Enums.OS.MacOs)]
    public async Task TheLibraryRebuildsTheMenuItWasHanded()
    {
        StatusItemLibrary.Use(Library);
        await Assert.That(StatusItemLibrary.Version()).IsEqualTo(StatusItemLibrary.ExpectedVersion);

        await Assert.That(StatusItemLibrary.Describe(Menu)).IsEqualTo(Describe(Menu, 0));
    }

    /// <summary>
    /// Anywhere but macOS there is no such library, which both say rather than throw, as an app
    /// built for every platform finds on any machine its macOS head was copied to by mistake.
    /// </summary>
    [Test]
    [RunOn(TUnit.Core.Enums.OS.Windows | TUnit.Core.Enums.OS.Linux)]
    public async Task WithNoLibraryBothSaySo()
    {
        var tray = StatusItemTray<TestTrayIcon>.Open(new(Library, "Test", (_, _) => null), out var error);

        await Assert.That(tray).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(StatusItemApp.Start(Library, "Testing")).IsNotNull();
    }

    /// <summary>
    /// What <c>ph_tray_describe</c> writes for a menu, written here from the managed items.
    /// </summary>
    static string Describe(IReadOnlyList<TrayMenuItem> items, int depth)
    {
        var builder = new StringBuilder();
        foreach (var item in items)
        {
            builder.Append(' ', depth * 2);
            if (item.Separator)
            {
                builder.Append("----\n");
                continue;
            }

            builder.Append($"{item.Label}  [id: {item.Id}]");
            if (item.IconName is { Length: > 0 } icon)
            {
                builder.Append($"  [glyph: {icon}]");
            }

            if (!item.Enabled)
            {
                builder.Append("  [disabled]");
            }

            if (item.Checked is { } on)
            {
                builder.Append(on ? "  [checked]" : "  [unchecked]");
            }

            builder.Append('\n');
            builder.Append(Describe(item.Children ?? [], depth + 1));
        }

        return builder.ToString();
    }

    /// <summary>
    /// A menu with each thing the ABI carries: a glyph, a toggle each way, a separator, a disabled
    /// heading, a submenu in a submenu, and text beyond ASCII.
    /// </summary>
    static readonly IReadOnlyList<TrayMenuItem> Menu =
    [
        new("exit", "Exit", IconName: "exit"),
        new("startup", "Run at startup", Checked: true),
        new("separator:1", "", Separator: true),
        new("group:Solution", "Solution", Enabled: false, IconName: "solution"),
        new(
            "move:a",
            "Sample.Test (txt)",
            Children:
            [
                new("move:a:accept", "Accept move"),
                new(
                    "move:a:more",
                    "More",
                    Children:
                    [
                        new("move:a:more:deeper", "Ünïcödé, 日本語", Checked: false)
                    ])
            ]),
        new(
            "move:b",
            "Other (png)",
            Children:
            [
                new("move:b:accept", "Accept move", IconName: "accept")
            ])
    ];
}
