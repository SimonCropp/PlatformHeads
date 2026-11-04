namespace PlatformHeads;

/// <summary>
/// What a <see cref="TrayMenuItem" /> is, as bits, for a C ABI that hands a menu over as an array
/// of structs: one <c>int32_t</c> where a struct cannot hold a <c>bool?</c>. The same values as
/// <c>StatusMenuFlags</c> in PlatformHeadsMac, which reads them back.
/// </summary>
[Flags]
public enum TrayMenuFlags
{
    None = 0,
    Enabled = 1,
    Separator = 2,

    /// <summary>
    /// A toggle, which <see cref="Checked" /> says the state of. Without it the item is not a toggle
    /// at all, which a check that is off cannot say.
    /// </summary>
    Checkable = 4,
    Checked = 8
}
