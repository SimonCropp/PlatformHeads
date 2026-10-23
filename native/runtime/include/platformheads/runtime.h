/*
 * What every native head built on raylib and Dear ImGui does underneath its drawing, in one
 * place: the window, GLFW's input as it happened, the fonts, the renderer and which frames are
 * built and drawn. Each of these was worked out in one app and copied into the next, and the fixes
 * made to one copy were found missing from the other by its users.
 *
 * A toolkit and not a framework. An app keeps its own present, poll and capture, its own state and
 * every bit of its drawing, and calls these where it used to do the same itself. One window a
 * process, as raylib has it.
 *
 * Compiled into the app's library by platformheads_add_library, which also puts this directory on
 * its include path.
 */
#pragma once

#include "imgui.h"

#include <cstddef>
#include <cstdint>
#include <string>

namespace ph
{
/*
 * Where a window is and whether it fills its screen: the bounds it goes back to when it is not
 * maximised, in the desktop's pixels from the top left of the primary monitor.
 */
struct Placement
{
    int32_t x = 0;
    int32_t y = 0;
    int32_t width = 0;
    int32_t height = 0;
    bool maximized = false;
};

/* ---- the window ---- */

struct WindowOptions
{
    /* The size of a window opened for the first time, in the pixels of an ordinary display: it
     * is scaled to the display it opens on. */
    int width = 1000;
    int height = 640;
    const char* title = nullptr;

    /* Opened and kept hidden, as behind a tray. */
    bool hidden = false;

