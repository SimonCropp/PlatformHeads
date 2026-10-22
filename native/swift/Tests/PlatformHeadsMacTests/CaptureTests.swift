import CoreGraphics
import ImageIO
import XCTest
@testable import PlatformHeadsMac

final class CaptureTests: XCTestCase {
    /// At the size asked for, whatever the display: no backing scale comes into it.
    func testAPngIsWrittenAtTheSizeAskedFor() throws {
        let path = NSTemporaryDirectory() + "PlatformHeadsCapture-\(UUID().uuidString).png"
        defer {
            try? FileManager.default.removeItem(atPath: path)
        }

        let written = Capture.png(width: 30, height: 20, path: path) { context in
            context.setFillColor(CGColor(red: 1, green: 0, blue: 0, alpha: 1))
            context.fill(CGRect(x: 0, y: 0, width: 30, height: 20))
        }

        XCTAssertTrue(written)
        let source = try XCTUnwrap(CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil))
        let image = try XCTUnwrap(CGImageSourceCreateImageAtIndex(source, 0, nil))
        XCTAssertEqual(image.width, 30)
        XCTAssertEqual(image.height, 20)
    }

    func testNothingIsWrittenForAnEmptySize() {
        XCTAssertFalse(Capture.png(width: 0, height: 20, path: NSTemporaryDirectory() + "never.png") { _ in })
    }
}
