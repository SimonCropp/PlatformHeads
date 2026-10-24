import AppKit

/// A tray app's menu bar item. The app hands over the pictures for its icons and glyphs once, then
/// a frame's icon, tooltip and menu as often as it likes; the menu is built from the last of those
/// when it opens, which is when its contents matter.
///
/// A left click is `iconClicked`, which on the other platforms' trays shows the app's window, and
/// the menu is the right button's, or a control-click's, which is how a Mac without a right button
/// asks for one. Both buttons opened the menu once, so the app never heard a left click.
///
/// Needs AppKit's events pumped, which `EventPump` does, and a status bar to sit in, which a test
/// has none of: `menu()` builds the menu without one.
public final class StatusItem: NSObject {
    /// The icon itself was clicked with the left button.
    public var iconClicked: (() -> Void)?

    /// An item with no submenu was chosen from the menu.
    public var chose: ((StatusMenuItem) -> Void)?

    public private(set) var items: [StatusMenuItem] = []

    private var item: NSStatusItem?
    private var icons: [Int32: NSImage] = [:]
    private var menuIcons: [String: NSImage] = [:]
    private var lastIcon: Int32 = -1
    private var lastTooltip = ""

    public override init() {
        super.init()
    }

    /// Puts the item in the menu bar. False when AppKit gave it no button to draw in.
    public func start() -> Bool {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        guard let button = item.button else {
            return false
        }

        button.target = self
        button.action = #selector(clicked(_:))
        button.sendAction(on: [.leftMouseUp, .rightMouseUp])
        self.item = item
        return true
    }

    /// Drawn at 18 points, the menu bar's height. A 48 pixel picture is sharp on any display.
    public func setIcon(_ icon: Int32, png: Data) {
        if let image = NSImage(data: png) {
            image.size = NSSize(width: 18, height: 18)
            icons[icon] = image
        }
    }

    public func setMenuIcon(_ name: String, png: Data) {
        if let image = NSImage(data: png) {
            image.size = NSSize(width: 16, height: 16)
            menuIcons[name] = image
        }
    }

    public func apply(icon: Int32, tooltip: String, items: [StatusMenuItem]) {
        if icon != lastIcon {
            lastIcon = icon
            item?.button?.image = icons[icon]
        }

        if tooltip != lastTooltip {
            lastTooltip = tooltip
            item?.button?.toolTip = tooltip
        }

        if items != self.items {
            self.items = items
        }
    }

    @objc private func clicked(_ sender: Any?) {
        guard let item else {
            return
        }

        let event = NSApp.currentEvent
        let wantsMenu = event?.type == .rightMouseUp || event?.modifierFlags.contains(.control) == true
        guard wantsMenu else {
            iconClicked?()
            return
        }

        item.menu = menu()
        item.button?.performClick(nil)
        item.menu = nil
    }

    /// The menu for the items last applied, submenus included.
    public func menu() -> NSMenu {
        build(items)
    }

    private func build(_ entries: [StatusMenuItem]) -> NSMenu {
        let menu = NSMenu()
        menu.autoenablesItems = false
        for entry in entries {
            if entry.separator {
                menu.addItem(.separator())
                continue
            }

            let menuItem = NSMenuItem(title: entry.label, action: nil, keyEquivalent: "")
            menuItem.isEnabled = entry.enabled
            menuItem.image = menuIcons[entry.icon]
            if let checked = entry.checked {
                menuItem.state = checked ? .on : .off
            }

            if entry.children.isEmpty {
                menuItem.target = self
                menuItem.action = #selector(choose(_:))
                menuItem.representedObject = entry
            } else {
                menuItem.submenu = build(entry.children)
            }

            menu.addItem(menuItem)
        }

        return menu
    }

    @objc private func choose(_ sender: NSMenuItem) {
        if let entry = sender.representedObject as? StatusMenuItem {
            chose?(entry)
        }
    }
}
