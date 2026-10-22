import AppKit

/// Where a scroller's knob is and what a press on it asks for, counted in rows, so its travel is
/// exactly the clamp the managed side applies to its scroll top.
///
/// A head has no `NSScrollView`: the managed side owns the scroll position and hands over a
/// screenful of rows at a time, so the scroller is told where the rows are rather than finding out.
public struct ScrollerTravel: Equatable {
    /// The first row on screen.
    public let top: Int

    /// The rows on screen, never under one.
    public let visible: Int

    /// The rows there are, never fewer than are on screen.
    public let total: Int

    public init(top: Int, visible: Int, total: Int) {
        self.top = top
        self.visible = max(1, visible)
        self.total = max(total, self.visible)
    }

    /// The furthest the top can go.
    public var maximum: Int {
        total - visible
    }

    public var knobProportion: CGFloat {
        CGFloat(visible) / CGFloat(total)
    }

    /// Where the knob sits, as the fraction of its travel `doubleValue` is.
    public var value: Double {
        maximum <= 0 ? 0 : Double(top) / Double(maximum)
    }

    /// Puts the knob where the rows on screen are.
    public func apply(to scroller: NSScroller) {
        scroller.knobProportion = knobProportion
        scroller.doubleValue = value
    }

    /// The top that `value` of the way along the travel is.
    public func row(at value: Double) -> Int32 {
        Int32((min(max(value, 0), 1) * Double(maximum)).rounded())
    }

    /// The top a press on `part` asks for: a page or a row either way, and anywhere else the place
    /// the knob was taken to, which is `value`.
    public func row(for part: NSScroller.Part, value: Double) -> Int32 {
        switch part {
        case .decrementPage:
            return Int32(max(0, top - visible))
        case .incrementPage:
            return Int32(min(maximum, top + visible))
        case .decrementLine:
            return Int32(max(0, top - 1))
        case .incrementLine:
            return Int32(min(maximum, top + 1))
        default:
            return row(at: value)
        }
    }
}
