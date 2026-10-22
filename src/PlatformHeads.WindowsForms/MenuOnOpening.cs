namespace PlatformHeads;

/// <summary>
/// A tray icon's menu, built again every time it opens, which is when what it lists matters,
/// rather than kept up to date on every change.
/// </summary>
public static class MenuOnOpening
{
    /// <summary>
    /// Has <paramref name="build"/> add the menu's items each time it opens. Whatever is on the menu
    /// already stays, as itself: items were once told apart by their text, so an item built with
    /// the text of a kept one was taken for it, and never removed.
    /// <para>
    /// What an open added is taken off as the menu closes and disposed at the next open, rather
    /// than disposed while the close that removed it is still running.
    /// </para>
    /// <para>
    /// A <see cref="ContextMenuStrip"/> with no items marks its <see cref="ToolStripDropDown.Opening"/>
    /// cancelled before raising it, and one with nothing kept has none until the first build, so
    /// the open is let through whenever the build added anything: otherwise the first click on the
    /// icon showed nothing.
    /// </para>
    /// </summary>
    public static void Rebuild(ContextMenuStrip menu, Action<ToolStripItemCollection> build)
    {
        var items = menu.Items;
        var kept = new HashSet<ToolStripItem>(items.Cast<ToolStripItem>());
        var removed = new List<ToolStripItem>();

        // Materialised, because this removes from the collection it reads
        void TakeBuilt()
        {
            foreach (var item in items.Cast<ToolStripItem>().Where(_ => !kept.Contains(_)).ToList())
            {
                items.Remove(item);
                removed.Add(item);
            }
        }

        menu.Closed += (_, _) => TakeBuilt();
        menu.Opening += (_, arguments) =>
        {
            // Also here, for an open that never closed, as one another handler cancelled: built
            // again over what it left, every item would be there twice
            TakeBuilt();
            foreach (var item in removed)
            {
                item.Dispose();
            }

            removed.Clear();
            build(items);
            arguments.Cancel = items.Count == 0;
        };
    }
}
