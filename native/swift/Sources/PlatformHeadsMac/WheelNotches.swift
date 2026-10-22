import AppKit

/// A scroll event as notches of a wheel, up the screen positive, as AppKit has it.
///
/// AppKit reports a wheel in lines and a trackpad in points, and treating both as the same thing
/// failed both ways. An ordinary flick of a trackpad reads as tens of points, so it arrived as tens
/// of notches; and a wheel's delta is scaled by how fast it turns, which for one click turned
/// slowly is a tenth of a line, so added up as a trackpad's are, ten clicks went by before anything
/// moved.
public struct WheelNotches {
    private var precise = Steps()

    public init() {
    }

    /// `pointsPerNotch` is what a precise device's movement of one notch is taken to be, which is
    /// a head's row height or near it.
    public mutating func take(_ event: NSEvent, pointsPerNotch: Double) -> Int32 {
        take(
            deltaY: Double(event.scrollingDeltaY),
            precise: event.hasPreciseScrollingDeltas,
            pointsPerNotch: pointsPerNotch)
    }

    public mutating func take(deltaY: Double, precise isPrecise: Bool, pointsPerNotch: Double) -> Int32 {
        if isPrecise {
            return precise.add(deltaY / max(pointsPerNotch, 1))
        }

        // A wheel with notches: an event is a click of it. So at least one notch the way it
        // turned, and more only when it says more. A trackpad's fraction is not finished by it.
        precise.reset()
        let lines = deltaY.rounded()
        if deltaY > 0 {
            return Int32(max(1, lines))
        }

        if deltaY < 0 {
            return Int32(min(-1, lines))
        }

        // Turned sideways, which nothing here answers
        return 0
    }
}
