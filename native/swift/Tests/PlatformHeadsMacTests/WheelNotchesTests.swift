import XCTest
@testable import PlatformHeadsMac

final class WheelNotchesTests: XCTestCase {
    /// One click of a wheel turned slowly reads as a tenth of a line, and is still a notch.
    func testAClickTurnedSlowlyIsANotch() {
        var wheel = WheelNotches()
        XCTAssertEqual(wheel.take(deltaY: 0.1, precise: false, pointsPerNotch: 16), 1)
        XCTAssertEqual(wheel.take(deltaY: -0.1, precise: false, pointsPerNotch: 16), -1)
    }

    /// Turned fast, a click says more than one line, and is that many.
    func testAClickTurnedFastIsWhatItSays() {
        var wheel = WheelNotches()
        XCTAssertEqual(wheel.take(deltaY: 3.4, precise: false, pointsPerNotch: 16), 3)
    }

    /// A trackpad's points add up between events rather than each rounding to nothing.
    func testATrackpadsPointsAddUp() {
        var wheel = WheelNotches()
        XCTAssertEqual(wheel.take(deltaY: 10, precise: true, pointsPerNotch: 16), 0)
        XCTAssertEqual(wheel.take(deltaY: 10, precise: true, pointsPerNotch: 16), 1)
        XCTAssertEqual(wheel.take(deltaY: 12, precise: true, pointsPerNotch: 16), 1)
    }

    /// A flick of tens of points is a few notches, not tens.
    func testAFlickIsAFewNotches() {
        var wheel = WheelNotches()
        XCTAssertEqual(wheel.take(deltaY: 48, precise: true, pointsPerNotch: 16), 3)
    }

    func testSidewaysIsNothing() {
        var wheel = WheelNotches()
        XCTAssertEqual(wheel.take(deltaY: 0, precise: false, pointsPerNotch: 16), 0)
    }

    func testStepsKeepTheirFraction() {
        var steps = Steps()
        XCTAssertEqual(steps.add(0.6), 0)
        XCTAssertEqual(steps.add(0.6), 1)
        XCTAssertEqual(steps.add(-0.3), 0)
        XCTAssertEqual(steps.add(-1.0), -1)
    }
}
