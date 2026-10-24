namespace PlatformHeads;

/// <summary>
/// One dbusmenu node, and which of its properties it has: a host asks a node for every property it
/// says it has, and draws a separator for one with a type and nothing else.
/// </summary>
/// <param name="Id">dbusmenu's number for the node, 0 for the root.</param>
/// <param name="ItemId">The <see cref="TrayMenuItem.Id" /> it reports when clicked, or null for a separator or the root.</param>
/// <param name="Label">The label the host draws.</param>
/// <param name="Enabled">Whether the host lets it be clicked.</param>
/// <param name="Separator">A line between groups.</param>
/// <param name="Icon">The glyph's name, kept for comparing layouts.</param>
/// <param name="Checked">Null for an item that is no toggle, otherwise whether the host draws a check.</param>
/// <param name="IconPng">The glyph as PNG, resolved once so a property read costs no decoding.</param>
/// <param name="Children">The submenu it opens.</param>
public sealed record DbusMenuNode(
    int Id,
    string? ItemId,
    string Label,
    bool Enabled,
    bool Separator,
    string? Icon,
    bool? Checked,
    byte[]? IconPng,
    IReadOnlyList<DbusMenuNode> Children)
{
    public IEnumerable<string> PropertyNames
    {
        get
        {
            if (Separator)
            {
                yield return "type";
                yield break;
            }

            if (Id != 0)
            {
                yield return "label";
                yield return "enabled";
                yield return "visible";
                if (IconPng is not null)
                {
                    yield return "icon-data";
                }

                if (Checked is not null)
                {
                    yield return "toggle-type";
                    yield return "toggle-state";
                }
            }

            if (Children.Count > 0)
            {
                yield return "children-display";
            }
        }
    }
}
