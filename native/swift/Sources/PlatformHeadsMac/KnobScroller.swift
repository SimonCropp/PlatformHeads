import AppKit

/// A head's scrollbar, with a drag of its knob followed as ordinary events rather than in AppKit's
/// own loop.
///
/// `NSScroller` tracks a press on its knob in a loop of its own, inside `mouseDown`, and returns
/// when the button comes up. That is inside the pump, so a present did not return for the length
/// of the drag: the knob moved under the pointer, every move was reported, and the managed loop
/// that scrolls the rows in answer ran once, at the release. The WinForms head has the same loop
/// under its scroll bar and is handed frames from inside it, which a C ABI has no way to ask for.
///
/// So the drag is three ordinary events instead. The window sends the moves and the release to the
/// view that took the press, each comes through the pump on its own, and there is a frame between
/// one and the next. Only a press on the knob: one in the slot is still AppKit's, since what it
/// means is the user's setting, a page or the place that was pressed, and it is over in a click.
public final class KnobScroller: NSScroller {
    /// Told where a drag of the knob has got to, as the fraction of its travel `doubleValue` is.
    /// The knob is not moved: it goes where the frame that answers this puts it, which is the row
    /// the rows on screen are at.
    public var knobDragged: ((Double) -> Void)?

    /// A knob being dragged: where in the window the pointer took hold of it, where the knob was
    /// then, and how long its travel is.
    private var dragging = false
    private var heldAt: CGFloat = 0
    private var heldValue = 0.0
    private var travel: CGFloat = 0

    /// One against the right edge of `view` and as tall as it, sending `action` to `target`.
    ///
    /// Legacy rather than overlay, whatever the machine prefers. An overlay scroller only fades in
    /// and out as part of an `NSScrollView`, and placed by hand it would sit over the rows forever.
    /// Legacy takes a strip of its own, as the WinForms head's does, and for the same reason: a bar
    /// that comes and goes moves the layout about.
    ///
    /// Kept against the right edge and as tall as the window leaves it by AppKit, as well as by the
    /// head: a window being resized is a loop of AppKit's own, inside the pump, so the head does
    /// not place it again until the mouse comes up, and until then it stayed where the last frame
    /// had put it, out in the middle of a window being widened. And enabled, said rather than left
    /// to whatever a scroller starts as: a scroll view sees to that for its own scrollers, and one
    /// that is not enabled draws its slot with no knob in it to take hold of.
    public static func legacy(in view: NSView, target: AnyObject, action: Selector) -> KnobScroller {
        let width = NSScroller.scrollerWidth(for: .regular, scrollerStyle: .legacy)
        let scroller = KnobScroller(
            frame: NSRect(x: view.bounds.maxX - width, y: 0, width: width, height: view.bounds.height))
        scroller.scrollerStyle = .legacy
        scroller.autoresizingMask = [.minXMargin, .height]
        scroller.target = target
        scroller.action = action
        scroller.isEnabled = true
        view.addSubview(scroller)
        return scroller
    }

    public override func mouseDown(with event: NSEvent) {
        let knob = rect(for: .knob)
        let room = rect(for: .knobSlot).height - knob.height
        guard room > 0, knobDragged != nil, knob.contains(convert(event.locationInWindow, from: nil)) else {
            super.mouseDown(with: event)
            return
        }

        dragging = true
        heldAt = event.locationInWindow.y
        heldValue = doubleValue
        travel = room
    }

    public override func mouseDragged(with event: NSEvent) {
        guard dragging else {
            super.mouseDragged(with: event)
            return
        }

        // Measured from the press rather than from the last move, and in the window's coordinates,
        // which count up the screen while the rows run down it.
        let moved = Double((heldAt - event.locationInWindow.y) / travel)
        knobDragged?(min(max(heldValue + moved, 0), 1))
    }

    public override func mouseUp(with event: NSEvent) {
        guard dragging else {
            super.mouseUp(with: event)
            return
        }

        // Nothing to report: the last move already said where it is.
        dragging = false
    }
}
