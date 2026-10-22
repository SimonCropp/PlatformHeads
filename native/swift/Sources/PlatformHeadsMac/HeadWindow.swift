import AppKit

/// The application and the window a head draws in, made the way every head has to make them when
/// the loop is the managed side's and `NSApplication.run` is never called.
///
/// All of it on the main thread, which is the managed side's main thread: AppKit requires it. A
/// head that only captures never calls any of it, which is what lets the pixel tests run on
/// whatever thread a test host picks.
public enum HeadWindow {
    /// Regular rather than accessory, so the window can take focus and be in the Dock without the
    /// app being a bundle; the menu bar, before `finishLaunching`, which is when it is first read;
    /// and `finishLaunching`, the part of `run` that has to happen before events are pumped by
    /// hand. The delegate is held weakly by AppKit, so the head keeps it.
    public static func launch(menu: NSMenu, delegate: NSApplicationDelegate) {
        let application = NSApplication.shared
        application.setActivationPolicy(.regular)
        application.mainMenu = menu
        application.delegate = delegate
        application.finishLaunching()
    }

    /// A titled, closable, resizable window around `view`, centred. Not released when closed,
    /// since a head hides on close as often as it closes, and the window is the head's to drop.
    public static func make(view: NSView, size: CGSize, title: String, delegate: NSWindowDelegate) -> NSWindow {
        let window = NSWindow(
            contentRect: NSRect(origin: .zero, size: size),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false)
        window.title = title
        window.contentView = view
        window.delegate = delegate
        window.isReleasedWhenClosed = false
        window.center()
        return window
    }

    /// Brings the window forward and the app with it. An app that is not frontmost just because
    /// one of its windows was ordered front leaves that window behind whatever is.
    public static func show(_ window: NSWindow?) {
        window?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    /// Takes the window off the screen and forgets it was a delegate's, before the head drops it.
    public static func close(_ window: NSWindow?) {
        window?.delegate = nil
        window?.orderOut(nil)
        window?.close()
    }
}
