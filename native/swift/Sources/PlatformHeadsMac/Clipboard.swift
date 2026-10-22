import AppKit

public enum Clipboard {
    /// Cleared first: `NSPasteboard` keeps whatever types were declared before, so a string written
    /// over an image left both on the board, and the image was what pasted.
    public static func set(_ text: String) {
        let board = NSPasteboard.general
        board.clearContents()
        board.setString(text, forType: .string)
    }
}
