import AppKit

/// Answers a quit that comes from outside the managed loop: Quit in the Dock, and the end of the
/// session, which is a logout, a restart or a shutdown.
///
/// Both arrive as `terminate:`, which does not return. With no delegate to ask, AppKit exits from
/// inside it, so nothing after the pump call runs, the managed `finally` included, and whatever the
/// app saves on its way out is lost.
///
/// A quit from the Dock is refused and handed to `terminating`, which reports it the way the app's
/// own quit is reported, so the managed side leaves its loop, saves and exits. That is what GLFW
/// does. Not `.terminateLater`, whose reply AppKit waits for in a modal loop inside the pump, while
/// the thread that would send the reply is the one blocked waiting for the pump to return.
///
/// The end of a session cannot be answered that way. A refused quit cancels the logout, restart or
/// shutdown that asked for it: the person has to ask again once the app has gone, and a restart
/// nobody is there to ask for, such as a system update's overnight, does not happen. So given
/// `sessionEnding`, the guard calls it there and then, on the main thread from inside the pump, and
/// lets AppKit end the process when it returns. What the app does in it is all it gets to do, as
/// with `WM_ENDSESSION` on Windows and `ph::OnDisplayLost` on Linux. Without one, a session's end
/// is refused as a quit is, which is how every app had it before.
///
/// A session is ending when the workspace has said it is about to power off, which it posts for a
/// logout too, or when the quit event gives that as its reason (`kAEQuitReason`). The first is
/// documented and the second is what loginwindow sends; either is enough. The workspace never says
/// that a logout was cancelled, so a quit from the Dock after another app cancelled one is taken
/// for a session ending as well: the app does what it does at a logout and is ended, rather than
/// leaving its loop.
public final class TerminateGuard: NSObject, NSApplicationDelegate {
    private let terminating: () -> Void
    private let sessionEnding: (() -> Void)?
    private var poweringOff = false
    private var observer: NSObjectProtocol?

    /// The quit reasons that are the session ending. `kAEShowRestartDialog` and the other dialog
    /// reasons are asks of loginwindow, never the reason an app is told to quit for.
    static let sessionReasons: Set<OSType> = [
        OSType(kAELogOut),
        OSType(kAEReallyLogOut),
        OSType(kAERestart),
        OSType(kAEShutDown)
    ]

    public init(_ terminating: @escaping () -> Void, sessionEnding: (() -> Void)? = nil) {
        self.terminating = terminating
        self.sessionEnding = sessionEnding
        super.init()
        // Not observed without a sessionEnding to call, since nothing would change.
        if sessionEnding != nil {
            observer = NSWorkspace.shared.notificationCenter.addObserver(
                forName: NSWorkspace.willPowerOffNotification,
                object: nil,
                queue: nil) { [weak self] _ in
                    self?.poweringOff = true
                }
        }
    }

    deinit {
        if let observer {
            NSWorkspace.shared.notificationCenter.removeObserver(observer)
        }
    }

    public func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        reply(endsSession: poweringOff || Self.endsSession(NSAppleEventManager.shared().currentAppleEvent))
    }

    func reply(endsSession: Bool) -> NSApplication.TerminateReply {
        if endsSession, let sessionEnding {
            sessionEnding()
            return .terminateNow
        }

        terminating()
        return .terminateCancel
    }

    /// Whether a quit event says the session is ending. A quit from the Dock, or one with no event
    /// at all, gives no reason.
    static func endsSession(_ event: NSAppleEventDescriptor?) -> Bool {
        guard let reason = event?.attributeDescriptor(forKeyword: AEKeyword(kAEQuitReason)) else {
            return false
        }

        // Read both ways: a reason may be sent as a type or as an enumeration, and a descriptor
        // that does not coerce to the kind asked for reads as zero.
        return sessionReasons.contains(reason.enumCodeValue) || sessionReasons.contains(reason.typeCodeValue)
    }
}
