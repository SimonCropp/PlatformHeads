import CPlatformHeadsTray
import XCTest
@testable import PlatformHeadsMacTray

/// The tray's C ABI as the managed side calls it, with no status bar: what it hands over and what
/// it hands back. `StatusTray` is one a process, so each test starts by draining it.
final class TrayExportsTests: XCTestCase {
    override func setUp() {
        while StatusTray.shared.takeChosen() != nil {
        }

        _ = StatusTray.shared.takeIconClicked()
    }

    func testTheVersionIsTheHeaders() {
        XCTAssertEqual(phTrayVersion(), Int32(PH_TRAY_VERSION))
    }

    /// The bits the header names, which the managed TrayMenuFlags and StatusMenuFlags also have.
    func testTheFlagsAreStatusMenuFlags() {
        XCTAssertEqual(PH_TRAY_ENABLED, 1)
        XCTAssertEqual(PH_TRAY_SEPARATOR, 2)
        XCTAssertEqual(PH_TRAY_CHECKABLE, 4)
        XCTAssertEqual(PH_TRAY_CHECKED, 8)
    }

    func testAnAppliedMenuIsDescribedAsTheTreeItWasHanded() {
        // Typed first: given the map's pointer initialiser, Swift took the literals for pointers
        let texts: [String] = ["exit", "Exit", "", "more", "More", "deeper", "Ünïcödé, 日本語", "glyph"]
        let strings = texts.map { UnsafePointer(strdup($0)!) }
        defer {
            strings.forEach { free(UnsafeMutablePointer(mutating: $0)) }
        }

        let items = [
            PhTrayItem(id: strings[0], label: strings[1], icon: strings[7], flags: PH_TRAY_ENABLED, parent: -1),
            PhTrayItem(id: strings[3], label: strings[4], icon: strings[2], flags: PH_TRAY_ENABLED, parent: -1),
            PhTrayItem(id: strings[5], label: strings[6], icon: strings[2], flags: PH_TRAY_CHECKABLE, parent: 1)
        ]
        items.withUnsafeBufferPointer {
            phTrayApply(1, "tip", $0.baseAddress, Int32($0.count))
        }

        var buffer = [CChar](repeating: 0, count: 1024)
        let length = phTrayDescribe(&buffer, Int32(buffer.count))
        let text = String(decoding: buffer.prefix(Int(length)).map { UInt8(bitPattern: $0) }, as: UTF8.self)
        XCTAssertEqual(
            text,
            """
            Exit  [id: exit]  [glyph: glyph]
            More  [id: more]
              Ünïcödé, 日本語  [id: deeper]  [disabled]  [unchecked]

            """)
    }

    /// Chosen items come back one a poll, in the order they were chosen, and a buffer too small for
    /// one is told how big it had to be.
    func testChosenItemsAreHandedOverOneAPollInOrder() {
        StatusTray.shared.choose("first")
        StatusTray.shared.choose("second-and-longer")
        XCTAssertTrue(StatusTray.shared.hasInput)

        var buffer = [CChar](repeating: 0, count: 8)
        XCTAssertEqual(phTrayPoll(&buffer, 8), 5)
        XCTAssertEqual(phTrayPoll(&buffer, 8), 17)
        XCTAssertEqual(phTrayPoll(&buffer, 8), 0)
        XCTAssertFalse(StatusTray.shared.hasInput)
    }

    /// A click on the icon is reported once, and is input until it has been.
    func testAnIconClickIsReportedOnce() {
        StatusTray.shared.clickIcon()
        XCTAssertTrue(StatusTray.shared.hasInput)
        XCTAssertEqual(phTrayIconClicked(), 1)
        XCTAssertEqual(phTrayIconClicked(), 0)
        XCTAssertFalse(StatusTray.shared.hasInput)
    }
}
