import AppKit
import CPlatformHeadsTray
import Foundation
import PlatformHeadsMac

/// The entry points of native/swift/include/platformheads_tray.h, over `StatusTray` and
/// `AccessoryApp`. Public, and so exported from whichever library of the app's links this target.
///
/// The header is imported for its struct and constants only, with PH_TRAY_TYPES_ONLY, so these are
/// the definitions of those symbols rather than a second declaration of them. Each one that touches
/// AppKit runs inside its own autorelease pool: nothing here calls `NSApplication.run`, which would
/// drain one an event.

@_cdecl("ph_tray_version")
public func phTrayVersion() -> Int32 {
    Int32(PH_TRAY_VERSION)
}

@_cdecl("ph_tray_init")
public func phTrayInit() -> Int32 {
    autoreleasepool {
        StatusTray.shared.item.start() ? 1 : 0
    }
}

@_cdecl("ph_tray_set_icon")
public func phTraySetIcon(_ icon: Int32, _ png: UnsafePointer<UInt8>?, _ length: Int32) {
    guard let png, length > 0 else {
        return
    }

    autoreleasepool {
        StatusTray.shared.item.setIcon(icon, png: Data(bytes: png, count: Int(length)))
    }
}

@_cdecl("ph_tray_set_menu_icon")
public func phTraySetMenuIcon(_ name: UnsafePointer<CChar>?, _ png: UnsafePointer<UInt8>?, _ length: Int32) {
    guard let name, let png, length > 0 else {
        return
    }

    autoreleasepool {
        StatusTray.shared.item.setMenuIcon(String(cString: name), png: Data(bytes: png, count: Int(length)))
    }
}

@_cdecl("ph_tray_apply")
public func phTrayApply(_ icon: Int32, _ tooltip: UnsafePointer<CChar>?, _ items: UnsafePointer<PhTrayItem>?, _ count: Int32) {
    autoreleasepool {
        StatusTray.shared.item.apply(
            icon: icon,
            tooltip: tooltip.map { String(cString: $0) } ?? "",
            items: TrayItems.read(items, count))
    }
}

@_cdecl("ph_tray_poll")
public func phTrayPoll(_ buffer: UnsafeMutablePointer<CChar>?, _ capacity: Int32) -> Int32 {
    guard let chosen = StatusTray.shared.takeChosen() else {
        return 0
    }

    return Utf8Buffer.write(chosen, buffer, capacity)
}

@_cdecl("ph_tray_icon_clicked")
public func phTrayIconClicked() -> Int32 {
    StatusTray.shared.takeIconClicked() ? 1 : 0
}

@_cdecl("ph_tray_describe")
public func phTrayDescribe(_ buffer: UnsafeMutablePointer<CChar>?, _ capacity: Int32) -> Int32 {
    Utf8Buffer.write(StatusMenuItem.describe(StatusTray.shared.item.items), buffer, capacity)
}

@_cdecl("ph_tray_shutdown")
public func phTrayShutdown() {
    autoreleasepool {
        StatusTray.shared.item.stop()
    }
}

@_cdecl("ph_tray_app_on_session_end")
public func phTrayAppOnSessionEnd(_ ended: PhTraySessionEnd?) {
    AccessoryApp.shared.sessionEnded = ended
}

@_cdecl("ph_tray_app_start")
public func phTrayAppStart(_ napReason: UnsafePointer<CChar>?) {
    autoreleasepool {
        AccessoryApp.shared.start(napReason: napReason.map { String(cString: $0) } ?? "A tray")
    }
}

@_cdecl("ph_tray_app_pump")
public func phTrayAppPump() {
    autoreleasepool {
        AccessoryApp.shared.pump()
    }
}

@_cdecl("ph_tray_app_quitting")
public func phTrayAppQuitting() -> Int32 {
    AccessoryApp.shared.quitting ? 1 : 0
}

@_cdecl("ph_tray_app_stop")
public func phTrayAppStop() {
    autoreleasepool {
        AccessoryApp.shared.stop()
    }
}
