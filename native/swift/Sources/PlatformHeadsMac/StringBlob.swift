/// The text at an offset into an app's string blob, the one buffer a screen's strings are handed
/// across the ABI in.
///
/// Every offset is checked against the blob rather than trusted: this is the boundary the managed
/// side reaches across, and a bad length is a read past the end of it. Added as `Int` and not in
/// thirty two bits, where an offset and a length that are each fine can add up past `Int32.max`
/// and trap.
public struct StringBlob {
    private let base: UnsafePointer<UInt8>?
    private let count: Int

    public init(_ base: UnsafePointer<UInt8>?, length: Int32) {
        self.base = base
        count = max(0, Int(length))
    }

    /// Empty for an empty or out of bounds slice.
    public func text(offset: Int32, length: Int32) -> String {
        guard length > 0,
              offset >= 0,
              let base,
              Int(offset) + Int(length) <= count
        else {
            return ""
        }

        let slice = UnsafeBufferPointer(start: base + Int(offset), count: Int(length))
        return String(decoding: slice, as: UTF8.self)
    }
}
