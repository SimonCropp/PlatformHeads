import XCTest
@testable import PlatformHeadsMac

/// A menu handed over as one array with each item's parent by index, as an app's C ABI carries it.
final class FlatMenuTests: XCTestCase {
    private struct Entry {
        let id: String
        let flags: StatusMenuFlags
        let parent: Int32
    }

    private func tree(_ entries: [Entry]) -> [StatusMenuItem] {
        StatusMenuItem.tree(entries, parent: { $0.parent }) { entry, children in
            StatusMenuItem(id: entry.id, label: entry.id, flags: entry.flags, children: children)
        }
    }

    func testEachItemIsUnderItsParent() {
        let items = tree([
            Entry(id: "open", flags: .enabled, parent: -1),
            Entry(id: "pending", flags: .enabled, parent: -1),
            Entry(id: "accept", flags: .enabled, parent: 1),
            Entry(id: "more", flags: .enabled, parent: 1),
            Entry(id: "discard", flags: [], parent: 3),
            Entry(id: "exit", flags: .enabled, parent: -1)
        ])

        XCTAssertEqual(
            StatusMenuItem.describe(items),
            """
            open  [id: open]
            pending  [id: pending]
              accept  [id: accept]
              more  [id: more]
                discard  [id: discard]  [disabled]
            exit  [id: exit]

            """)
    }

    /// A check that is off and no check at all are different items.
    func testTheFlagsSayWhatTheItemIs() {
        let items = tree([
            Entry(id: "line", flags: .separator, parent: -1),
            Entry(id: "off", flags: [.enabled, .checkable], parent: -1),
            Entry(id: "on", flags: [.enabled, .checkable, .checked], parent: -1),
            Entry(id: "plain", flags: .enabled, parent: -1)
        ])

        XCTAssertTrue(items[0].separator)
        XCTAssertEqual(items[1].checked, false)
        XCTAssertEqual(items[2].checked, true)
        XCTAssertNil(items[3].checked)
        XCTAssertFalse(items[0].enabled)
    }

    /// The values the managed TrayMenuFlags has, which the headers write down.
    func testTheFlagsHaveTheValuesTheHeadersUse() {
        XCTAssertEqual(StatusMenuFlags.enabled.rawValue, 1)
        XCTAssertEqual(StatusMenuFlags.separator.rawValue, 2)
        XCTAssertEqual(StatusMenuFlags.checkable.rawValue, 4)
        XCTAssertEqual(StatusMenuFlags.checked.rawValue, 8)
    }

    /// A parent that names the item itself, or one after it, is not one the managed side writes,
    /// and taking it would never end.
    func testAParentThatIsNotBeforeTheItemIsPassedOver() {
        let items = tree([
            Entry(id: "open", flags: .enabled, parent: -1),
            Entry(id: "self", flags: .enabled, parent: 1),
            Entry(id: "later", flags: .enabled, parent: 3),
            Entry(id: "after", flags: .enabled, parent: 2)
        ])

        XCTAssertEqual(items.map { $0.id }, ["open"])
        XCTAssertTrue(items[0].children.isEmpty)
    }

    func testNoEntriesIsNoMenu() {
        XCTAssertTrue(tree([]).isEmpty)
        XCTAssertEqual(StatusMenuItem.describe([]), "")
    }
}
