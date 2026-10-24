namespace PlatformHeads;

/// <summary>
/// A tray menu as dbusmenu sees it: nodes numbered depth first under a root with id 0, a submenu's
/// items under the item that opens it. Pure, so it is tested without a bus.
/// </summary>
public sealed class DbusMenuLayout
{
    public static DbusMenuLayout Empty { get; } = new(new(0, null, "", true, false, null, null, null, []), []);

    DbusMenuLayout(DbusMenuNode root, List<DbusMenuNode> all)
    {
        Root = root;
        All = all;
        Signature = string.Join('\n', all.Select(_ => $"{_.Id}|{_.ItemId}|{_.Label}|{_.Enabled}|{_.Separator}|{_.Icon}|{_.Checked}|{_.Children.Count}"));
    }

    public DbusMenuNode Root { get; }

    /// <summary>
    /// Every node but the root, depth first, so a submenu's items follow the item that opens it.
    /// </summary>
    public IReadOnlyList<DbusMenuNode> All { get; }

    /// <summary>
    /// What the menu is, as text, so two layouts are compared without walking them. Each node's
    /// count of children is in it, which with the order is what says where a submenu ends.
    /// </summary>
    public string Signature { get; }

    public DbusMenuNode? Find(int id)
    {
        if (id == 0)
        {
            return Root;
        }

        return All.FirstOrDefault(_ => _.Id == id);
    }

    /// <param name="items">The menu, as the app's model has it.</param>
    /// <param name="glyph">A glyph as a 16 pixel PNG, read once per node as it is built.</param>
    public static DbusMenuLayout Build(IReadOnlyList<TrayMenuItem> items, Func<string, int, byte[]?>? glyph = null)
    {
        var all = new List<DbusMenuNode>();
        var next = 1;
        var children = Nodes(items, glyph, all, ref next);
        var root = new DbusMenuNode(0, null, "", true, false, null, null, null, children);
        return new(root, all);
    }

    static List<DbusMenuNode> Nodes(IReadOnlyList<TrayMenuItem> items, Func<string, int, byte[]?>? glyph, List<DbusMenuNode> all, ref int next)
    {
        var nodes = new List<DbusMenuNode>();
        foreach (var item in items)
        {
            var id = next++;
            var index = all.Count;
            var children = item.HasChildren ? Nodes(item.Children!, glyph, all, ref next) : [];
            var png = item.IconName is null ? null : glyph?.Invoke(item.IconName, 16);
            var node = new DbusMenuNode(id, item.Separator ? null : item.Id, item.Label, item.Enabled, item.Separator, item.IconName, item.Separator ? null : item.Checked, png, children);
            // Ahead of the children just added, so the list is in the order of the ids
            all.Insert(index, node);
            nodes.Add(node);
        }

        return nodes;
    }
}
