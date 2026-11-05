/*
 * The macOS tray's ABI: PlatformHeadsMac's StatusItem in the menu bar, and for an app that has no
 * window of its own, the application around it. Defined by the PlatformHeadsMacTray Swift target,
 * which an app links into a library of its own, and called by PlatformHeads.Mac's StatusItemTray
 * and StatusItemApp, whose P/Invokes name platformheads_tray and are resolved to whichever library
 * the app says it linked it into.
 *
 * Bump PH_TRAY_VERSION whenever a struct changes or a field changes meaning: the managed side
 * refuses a library whose version is not the one it was built for.
 *
 * Every call that touches AppKit is the main thread's to make, as AppKit requires.
 */
#ifndef PLATFORMHEADS_TRAY_H
#define PLATFORMHEADS_TRAY_H

#include <stdint.h>

#define PH_TRAY_API __attribute__((visibility("default")))

#ifdef __cplusplus
extern "C" {
#endif

/*
 * 1: the status item and its menu, and the accessory application. Was DiffEngineTray's detray.h
 *    at its version 2, and the tray half of BuildMonitor's bm.h.
 */
#define PH_TRAY_VERSION 1

/* What ph_tray_app_on_session_end calls. */
typedef void (*PhTraySessionEnd)(void);

/* PlatformHeads' TrayMenuFlags and PlatformHeadsMac's StatusMenuFlags, which write and read them. */
#define PH_TRAY_ENABLED 1
#define PH_TRAY_SEPARATOR 2
/* A toggle, drawn with a check when PH_TRAY_CHECKED is set as well. */
#define PH_TRAY_CHECKABLE 4
#define PH_TRAY_CHECKED 8

/*
 * One menu item. The menu is one array, each submenu's items after the item that opens it, and
 * parent is the index of that item in the same array, or -1 for an item of the menu itself. The
 * strings are UTF-8, and read only during the call they are passed to. icon names a glyph handed
 * over by ph_tray_set_menu_icon, or is empty.
 */
typedef struct PhTrayItem {
    const char* id;
    const char* label;
    const char* icon;
    int32_t flags;
    int32_t parent;
} PhTrayItem;

#ifndef PH_TRAY_TYPES_ONLY

PH_TRAY_API int32_t ph_tray_version(void);

/*
 * The item in the status bar. 1 on success, 0 when AppKit gave it no button to draw in. Makes no
 * application of its own: an app with a window has one, and an app without calls
 * ph_tray_app_start first.
 */
PH_TRAY_API int32_t ph_tray_init(void);

/* An icon's picture, by the value of the app's managed icon enum. A PNG, drawn at the menu bar's height. */
PH_TRAY_API void ph_tray_set_icon(int32_t icon, const uint8_t* png, int32_t length);

/* A glyph a menu item can name, drawn at 16 points. A second call for a name replaces it. */
PH_TRAY_API void ph_tray_set_menu_icon(const char* name, const uint8_t* png, int32_t length);

/* What the item shows: which icon, its tooltip, and the menu built when it is next opened. */
PH_TRAY_API void ph_tray_apply(int32_t icon, const char* tooltip, const PhTrayItem* items, int32_t count);

/*
 * The id of an item chosen since the last poll, written to buffer, its whole length in bytes
 * returned, or 0 for none. Chosen items are handed over one a poll, in the order they were chosen.
 */
PH_TRAY_API int32_t ph_tray_poll(char* buffer, int32_t capacity);

/* 1 when the icon itself was clicked with the left button since the last call, which clears it. */
PH_TRAY_API int32_t ph_tray_icon_clicked(void);

/*
 * The menu as the last apply left it, a line an item, as text, written to buffer with its whole
 * length returned. For tests, which can make no status item: neither this nor ph_tray_apply needs
 * ph_tray_init.
 */
PH_TRAY_API int32_t ph_tray_describe(char* buffer, int32_t capacity);

/* Takes the item out of the status bar. */
PH_TRAY_API void ph_tray_shutdown(void);

/*
 * Has ended called at a logout, restart or shutdown, from inside the ph_tray_app_pump that
 * dispatched AppKit's quit, on the main thread. AppKit ends the process as it returns, so it is
 * the one chance to save what a clean exit would: refused, the quit would cancel the logout. Read
 * by ph_tray_app_start, so set before it; with none, a session's end is refused and reported by
 * ph_tray_app_quitting.
 */
PH_TRAY_API void ph_tray_app_on_session_end(PhTraySessionEnd ended);

/*
 * The application, for an app whose only face is the tray: an accessory, with no Dock icon and no
 * menu bar of its own, whose quits are answered by PlatformHeadsMac's TerminateGuard, and with App
 * Nap kept off for napReason. An app with a window of its own has all of that already and does not
 * call this or the three below.
 */
PH_TRAY_API void ph_tray_app_start(const char* napReason);

/*
 * Dispatches AppKit's events for up to a tenth of a second, and returns at once when one has left
 * an item chosen, the icon clicked or a quit asked for. The menu's own tracking loop runs inside it.
 */
PH_TRAY_API void ph_tray_app_pump(void);

/*
 * 1 once AppKit has been asked to quit other than by the session ending: with no Dock icon, by
 * something that sent the app a quit event, such as AppleScript's quit. Refused, for the managed
 * side to end its loop the way its own exit does.
 */
PH_TRAY_API int32_t ph_tray_app_quitting(void);

/* App Nap allowed again, for an app on its way out. */
PH_TRAY_API void ph_tray_app_stop(void);

#endif

#ifdef __cplusplus
}
#endif

#endif
