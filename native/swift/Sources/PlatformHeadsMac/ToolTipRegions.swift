import AppKit

/// A view's tool tips, one region a text, registered again only when they change.
///
/// AppKit times a tip's delay from the moment the cursor enters its region, and a head draws far
/// more often than its tips change: removing and adding the region under a resting cursor every
/// frame restarted that delay before it could ever elapse, so the tips never appeared at all.
///
/// A region whose text is empty gets none, rather than one answering with an empty string, which
/// would still open a popup, and a popup that says nothing is worse than none. Called from the
/// present after drawing rather than from `draw`, since these are tracking regions and changing
/// them while AppKit is drawing invites re-entrancy.
public final class ToolTipRegions {
    private var rects: [NSRect] = []
    private var texts: [String] = []

    /// The strings the regions read, held for as long as the regions stand, because AppKit does
    /// not retain a tip's owner: a string bridged at the call is freed the moment it returns, and
    /// the tip reads a dead object.
    private var owners: [NSString] = []

    public init() {
    }

    public func apply(_ tips: [(rect: NSRect, text: String)], to view: NSView) {
        let wanted = tips.filter { !$0.text.isEmpty }
        let wantedRects = wanted.map { $0.rect }
        let wantedTexts = wanted.map { $0.text }
        guard wantedRects != rects || wantedTexts != texts else {
            return
        }

        rects = wantedRects
        texts = wantedTexts
        view.removeAllToolTips()
        owners = wantedTexts.map { $0 as NSString }
        for (rect, owner) in zip(rects, owners) {
            _ = view.addToolTip(rect, owner: owner, userData: nil)
        }
    }
}
