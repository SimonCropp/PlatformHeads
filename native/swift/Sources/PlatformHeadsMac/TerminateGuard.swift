import AppKit

/// Answers a quit that comes from outside the managed loop: Quit in the Dock, and logout.
///
/// Both arrive as `terminate:`, which does not return. With no delegate to ask, AppKit exits from
/// inside it, so nothing after the pump call runs, the managed `finally` included, and whatever the
/// app saves on its way out is lost.
///
/// The quit is refused and handed to `terminating`, which reports it the way the app's own quit
/// is reported, so the managed side leaves its loop, saves and exits. That is what GLFW does. Not
/// `.terminateLater`, whose reply AppKit waits for in a modal loop inside the pump, while the
/// thread that would send the reply is the one blocked waiting for the pump to return.
///
/// A refused quit also cancels the logout that asked for it. By the time the logout is tried again
/// the app has exited.
public final class TerminateGuard: NSObject, NSApplicationDelegate {
    private let terminating: () -> Void

    public init(_ terminating: @escaping () -> Void) {
        self.terminating = terminating
    }

    public func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        terminating()
        return .terminateCancel
    }
}
