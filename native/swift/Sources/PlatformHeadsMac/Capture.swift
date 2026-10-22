import CoreGraphics
import Foundation
import ImageIO

/// A frame drawn into a bitmap of the head's own making and written as a png, which is what a
/// head's capture export does for the pixel snapshots.
///
/// Not the view's own bitmap: `bitmapImageRepForCachingDisplay` inherits the window's backing
/// scale, which is 2 on a Retina display and 1 elsewhere, so a committed baseline would only ever
/// match on the kind of display that made it. Everything that varies is pinned here instead: the
/// scale, the colour space, and the six font smoothing and subpixel switches. No window is needed,
/// so neither is a window server, and a capture can run on whichever thread a test host likes.
public enum Capture {
    /// Draws through `draw`, whose context is unflipped: a head that lays out from the top flips it
    /// itself. False when the bitmap could not be made or the file not written.
    public static func png(width: Int32, height: Int32, path: String, draw: (CGContext) -> Void) -> Bool {
        guard width > 0,
              height > 0,
              let space = CGColorSpace(name: CGColorSpace.sRGB),
              let context = CGContext(
                  data: nil,
                  width: Int(width),
                  height: Int(height),
                  bitsPerComponent: 8,
                  bytesPerRow: 0,
                  space: space,
                  bitmapInfo: CGImageAlphaInfo.premultipliedFirst.rawValue | CGBitmapInfo.byteOrder32Little.rawValue)
        else {
            return false
        }

        context.setAllowsFontSmoothing(false)
        context.setShouldSmoothFonts(false)
        context.setAllowsFontSubpixelPositioning(false)
        context.setShouldSubpixelPositionFonts(false)
        context.setAllowsFontSubpixelQuantization(false)
        context.setShouldSubpixelQuantizeFonts(false)
        draw(context)

        guard let image = context.makeImage() else {
            return false
        }

        let url = URL(fileURLWithPath: path) as CFURL
        guard let destination = CGImageDestinationCreateWithURL(url, "public.png" as CFString, 1, nil) else {
            return false
        }

        CGImageDestinationAddImage(destination, image, nil)
        return CGImageDestinationFinalize(destination)
    }
}
