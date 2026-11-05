/*
 * Brings the tray's ABI structs and constants into Swift without their prototypes: the
 * PlatformHeadsMacTray target provides those symbols itself, with @_cdecl, and a prototype
 * imported beside each would be a second declaration of it. Reached by a relative include rather
 * than copied, so there is one header to change.
 */
#ifndef PLATFORMHEADS_TRAY_BRIDGE_H
#define PLATFORMHEADS_TRAY_BRIDGE_H

#define PH_TRAY_TYPES_ONLY
#include "../../../include/platformheads_tray.h"

#endif
