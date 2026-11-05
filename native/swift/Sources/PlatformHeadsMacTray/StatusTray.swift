import PlatformHeadsMac

/// The status item behind `platformheads_tray.h`, and what has come of it since the managed side
/// last asked: the items chosen, in the order they were, and whether the icon was clicked.
///
/// One a process, since the C ABI has no handle to name another by. An app whose window library
/// pumps AppKit itself asks `hasInput` in its own pump, so a click on the menu ends the pump's wait
/// as a key does, rather than waiting out the frame.
public final class StatusTray {
    public static let shared = StatusTray()

    public let item = StatusItem()
    private var chosen: [String] = []
    private var iconClicked = false

    private init() {
        item.chose = { [unowned self] entry in
            self.chosen.append(entry.id)
        }
        item.iconClicked = { [unowned self] in
            self.iconClicked = true
        }
    }

    /// Something is waiting for the next poll to hand over.
    public var hasInput: Bool {
        !chosen.isEmpty || iconClicked
    }

    func takeChosen() -> String? {
        chosen.isEmpty ? nil : chosen.removeFirst()
    }

    func takeIconClicked() -> Bool {
        defer {
            iconClicked = false
        }

        return iconClicked
    }

    /// What `item` reports, as the menu bar would: for a test, which has no status bar to click in.
    func choose(_ id: String) {
        chosen.append(id)
    }

    func clickIcon() {
        iconClicked = true
    }
}
