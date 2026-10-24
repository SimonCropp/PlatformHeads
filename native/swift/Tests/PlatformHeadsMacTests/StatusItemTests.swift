import AppKit
import XCTest
@testable import PlatformHeadsMac

/// The menu a status item builds, without a menu bar to put the item in: `start` is never called.
final class StatusItemTests: XCTestCase {
    private let items = [
        StatusMenuItem(id: "open", label: "Open"),
        StatusMenuItem(id: "", label: "", separator: true),
        StatusMenuItem(id: "pending", label: "Pending", children: [
            StatusMenuItem(id: "accept", label: "Accept"),
            StatusMenuItem(id: "discard", label: "Discard", enabled: false)
        ]),
        StatusMenuItem(id: "startup", label: "Run at startup", checked: true)
    ]

    func testTheMenuHasTheItemsTheirSubmenusAndTheirChecks() {
        let status = StatusItem()
        status.apply(icon: 0, tooltip: "Tray", items: items)

        let menu = status.menu()

        XCTAssertEqual(menu.items.map { $0.title }, ["Open", "", "Pending", "Run at startup"])
        XCTAssertTrue(menu.items[1].isSeparatorItem)
        XCTAssertEqual(menu.items[2].submenu?.items.map { $0.title }, ["Accept", "Discard"])
        XCTAssertEqual(menu.items[2].submenu?.items.map { $0.isEnabled }, [true, false])
        XCTAssertEqual(menu.items[3].state, .on)
        XCTAssertEqual(menu.items[0].state, .off)
    }

    /// An item that opens a submenu is not one to choose: a host clicks it on the way to the
    /// submenu, and the app would hear a choice nobody made.
    func testChoosingAnItemReportsItAndAnItemWithASubmenuHasNoAction() {
        let status = StatusItem()
        var chosen: [String] = []
        status.chose = { chosen.append($0.id) }
        status.apply(icon: 0, tooltip: "Tray", items: items)

        let menu = status.menu()
        menu.performActionForItem(at: 0)
        menu.items[2].submenu?.performActionForItem(at: 0)

        XCTAssertEqual(chosen, ["open", "accept"])
        XCTAssertNil(menu.items[2].action)
    }
}
