import AppKit
import XCTest
@testable import PlatformHeadsMac

/// What a quit from outside the managed loop is answered with. Nothing here quits: the guard is
/// asked as AppKit would ask it, and the answer is read.
final class TerminateGuardTests: XCTestCase {
    private var reported: [String] = []

    private func guarded(sessionEnding: Bool = true) -> TerminateGuard {
        if sessionEnding {
            return TerminateGuard({ self.reported.append("terminating") }) {
                self.reported.append("sessionEnding")
            }
        }

        return TerminateGuard { self.reported.append("terminating") }
    }

    /// Quit in the Dock: refused, and reported to the managed side as its own quit.
    func testAQuitIsRefusedAndReported() {
        let terminate = guarded()

        XCTAssertEqual(terminate.reply(endsSession: false), .terminateCancel)
        XCTAssertEqual(reported, ["terminating"])
    }

    /// A logout, restart or shutdown: the app's session end is done there and then, and the quit is
    /// let through, since refusing it cancels the logout.
    func testASessionEndingIsDoneAndLetThrough() {
        let terminate = guarded()

        XCTAssertEqual(terminate.reply(endsSession: true), .terminateNow)
        XCTAssertEqual(reported, ["sessionEnding"])
    }

    /// An app that gives no sessionEnding keeps what it had.
    func testWithNoSessionEndingASessionEndingIsRefusedAsAQuitIs() {
        let terminate = guarded(sessionEnding: false)

        XCTAssertEqual(terminate.reply(endsSession: true), .terminateCancel)
        XCTAssertEqual(reported, ["terminating"])
    }

    /// The workspace says it is about to power off before any app is asked to quit, for a logout
    /// too, and a quit after that is the session's end. Asked with no current event, as in a test
    /// process, so the notification is the only thing that can say so.
    func testAQuitAfterTheWorkspaceSaysItIsPoweringOffEndsTheSession() {
        let terminate = guarded()
        XCTAssertNil(NSAppleEventManager.shared().currentAppleEvent)

        NSWorkspace.shared.notificationCenter.post(name: NSWorkspace.willPowerOffNotification, object: nil)

        XCTAssertEqual(terminate.applicationShouldTerminate(NSApplication.shared), .terminateNow)
        XCTAssertEqual(reported, ["sessionEnding"])
    }

    func testAQuitWithNothingSaidIsAQuit() {
        let terminate = guarded()

        XCTAssertEqual(terminate.applicationShouldTerminate(NSApplication.shared), .terminateCancel)
        XCTAssertEqual(reported, ["terminating"])
    }

    /// Each reason a session ends for, given either way a descriptor can carry it.
    func testTheQuitReasonsThatEndASession() {
        for reason in TerminateGuard.sessionReasons {
            XCTAssertTrue(TerminateGuard.endsSession(quit(NSAppleEventDescriptor(enumCode: reason))))
            XCTAssertTrue(TerminateGuard.endsSession(quit(NSAppleEventDescriptor(typeCode: reason))))
        }
    }

    /// A quit from the Dock gives no reason, and the dialog reasons are asks of loginwindow.
    func testOtherQuitsDoNotEndTheSession() {
        XCTAssertFalse(TerminateGuard.endsSession(nil))
        XCTAssertFalse(TerminateGuard.endsSession(quit(nil)))
        XCTAssertFalse(TerminateGuard.endsSession(quit(NSAppleEventDescriptor(enumCode: OSType(kAEShowRestartDialog)))))
    }

    /// A quit event, as an app is sent one, with the reason given as loginwindow gives it.
    private func quit(_ reason: NSAppleEventDescriptor?) -> NSAppleEventDescriptor {
        let event = NSAppleEventDescriptor(
            eventClass: AEEventClass(kCoreEventClass),
            eventID: AEEventID(kAEQuitApplication),
            targetDescriptor: nil,
            returnID: AEReturnID(kAutoGenerateReturnID),
            transactionID: AETransactionID(kAnyTransactionID))
        if let reason {
            event.setAttribute(reason, forKeyword: AEKeyword(kAEQuitReason))
        }

        return event
    }
}
