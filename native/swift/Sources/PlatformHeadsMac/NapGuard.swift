import Foundation

/// Keeps App Nap off for as long as a head is open.
///
/// A napped process has its timers coalesced and its priority lowered, and a head is a loop driven
/// from outside, waiting on `nextEvent(until:)` a frame at a time, with sockets and timers of the
/// managed side beside it: covered, it was slow to answer them and slower to bring its window
/// forward. Idle sleep stays allowed. A head redraws only on a change, so staying awake costs a
/// pump.
public final class NapGuard {
    private var activity: NSObjectProtocol?

    public init() {
    }

    /// Once: a second call while the first stands does nothing.
    public func begin(reason: String) {
        guard activity == nil else {
            return
        }

        activity = ProcessInfo.processInfo.beginActivity(
            options: .userInitiatedAllowingIdleSystemSleep,
            reason: reason)
    }

    public func end() {
        guard let activity else {
            return
        }

        ProcessInfo.processInfo.endActivity(activity)
        self.activity = nil
    }
}
