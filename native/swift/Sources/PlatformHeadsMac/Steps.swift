/// Whole steps out of amounts that come a fraction at a time, keeping the fraction: a trackpad
/// sends many small deltas between two polls, and dropping each one's fraction on its own is what
/// made slow movement do nothing.
public struct Steps {
    private var remainder = 0.0

    public init() {
    }

    /// The whole steps `amount` completes, towards zero, with what is left kept for the next.
    public mutating func add(_ amount: Double) -> Int32 {
        remainder += amount
        let steps = remainder.rounded(.towardZero)
        remainder -= steps
        return Int32(steps)
    }

    /// Forgets the fraction, for movement of another kind that should not finish this one's step.
    public mutating func reset() {
        remainder = 0
    }
}
