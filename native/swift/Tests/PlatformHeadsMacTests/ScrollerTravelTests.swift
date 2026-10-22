import AppKit
import XCTest
@testable import PlatformHeadsMac

final class ScrollerTravelTests: XCTestCase {
    /// Counted in rows, so the far end of the travel is the last top the managed side allows.
    func testTheKnobIsWhereTheRowsAre() {
        let travel = ScrollerTravel(top: 45, visible: 10, total: 100)
        XCTAssertEqual(travel.maximum, 90)
        XCTAssertEqual(travel.value, 0.5)
        XCTAssertEqual(travel.knobProportion, 0.1)
        XCTAssertEqual(travel.row(at: 1), 90)
        XCTAssertEqual(travel.row(at: 2), 90)
        XCTAssertEqual(travel.row(at: -1), 0)
    }

    /// Rows that all fit have no travel, and the knob is the whole slot.
    func testRowsThatFitHaveNoTravel() {
        let travel = ScrollerTravel(top: 0, visible: 10, total: 4)
        XCTAssertEqual(travel.maximum, 0)
        XCTAssertEqual(travel.value, 0)
        XCTAssertEqual(travel.knobProportion, 1)
    }

    func testAPressInTheSlotIsAPageAndOnAnArrowARow() {
        let travel = ScrollerTravel(top: 85, visible: 10, total: 100)
        XCTAssertEqual(travel.row(for: .incrementPage, value: 0), 90)
        XCTAssertEqual(travel.row(for: .decrementPage, value: 0), 75)
        XCTAssertEqual(travel.row(for: .incrementLine, value: 0), 86)
        XCTAssertEqual(travel.row(for: .decrementLine, value: 0), 84)
        XCTAssertEqual(travel.row(for: .knob, value: 0.25), 23)
        XCTAssertEqual(ScrollerTravel(top: 3, visible: 10, total: 100).row(for: .decrementPage, value: 0), 0)
    }
}
