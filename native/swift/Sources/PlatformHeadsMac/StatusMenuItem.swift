/// One entry of a status item's menu, as a managed `TrayMenuItem` is: what it says, whether it can
/// be chosen, whether it is a toggle that is checked, and the submenu it opens. Equal by value, so
/// a frame that hands over the same menu changes nothing.
public struct StatusMenuItem: Equatable {
    public let id: String
    public let label: String

    /// A glyph handed to `StatusItem.setMenuIcon`, or empty for none.
    public let icon: String
    public let enabled: Bool
    public let separator: Bool

    /// Nil for an item that is not a toggle.
    public let checked: Bool?
    public let children: [StatusMenuItem]

    public init(
        id: String,
        label: String,
        icon: String = "",
        enabled: Bool = true,
        separator: Bool = false,
        checked: Bool? = nil,
        children: [StatusMenuItem] = []) {
        self.id = id
        self.label = label
        self.icon = icon
        self.enabled = enabled
        self.separator = separator
        self.checked = checked
        self.children = children
    }
}
