import AppKit
import CPlatformHeadsTray
import PlatformHeadsMac

/// The application around a tray that is all an app shows: an accessory, with no Dock icon and no
/// menu bar of its own, which a tray has no use for. Not `HeadWindow.launch`, which makes a regular
/// app so its window can take focus. An app with a window has its own application and its own
/// `TerminateGuard`, and never starts this.
final class AccessoryApp {
    static let shared = AccessoryApp()

    private let napGuard = NapGuard()
    private(set) var quitting = false

    /// What `ph_tray_app_on_session_end` was handed.
    var sessionEnded: PhTraySessionEnd?

    /// A quit sent to the app, which has no Dock icon to quit from, turned into the managed side's
    /// own quit. A logout, restart or shutdown runs `sessionEnded` there and then instead, and
    /// AppKit ends the process once it returns. Made in `start`, from whatever `sessionEnded` is by
    /// then: with none, a session's end is refused as a quit is, which cancels the logout but loses
    /// nothing. Held here: AppKit keeps its delegate weakly.
    private var terminateGuard: TerminateGuard?

    private init() {
    }

    /// `finishLaunching` is the part of `run` that has to happen before events are pumped by hand.
    /// App Nap is kept off: napped, a process's timers are coalesced, and the managed side's sockets
    /// beside this answered late.
    func start(napReason: String) {
        let application = NSApplication.shared
        application.setActivationPolicy(.accessory)
        let terminate = TerminateGuard(
            { [unowned self] in
                self.quitting = true
            },
            sessionEnding: sessionEnded.map { ended -> () -> Void in { ended() } })
        terminateGuard = terminate
        application.delegate = terminate
        application.finishLaunching()
        napGuard.begin(reason: napReason)
    }

    /// A tenth of a second at most, which is what a tray's managed loop turns on, and less when an
    /// item has been chosen, the icon clicked or a quit asked for.
    func pump() {
        EventPump.pump(unseen: true) { StatusTray.shared.hasInput || quitting }
    }

    func stop() {
        napGuard.end()
    }
}
