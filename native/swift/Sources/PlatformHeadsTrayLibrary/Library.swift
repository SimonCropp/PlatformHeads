// A library made the way an app makes its tray's: a target of its own that does nothing but depend
// on PlatformHeadsMacTray, whose exports SwiftPM then links into it. So the macOS job's tests,
// which load it, find out whether a dependency's @_cdecl functions come out of an app's library,
// before an app takes the tag to find out.
import PlatformHeadsMacTray