    /* How the last window was left, or null. Used whether or not the window opens hidden, so one
     * started behind a tray comes up where it was left when it is shown. */
    const Placement* placement = nullptr;
};

/*
 * Makes the window, and puts GLFW's callbacks over raylib's so input is kept as it happened.
 *
 * Always made hidden, then sized, placed and maximised as it was left, then shown unless asked to
 * stay hidden: made visible, a window first appears where the window manager puts it and then
 * jumps. False when there is no window to be had, as with no display.
 */
bool OpenWindow(const WindowOptions& options);

/* Closes the window and forgets everything about it. The app destroys its ImGui context first,
 * and calls StopFontFinder before this. */
void CloseWindow();

/*
 * How much larger than a pixel the desktop wants things drawn, which under X11 is Xft.dpi over 96,
 * read as the window was made. 1 on an ordinary display. Not under 1, nor over 8.
 *
 * Under X11 a pixel is a pixel whatever the display, so a head that never asked was half size on a
 * display set to twice the size. A head scales its font, ImGui's sizes and the lengths it gives in
 * pixels by this, and never a capture, which draws at the size it is told.
 */
float WindowScale();

/* Hides or shows the window. Showing brings a minimised one back, which RestoreWindow alone would
 * not leave maximised, and marks it to be drawn: nothing kept what a hidden window showed. */
void SetHidden(bool hidden);

/* Shows, un-minimises and raises the window. A minimised window stays minimised through
 * SetWindowFocused alone, so a focus for something new left it in the taskbar. */
void Focus();

/* A runtime that never opened a window, a capture host, copies nothing rather than crashing:
 * GLFW owns the clipboard and needs its window. */
void SetClipboard(const char* text);

/*
 * Whether the window was asked to close since the last call. raylib latches GLFW's close flag and
 * offers no way to clear it, and a window that hides on close, as one beside a tray does, would
 * otherwise report closing again on every frame after it was shown. Cleared here.
 */
bool TakeCloseRequest();

/* Hidden or minimised, as the last present found it: nothing of the window is on the screen. One
 * behind another window is not known to be: X11 says nothing of it that GLFW passes on. */
bool Unseen();

/* The bounds the window goes back to and whether it is maximised now, as last tracked, or false
 * before there have been any. */
bool GetPlacement(Placement& placement);

/* Notes the window's bounds while it is neither maximised, minimised nor hidden. GLFW says where
 * a maximised window is and nothing about where it goes back to, so that has to have been noted
 * while it was still there. Called by Rest, so every present notes them. */
void TrackPlacement();

/* Whether a remembered window would open somewhere it can be reached: its top edge on a monitor,
 * with enough of its width there to take hold of. */
bool OnAMonitor(const Placement& placement);

/*
 * Called once as the connection to the X server is lost, which is how a logoff ends a head: the
 * server goes, and with it XWayland on a Wayland desktop, since GLFW is built for X11 alone. Xlib
 * then ends the process with exit(1), which runs nothing an app would have run on its way out, so
 * this is its one chance to save what a clean exit would have.
 *
 * On the thread that reads the window's events, inside whichever call was reading them. The process
 * still ends once it returns: Xlib allows nothing else, and the libX11 the library is built against
 * is older than the call that would. Null stops listening. Nothing happens off Linux.
 */
void OnDisplayLost(void (*lost)());

/* ---- input ---- */

/*
 * A key pressed, or repeating while it is held, with the character it typed if it typed one.
 * Handed out one at a time by PopKey, so two keys between two polls are both acted on and in their
 * order, and a press and release that came between two of raylib's readings at all.
 */
struct KeyPress
{
    /* GLFW's number for the key, which is where it is on a US keyboard, or -1 for a key it has
     * none for, and the window system's own number for it. */
    int key = -1;
    int scancode = 0;
    int mods = 0;
    bool repeated = false;
    unsigned int character = 0;
};

/* GLFW's modifier bits, as KeyPress::mods carries them. */
constexpr int ModShift = 0x0001;
constexpr int ModControl = 0x0002;
constexpr int ModAlt = 0x0004;
constexpr int ModSuper = 0x0008;

/* The next key pressed, oldest first. */
bool PopKey(KeyPress& press);

/* Whole notches the wheel has turned down since the last call, keeping the fraction: a touchpad
 * sends a fraction of one a message, and truncating each threw every one of them away. */
int32_t TakeWheelNotches();

/*
 * Hands ImGui what has happened since the last frame built: the size, the time, where the pointer
 * is or that it has left, every press and release in the order they came, and the wheel. With
 * `keys`, also every key going down and up and every character typed, for a head whose text fields
 * ImGui edits; without, ImGui is given no keys, for a head that reads every key itself.
 *
 * `elapsed` is the time since the present before this one began, from BeginPresent.
 */
void FeedImGui(float elapsed, bool keys);

/*
 * Which character a key press is to be read as, in lower case, or zero for none: what it typed,
 * or with control held, what the key types unshifted on the layout in use, and on a layout with no
 * Latin letters the letter a US keyboard has in its place. Read by position, Ctrl+A on AZERTY was
 * the key labelled Q.
 */
unsigned int LetterOf(const KeyPress& press);

/* ---- which frames are built and drawn ---- */

/* How long a frame lasts, which is what holds the managed loop to sixty turns a second. */
constexpr double FrameSeconds = 1.0 / 60.0;

/*
 * Starts a present: makes the window's ImGui context current and says how long it is since the
 * last present began, built or not, which is a frame's length while frames are coming. Not the
 * time since the last frame ImGui was given: after a window has been left alone for an hour, that
 * would have a tooltip's delay over in the first frame.
 */
float BeginPresent(ImGuiContext* context);

/*
 * Whether anything has come from the pointer, the buttons, the wheel, the keys or the window since
 * the last call, by what GLFW's callbacks kept rather than raylib's state, so Caps Lock and Num
 * Lock, which raylib reports as held for as long as they are on, are no reason to draw. A change
 * of size, of hidden or of minimised also marks the window stale.
 */
bool Arrived();

/* Hidden or minimised, as Arrived last found it. A head builds and draws nothing for it, but
 * still takes in what arrived, so the first present after it is shown has it. */
bool Hidden();

/*
 * Whether a frame can be left unbuilt and undrawn: nothing arrived, the window can be taken to
 * show what was last drawn, and a second of frames built since anything last changed have all
 * come out as the frame on the screen, which is what a spinner, or anything else that moves by
 * itself, fails. A head asks whatever else it builds from, its pictures' files say, only when this
 * is true.
 */
bool CanLeaveAlone(bool arrived);

/*
 * Puts the frame ImGui has just rendered on the screen if it differs from the one there, or the
 * window cannot be taken to show that, or a texture is waiting: the pointer crossing a pane, a key
 * that did nothing and the frames after any change mostly come out as the frame before. Shows the
 * cursor ImGui asked for. Returns whether it drew.
 */
bool Show(ImDrawData* drawData, uint8_t red, uint8_t green, uint8_t blue, bool arrived);

/*
 * The end of every present, drawn or not: waits out what is left of the frame, then reads the
 * window system's events, last, so what a poll reports and the next frame is built from are as
 * fresh as they can be, and notes the window's placement. raylib's EndDrawing did the wait and the
 * read, and only for a frame it had put on the screen.
 */
void Rest();

/* The next frame is drawn whatever it comes out as: a texture has been put behind a name the
 * frame on the screen may have used, say. */
void MarkStale();

/* ---- drawing ---- */

/*
 * Draws ImGui's draw lists through rlgl, honouring its texture requests
 * (ImGuiBackendFlags_RendererHasTextures) and the vertex offset of a list split past what a
 * sixteen bit index reaches (ImGuiBackendFlags_RendererHasVtxOffset). Declare both on a context
 * drawn through this. Into whatever is being drawn to, the window or a render texture.
 */
void RenderDrawData(ImDrawData* drawData);

/* ---- fonts ---- */

/*
 * Adds the font a head embeds, from a copy ImGui owns. `size` is an em, which is what Core Text
 * and GDI+ take; ImGui's loader scales by pixel height, so `emScale` is the font's ascent plus
 * descent over its em, which makes the one the other.
 */
ImFont* AddEmbeddedFont(const uint8_t* ttf, int32_t length, float size, float emScale);

/*
 * The machine's own fonts, merged into `font` as further sources for the characters it has no
 * glyph for. Found through fontconfig, which is looked for at run time rather than linked, so a
 * machine without it can still load the library, on a thread of its own, and only for characters
 * on screen. Drawn and not shaped. `font` must be the font added last, since a merge goes into the
 * font added before it.
 */
void UseSystemFonts(ImFont* font);

/* Asks for a font for every character of `text` the font cannot draw. ASCII costs a pass over its
 * bytes and nothing else, and each code point is asked about once. */
void FindFontsFor(const char* text, size_t length);

/* Merges what the finder has read. At the top of a frame: ImGui takes a new source only between
 * frames. Returns whether one was merged, which is a frame to build. */
bool TakeFonts();

/* Stops the finder and forgets the fonts. Before CloseWindow, and after the app's ImGui context,
 * whose atlas reads glyphs out of them, is destroyed. */
void StopFontFinder();

/* ---- strings ---- */

/*
 * The text at an offset into an app's string blob. Bad offsets are a crash, not a glitch, so the
 * whole boundary is bounds checked rather than trusted, and the bounds are added in sixty four
 * bits, where two large thirty two bit values cannot wrap.
 */
bool Slice(const uint8_t* strings, int32_t stringsLength, int32_t offset, int32_t length, const char** begin, const char** end);
std::string Copy(const uint8_t* strings, int32_t stringsLength, int32_t offset, int32_t length);

/* ---- labels ---- */

/*
 * ImGui reads a label for more than its text: from "##" on it is the item's identity and is not
 * drawn. These draw a label that has the mark in it themselves, over an item given no text, so a
 * file named "Notes##2.txt" is not "Notes". An item is given its identity by PushID.
 */
bool Marked(const std::string& label);
bool SelectableLabel(const std::string& label, bool selected = false, ImGuiSelectableFlags flags = 0, const ImVec2& size = ImVec2(0.0f, 0.0f));
bool ButtonLabel(const std::string& label);
float LabelWidth(const std::string& label);

/* One line of text in no more than a width, ending in an ellipsis where it was cut short. */
void TextWithin(const char* begin, const char* end, float width);

/* The header row of a table, as TableHeadersRow lays it out, with a marked header's text drawn
 * over a header given none and cut at its column's edge. `headers` from column `first` on. */
void HeadersRow(const std::string* headers, int first, int columns);
}
