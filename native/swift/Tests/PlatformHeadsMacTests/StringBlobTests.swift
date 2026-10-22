import XCTest
@testable import PlatformHeadsMac

final class StringBlobTests: XCTestCase {
    private let bytes = Array("héllo world".utf8)

    func testASliceIsItsText() {
        bytes.withUnsafeBufferPointer {
            let blob = StringBlob($0.baseAddress, length: Int32($0.count))
            XCTAssertEqual(blob.text(offset: 0, length: 6), "héllo")
            XCTAssertEqual(blob.text(offset: 7, length: 5), "world")
        }
    }

    /// Past the end, before the start or empty is no text, never a read outside the blob.
    func testABadSliceIsEmpty() {
        bytes.withUnsafeBufferPointer {
            let blob = StringBlob($0.baseAddress, length: Int32($0.count))
            XCTAssertEqual(blob.text(offset: 7, length: 6), "")
            XCTAssertEqual(blob.text(offset: -1, length: 2), "")
            XCTAssertEqual(blob.text(offset: 0, length: 0), "")
            XCTAssertEqual(StringBlob(nil, length: 10).text(offset: 0, length: 2), "")
        }
    }

    /// Two values that are each in range add up past Int32.max, which trapped where they were
    /// added in thirty two bits.
    func testAnOffsetAndLengthThatOverflowThirtyTwoBitsAreEmpty() {
        bytes.withUnsafeBufferPointer {
            let blob = StringBlob($0.baseAddress, length: Int32($0.count))
            XCTAssertEqual(blob.text(offset: Int32.max - 1, length: Int32.max - 1), "")
        }
    }
}
