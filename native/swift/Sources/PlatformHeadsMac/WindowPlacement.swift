import AppKit

/// Where a window was left, in the units every head reports one in: from the top left of the
/// primary screen with y down, which is how the WinForms and raylib heads measure it and what the
/// managed side's `WindowPlacement` holds. AppKit measures from the bottom left with y up.
public struct WindowBounds: Equatable {
    public var x: Int32
    public var y: Int32
    public var width: Int32
    public var height: Int32

    public init(x: Int32, y: Int32, width: Int32, height: Int32) {
        self.x = x
        self.y = y
        self.width = width
        self.height = height
    }
}

/// A window's frame to `WindowBounds` and back. The primary screen's height is a parameter rather
/// than looked up, so a test can convert without a screen to look it up on.
///
/// Never maximised: AppKit's zoom is a size rather than a mode a window stays in, so a zoomed window
/// is kept as the frame it zoomed to.
public enum WindowPlacement {
    public static func frame(of bounds: WindowBounds, primaryHeight: CGFloat) -> NSRect {
        NSRect(
            x: CGFloat(bounds.x),
            y: primaryHeight - CGFloat(bounds.y) - CGFloat(bounds.height),
            width: CGFloat(bounds.width),
            height: CGFloat(bounds.height))
    }

    public static func bounds(of frame: NSRect, primaryHeight: CGFloat) -> WindowBounds {
        WindowBounds(
            x: Int32(frame.minX.rounded()),
            y: Int32((primaryHeight - frame.maxY).rounded()),
            width: Int32(frame.width.rounded()),
            height: Int32(frame.height.rounded()))
    }

    /// Whether enough of the title bar is on one of the screens to drag the window by. A display
    /// unplugged since the window was left there, or a laptop taken off its dock, would otherwise
    /// open it where it could be neither seen nor dragged back, which looks like an app that did
    /// not start.
    public static func reachable(_ frame: NSRect, screens: [NSRect]) -> Bool {
        let titleBar = NSRect(x: frame.minX, y: frame.maxY - 28, width: frame.width, height: 28)
        return screens.contains {
            let visible = $0.intersection(titleBar)
            return visible.width >= 100 && visible.height > 0
        }
    }

    /// Puts `window` where it was left, when that is still somewhere it can be reached, and says
    /// whether it did. A window it does not move is wherever it was, which for a new one is
    /// wherever the head centred it.
    @discardableResult
    public static func restore(_ window: NSWindow, to bounds: WindowBounds) -> Bool {
        guard bounds.width > 0, bounds.height > 0, let primary = NSScreen.screens.first else {
            return false
        }

        let target = WindowPlacement.frame(of: bounds, primaryHeight: primary.frame.height)
        guard reachable(target, screens: NSScreen.screens.map(\.visibleFrame)) else {
            return false
        }

        window.setFrame(target, display: false)
        return true
    }

    /// Where `window` is now, or nil when there is none, or it is full screen: that frame is the
    /// display's rather than one the user chose, and the one they did choose comes back when they
    /// leave full screen.
    public static func current(of window: NSWindow?) -> WindowBounds? {
        guard let window,
              !window.styleMask.contains(.fullScreen),
              let primary = NSScreen.screens.first
        else {
            return nil
        }

        return bounds(of: window.frame, primaryHeight: primary.frame.height)
    }
}
