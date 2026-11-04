/// The bits a managed `TrayMenuFlags` holds, as an app's C ABI carries them in one `int32_t` a
/// menu item.
public struct StatusMenuFlags: OptionSet {
    public let rawValue: Int32

    public init(rawValue: Int32) {
        self.rawValue = rawValue
    }

    public static let enabled = StatusMenuFlags(rawValue: 1)
    public static let separator = StatusMenuFlags(rawValue: 2)

    /// A toggle, which `checked` says the state of. Without it the item is not a toggle at all.
    public static let checkable = StatusMenuFlags(rawValue: 4)
    public static let checked = StatusMenuFlags(rawValue: 8)
}

/// A menu as an app's C ABI hands it over, made into a tree again: one array with each item's
/// parent by index, which is what the managed `TrayMenuItem.Flatten` makes, since a C struct
/// cannot hold a list of itself. And a menu as text, for a test that can make no status item.
extension StatusMenuItem {
    public init(id: String, label: String, icon: String = "", flags: StatusMenuFlags, children: [StatusMenuItem] = []) {
        self.init(
            id: id,
            label: label,
            icon: icon,
            enabled: flags.contains(.enabled),
            separator: flags.contains(.separator),
            checked: flags.contains(.checkable) ? flags.contains(.checked) : nil,
            children: children)
    }

    /// The tree `entries` describe: `parent` is the index of the entry whose submenu an entry is
    /// in, or -1 for one of the menu itself, and `item` makes an entry an item given the children
    /// already made for it. An entry is only taken as a child of one before it, as the managed
    /// side writes them, so a parent that names the entry itself or one after it cannot loop.
    public static func tree<Entry>(
        _ entries: [Entry],
        parent: (Entry) -> Int32,
        item: (Entry, [StatusMenuItem]) -> StatusMenuItem) -> [StatusMenuItem] {
        var byParent: [Int32: [Int]] = [:]
        for (position, entry) in entries.enumerated() {
            byParent[parent(entry), default: []].append(position)
        }

        return children(of: -1, entries, byParent, item)
    }

    private static func children<Entry>(
        of index: Int32,
        _ entries: [Entry],
        _ byParent: [Int32: [Int]],
        _ item: (Entry, [StatusMenuItem]) -> StatusMenuItem) -> [StatusMenuItem] {
        (byParent[index] ?? [])
            .filter { $0 > Int(index) }
            .map { item(entries[$0], children(of: Int32($0), entries, byParent, item)) }
    }

    /// A line an item, two spaces deeper for each submenu: its label and id, and whatever else it
    /// has, the way the managed tests write a menu.
    public static func describe(_ items: [StatusMenuItem], depth: Int = 0) -> String {
        var lines = ""
        for item in items {
            lines += String(repeating: " ", count: depth * 2)
            if item.separator {
                lines += "----\n"
                continue
            }

            lines += "\(item.label)  [id: \(item.id)]"
            if !item.icon.isEmpty {
                lines += "  [glyph: \(item.icon)]"
            }

            if !item.enabled {
                lines += "  [disabled]"
            }

            if let checked = item.checked {
                lines += checked ? "  [checked]" : "  [unchecked]"
            }

            lines += "\n"
            lines += describe(item.children, depth: depth + 1)
        }

        return lines
    }
}
