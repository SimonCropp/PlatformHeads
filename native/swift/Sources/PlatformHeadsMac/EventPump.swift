import AppKit
import Foundation

/// The pump a head runs at the end of every present, which is both how AppKit's events are
/// dispatched and what holds the managed loop to a frame a turn. The loop stays in C# rather than
/// being inverted into `NSApplication.run`, so nothing else dispatches them, and without the wait
/// the managed loop, which calls straight back in, would spin a core.
public enum EventPump {
    /// How long a present waits for an event while the window can be seen.
    public static let frameSeconds: TimeInterval = 1.0 / 60.0

    /// How long one waits while nobody can see the window: ten turns of the managed loop a second,
    /// and a tenth of a second at most before a window the managed side has been asked for, by a
    /// message it acts on between two presents and has no way to interrupt this with, comes
    /// forward. An event still ends the wait at once, and a click on the Dock icon is one.
    public static let unseenWait: TimeInterval = 0.1

    /// Dispatches what AppKit has queued, then waits for more until the frame is up.
    ///
    /// Not blocked while `hasInput` says something is waiting for the next poll, which is the next
    /// frame's, now: a head that hands its keys over one a poll would otherwise hand a held key's
    /// repeats over more slowly than a fast repeat rate makes them. Nor once an event has left
    /// something to hand over: the wait used to run to its deadline whatever arrived during it, so
    /// a key pressed a millisecond into a frame was answered a frame late. What else is already
    /// queued is still dispatched, without waiting for more.
    public static func pump(unseen: Bool, hasInput: () -> Bool) {
        let wait = unseen ? unseenWait : frameSeconds
        var deadline = hasInput() ? Date.distantPast : Date(timeIntervalSinceNow: wait)
        while let event = NSApp.nextEvent(matching: .any, until: deadline, inMode: .default, dequeue: true) {
            NSApp.sendEvent(event)
            if hasInput() {
                deadline = Date.distantPast
            }
        }
    }

    /// Nobody can see the window: ordered out behind a tray, in the Dock, or wholly covered. The
    /// managed loop turns only as fast as the pump returns, and it was turning sixty times a second
    /// to present to nothing. False with no window, which is a head that only captures.
    public static func isUnseen(_ window: NSWindow?) -> Bool {
        guard let window else {
            return false
        }

        return !window.isVisible || window.isMiniaturized || !window.occlusionState.contains(.visible)
    }
}
