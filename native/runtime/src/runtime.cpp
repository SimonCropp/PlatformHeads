/*
 * The window, GLFW's input and which frames are built and drawn: see runtime.h. Worked out in
 * DiffEngine's viewer, apart from opening the window hidden, which was BuildMonitor's.
 */
#include "platformheads/runtime.h"

#include "imgui.h"
#include "imgui_internal.h"
#include "raylib.h"
#include "rlgl.h"

#include <algorithm>
#include <cfloat>
#include <cstring>
#include <deque>
#include <vector>

/*
 * GLFW's own calls, which raylib links into the library statically, so the symbols resolve
 * without GLFW's headers. glfwGetCurrentContext returns raylib's window.
 */
extern "C"
{
void* glfwGetCurrentContext(void);
void glfwSetWindowShouldClose(void* window, int value);

/* The window system wanting the window's content drawn again, because part of it has been
 * uncovered or it has been put back on the screen. raylib asks to be told when a window is
 * resized, moved, minimised or focused and not this, since it draws every frame whatever happens.
 * A window that has stopped drawing frames nobody needs has to hear it. raylib sets none. */
typedef void (*PhRefresh)(void* window);
PhRefresh glfwSetWindowRefreshCallback(void* window, PhRefresh callback);

/* A mouse button, the wheel, a key, a character and the pointer crossing the window's edge. raylib
 * sets all five, and keeps from them what was so when it last read the window system's events:
 * a state, read once a frame, which a press and release between two reads leave as it was. So
 * these are set over raylib's, which are kept and still called, and what they are told is kept
 * as what happened. */
typedef void (*PhButtonEvent)(void* window, int button, int action, int mods);
typedef void (*PhScrollEvent)(void* window, double across, double down);
typedef void (*PhKeyEvent)(void* window, int key, int scancode, int action, int mods);
typedef void (*PhCharacterEvent)(void* window, unsigned int codepoint);
typedef void (*PhCrossingEvent)(void* window, int entered);
PhCrossingEvent glfwSetCursorEnterCallback(void* window, PhCrossingEvent callback);
PhButtonEvent glfwSetMouseButtonCallback(void* window, PhButtonEvent callback);
PhScrollEvent glfwSetScrollCallback(void* window, PhScrollEvent callback);
PhKeyEvent glfwSetKeyCallback(void* window, PhKeyEvent callback);
PhCharacterEvent glfwSetCharCallback(void* window, PhCharacterEvent callback);

/* What a key types on the layout in use, unshifted, or null for a key that types nothing. */
const char* glfwGetKeyName(int key, int scancode);

/* How much larger than a pixel the desktop wants everything drawn. raylib asks GLFW this only
 * for a window it was told to scale itself, which this one is not. */
void glfwGetWindowContentScale(void* window, float* across, float* down);
}

namespace
{
constexpr int glfwRelease = 0;
constexpr int glfwPress = 1;

/*
 * How many frames in a row have to be built with nothing arriving, and come out as the frame on
 * the screen, before frames stop being built: a second of them.
 *
 * ImGui does things over several frames and counts some of them in the time it is told has passed.
 * A layout can take a second frame to settle, input given in one frame may be acted on over the
 * next few, and for a quarter of a second after the pointer leaves a row with a tooltip the next
 * row's comes up without its delay. A second is longer than any of them, and costs little: these
 * are frames that are built and compared, not drawn.
 */
constexpr int settledFrames = 60;

struct Press
{
    int button;
    bool down;
};

/* A key going down or up, for ImGui. */
struct KeyChange
{
    ImGuiKey key;
    bool down;
};

struct Runtime
{
    bool open = false;
    float scale = 1.0f;

    /*
     * What the pointer's buttons, the wheel and the keys have done since each was last taken, as
     * GLFW reported it: every press and release in the order they came, every wheel message added
     * up, every key pressed with the character it typed. Under Xvfb, read as raylib's state once a
     * frame, none of ten clicks xdotool sent was seen, and three of ten presses of Page Down.
     */
    std::vector<Press> presses;
    bool held[3] = {};

    /* The wheel twice over, because it is taken twice: by ImGui, and by the app for its own use,
     * which is not at the same moment. */
    float wheelAcross = 0.0f;
    float wheelDown = 0.0f;
    float wheelNotches = 0.0f;
    float wheelRemainder = 0.0f;

    std::deque<ph::KeyPress> keys;

    /* For a head whose text ImGui edits: every key going down and up, and every character. */
    std::vector<KeyChange> keyChanges;
    std::vector<unsigned int> characters;

    /* Whether the next character GLFW reports was typed by the key press at the back of the queue,
     * which is so only straight after that press: GLFW reports the two together. */
    bool characterFollows = false;

    /* A key has been pressed or let go since Arrived last asked. */
    bool keyed = false;

    /*
     * Whether the pointer is over the window, by the last crossing GLFW reported, and whether the
     * last frame was built with it gone. raylib goes on answering with the last place the pointer
     * was in the window, so a pointer that left over a row was still on that row: it stayed lit,
     * and its tooltip came up with the pointer on another window. Not while a button is held,
     * since a selection dragged past the window's edge has to go on being one. Taken to be inside
     * until a crossing says otherwise.
     */
    bool pointerInside = true;
    bool pointerGone = false;

    PhCrossingEvent raylibCrossing = nullptr;
    PhButtonEvent raylibButton = nullptr;
    PhScrollEvent raylibScroll = nullptr;
    PhKeyEvent raylibKey = nullptr;
    PhCharacterEvent raylibCharacter = nullptr;

    /* What was last handed to raylib, so an idle frame is not a window system call. */
    int cursor = MOUSE_CURSOR_DEFAULT;

    /* The window's bounds as they were when it was last neither maximised nor minimised, and
     * whether it is maximised now. */
    bool tracked = false;
    ph::Placement normal{};

    /* What was drawn to make the frame on the screen, reduced to a number: see Fingerprint. */
    uint64_t shown = 0;

    /* The window cannot be taken to be showing the frame last drawn into it: nothing has been
     * drawn yet, the window system has asked for its content again, it has changed size or come
     * back from being hidden, or a texture has been put behind a name that frame may have used. */
    bool stale = true;

    /* Frames in a row that had nothing arrive and came out as the one on the screen. */
    int settled = 0;

    /* When the last present began, and when the last frame's wait ended, by GetTime. */
    double began = 0.0;
    double ended = 0.0;

    /* The pointer and the window as the last present found them. */
    Vector2 pointer{};
    int width = 0;
    int height = 0;
    bool hidden = false;
    bool minimised = false;
    bool focused = false;
};

Runtime runtime;

extern "C" void WindowRefreshed(void* window)
{
    runtime.stale = true;
}

/* The five below are called from inside PollInputEvents, each after raylib's own. */
extern "C" void PointerCrossed(void* window, int entered)
{
    if (runtime.raylibCrossing != nullptr)
    {
        runtime.raylibCrossing(window, entered);
    }

    runtime.pointerInside = entered != 0;
}

extern "C" void ButtonChanged(void* window, int button, int action, int mods)
{
    if (runtime.raylibButton != nullptr)
    {
        runtime.raylibButton(window, button, action, mods);
    }

    /* Left, right and middle, which GLFW and ImGui number alike. */
    if (button >= 0 &&
        button < 3)
    {
        runtime.presses.push_back({button, action != glfwRelease});
        runtime.held[button] = action != glfwRelease;
    }
}

extern "C" void WheelTurned(void* window, double across, double down)
{
    if (runtime.raylibScroll != nullptr)
    {
        runtime.raylibScroll(window, across, down);
    }

    runtime.wheelAcross += static_cast<float>(across);
    runtime.wheelDown += static_cast<float>(down);
    runtime.wheelNotches += static_cast<float>(down);
}

/* ImGui's name for a key, for a head whose text ImGui edits: the keys editing and moving through
 * text and lists need. None for every other, which ImGui is not given. */
ImGuiKey ImGuiKeyOf(int key)
{
    switch (key)
    {
        case KEY_TAB: return ImGuiKey_Tab;
        case KEY_LEFT: return ImGuiKey_LeftArrow;
        case KEY_RIGHT: return ImGuiKey_RightArrow;
        case KEY_UP: return ImGuiKey_UpArrow;
        case KEY_DOWN: return ImGuiKey_DownArrow;
        case KEY_PAGE_UP: return ImGuiKey_PageUp;
        case KEY_PAGE_DOWN: return ImGuiKey_PageDown;
        case KEY_HOME: return ImGuiKey_Home;
        case KEY_END: return ImGuiKey_End;
        case KEY_INSERT: return ImGuiKey_Insert;
        case KEY_DELETE: return ImGuiKey_Delete;
        case KEY_BACKSPACE: return ImGuiKey_Backspace;
        case KEY_SPACE: return ImGuiKey_Space;
        case KEY_ENTER: return ImGuiKey_Enter;
        case KEY_KP_ENTER: return ImGuiKey_KeypadEnter;
        case KEY_ESCAPE: return ImGuiKey_Escape;
        case KEY_LEFT_CONTROL: return ImGuiKey_LeftCtrl;
        case KEY_RIGHT_CONTROL: return ImGuiKey_RightCtrl;
        case KEY_LEFT_SHIFT: return ImGuiKey_LeftShift;
        case KEY_RIGHT_SHIFT: return ImGuiKey_RightShift;
        case KEY_LEFT_ALT: return ImGuiKey_LeftAlt;
        case KEY_RIGHT_ALT: return ImGuiKey_RightAlt;
        case KEY_LEFT_SUPER: return ImGuiKey_LeftSuper;
        case KEY_RIGHT_SUPER: return ImGuiKey_RightSuper;
        case KEY_A: return ImGuiKey_A;
        case KEY_C: return ImGuiKey_C;
        case KEY_V: return ImGuiKey_V;
        case KEY_X: return ImGuiKey_X;
        case KEY_Y: return ImGuiKey_Y;
        case KEY_Z: return ImGuiKey_Z;
        default: return ImGuiKey_None;
    }
}

extern "C" void KeyChanged(void* window, int key, int scancode, int action, int mods)
{
    if (runtime.raylibKey != nullptr)
    {
        runtime.raylibKey(window, key, scancode, action, mods);
    }

    runtime.characterFollows = false;
    runtime.keyed = true;

    /* A repeat goes to ImGui as the key going down again, which is how it repeats a held arrow or
     * delete in a text field. */
    const ImGuiKey named = ImGuiKeyOf(key);
    if (named != ImGuiKey_None)
    {
        runtime.keyChanges.push_back({named, action != glfwRelease});
    }

    if (action == glfwRelease)
    {
        return;
    }

    /* One repeat of a key waiting at a time. A loop that was held up for seconds is handed every
     * repeat the window system kept for it at once, and would go on scrolling for as long again
     * after the key was let go. */
    const bool repeated = action != glfwPress;
    if (repeated)
    {
        for (const ph::KeyPress& waiting : runtime.keys)
        {
            if (waiting.repeated &&
                waiting.key == key &&
                waiting.scancode == scancode)
            {
                return;
            }
        }
    }

    runtime.keys.push_back({key, scancode, mods, repeated, 0});
    runtime.characterFollows = true;
}

extern "C" void CharacterTyped(void* window, unsigned int codepoint)
{
    if (runtime.raylibCharacter != nullptr)
    {
        runtime.raylibCharacter(window, codepoint);
    }

    runtime.characters.push_back(codepoint);

    /* Typed by the press just queued: GLFW reports a key and then its character. One that follows
     * a repeat that was not queued goes with it, and one that comes by itself is what an input
     * method composed, which is none of the keys read here. */
    if (runtime.characterFollows &&
        !runtime.keys.empty())
    {
        runtime.keys.back().character = codepoint;
    }

    runtime.characterFollows = false;
}

void ClearInput()
{
    runtime.presses.clear();
    runtime.keys.clear();
    runtime.keyChanges.clear();
    runtime.characters.clear();
    runtime.held[0] = runtime.held[1] = runtime.held[2] = false;
    runtime.wheelAcross = 0.0f;
    runtime.wheelDown = 0.0f;
    runtime.wheelNotches = 0.0f;
    runtime.wheelRemainder = 0.0f;
    runtime.characterFollows = false;
    runtime.keyed = false;
    runtime.pointerInside = true;
    runtime.pointerGone = false;
}

uint64_t Mix(uint64_t hash, const void* data, size_t size)
{
    const unsigned char* bytes = static_cast<const unsigned char*>(data);
    for (; size >= sizeof(uint64_t); bytes += sizeof(uint64_t), size -= sizeof(uint64_t))
    {
        uint64_t word;
        memcpy(&word, bytes, sizeof word);
        hash = (hash ^ word) * 0x9E3779B97F4A7C15ull;
        hash ^= hash >> 29;
    }

    for (; size > 0; bytes++, size--)
    {
        hash = (hash ^ *bytes) * 0x100000001B3ull;
    }

    return hash;
}

/*
 * Everything RenderDrawData would draw a frame from, reduced to one number: the size drawn at, and
 * for every draw list its vertices, its indices and what each command clips to and draws with. Two
 * frames with the same number are the same pixels, short of a texture's content having changed
 * behind its name, which is asked separately. Sixty four bits, so two frames that differ share a
 * number about as often as never, and a pass over the vertices costs a small part of drawing them.
 */
uint64_t Fingerprint(const ImDrawData* drawData)
{
    uint64_t hash = 0xCBF29CE484222325ull;
    hash = Mix(hash, &drawData->DisplaySize, sizeof drawData->DisplaySize);
    for (int list = 0; list < drawData->CmdListsCount; list++)
    {
        const ImDrawList* commands = drawData->CmdLists[list];
        hash = Mix(
            hash,
            commands->VtxBuffer.Data,
            static_cast<size_t>(commands->VtxBuffer.Size) * sizeof(ImDrawVert));
        hash = Mix(
            hash,
            commands->IdxBuffer.Data,
            static_cast<size_t>(commands->IdxBuffer.Size) * sizeof(ImDrawIdx));
        for (const ImDrawCmd& command : commands->CmdBuffer)
        {
            const uint64_t drawn[] = {
                static_cast<uint64_t>(command.GetTexID()),
                command.VtxOffset,
                command.IdxOffset,
                command.ElemCount};
            hash = Mix(hash, &command.ClipRect, sizeof command.ClipRect);
            hash = Mix(hash, drawn, sizeof drawn);
        }
    }

    return hash;
}

/* Whether ImGui is waiting for a texture to be made, updated or destroyed, which RenderDrawData
 * does as it draws: the font atlas, when a character is drawn for the first time. Such a frame is
 * drawn whatever its fingerprint, so the atlas on the GPU never falls behind the one ImGui holds. */
bool TexturesWaiting(const ImDrawData* drawData)
{
    if (drawData->Textures == nullptr)
    {
        return false;
    }

    for (const ImTextureData* texture : *drawData->Textures)
    {
        if (texture->Status != ImTextureStatus_OK)
        {
            return true;
        }
    }

    return false;
}

/* raylib's cursor for the one ImGui asks for. ImGui only records the cursor it wants: showing it
 * is the backend's job, and a head that never did showed an arrow over every link and splitter. */
int CursorOf(ImGuiMouseCursor cursor)
{
    switch (cursor)
    {
        case ImGuiMouseCursor_TextInput: return MOUSE_CURSOR_IBEAM;
        case ImGuiMouseCursor_ResizeAll: return MOUSE_CURSOR_RESIZE_ALL;
        case ImGuiMouseCursor_ResizeNS: return MOUSE_CURSOR_RESIZE_NS;
        case ImGuiMouseCursor_ResizeEW: return MOUSE_CURSOR_RESIZE_EW;
        case ImGuiMouseCursor_ResizeNESW: return MOUSE_CURSOR_RESIZE_NESW;
        case ImGuiMouseCursor_ResizeNWSE: return MOUSE_CURSOR_RESIZE_NWSE;
        case ImGuiMouseCursor_Hand: return MOUSE_CURSOR_POINTING_HAND;
        case ImGuiMouseCursor_NotAllowed: return MOUSE_CURSOR_NOT_ALLOWED;
        default: return MOUSE_CURSOR_DEFAULT;
    }
}

/* The one character a string is, or zero for a string that is none or several. */
unsigned int OnlyCharacter(const char* text)
{
    if (text == nullptr ||
        *text == '\0')
    {
        return 0;
    }

    unsigned int codepoint = 0;
    const int length = ImTextCharFromUtf8(&codepoint, text, nullptr);
    return length > 0 && text[length] == '\0' ? codepoint : 0;
}

/* Whether the layout in use has a key that types a letter, unshifted. Asked of every key GLFW
 * names, which are the ones that type something, by their own numbers. */
bool LayoutTypes(unsigned int letter)
{
    for (int key = KEY_APOSTROPHE; key <= 162; key++)
    {
        if (OnlyCharacter(glfwGetKeyName(key, 0)) == letter)
        {
            return true;
        }
    }

    return false;
}

/* Whether the layout in use has a key that types any of the letters a to z, unshifted. */
bool LayoutIsLatin()
{
    for (int key = KEY_APOSTROPHE; key <= 162; key++)
    {
        const unsigned int typed = OnlyCharacter(glfwGetKeyName(key, 0));
        if (typed >= 'a' &&
            typed <= 'z')
        {
            return true;
        }
    }

    return false;
}
}

namespace ph
{
bool OpenWindow(const WindowOptions& options)
{
    if (runtime.open)
    {
        return true;
    }

    SetTraceLogLevel(LOG_WARNING);

    /*
     * Always made hidden: made visible, a window first appears where the window manager puts it
     * and then jumps to where it was left.
     *
     * No MSAA. ImGui draws axis aligned quads with pre-antialiased glyph textures, so multisampling
     * buys nothing visually, and it is a real source of difference between a GPU and the software
     * rasteriser pixel snapshots are pinned to.
     *
     * No VSYNC, nor SetTargetFPS: raylib only holds to either inside EndDrawing, which is not
     * called. A frame is ended, and waited out, by Rest.
     *
     * ALWAYS_RUN because PollInputEvents otherwise waits for an event for as long as the window is
     * minimised, and every present makes that call: the managed loop stopped there with it, and
     * a show asked for over a socket waited until something else brought the window back.
     */
    SetConfigFlags(FLAG_WINDOW_RESIZABLE | FLAG_WINDOW_ALWAYS_RUN | FLAG_WINDOW_HIDDEN);

    /* At the size it was left, which is known before there is a window to ask about monitors.
     * Where it goes is decided below, once there is. */
    const Placement* placement = options.placement;
    const bool sized = placement != nullptr &&
                       placement->width > 0 &&
                       placement->height > 0;
    InitWindow(
        sized ? placement->width : options.width,
        sized ? placement->height : options.height,
        options.title == nullptr ? "" : options.title);
    if (!IsWindowReady())
    {
        return false;
    }

    void* handle = glfwGetCurrentContext();

    /* Not under 1: a desktop set smaller than a pixel to the pixel is not asking for text below
     * the size it is legible at. And not a number that is no scale at all. */
    float across = 1.0f;
    float down = 1.0f;
    glfwGetWindowContentScale(handle, &across, &down);
    runtime.scale = across > 1.0f && across <= 8.0f ? across : 1.0f;

    runtime.tracked = false;
    if (!sized &&
        runtime.scale != 1.0f)
    {
        /* The size asked for is in the pixels of an ordinary display. As much of it at this
         * display's scale as its monitor has room for, and in the middle of that monitor, which is
         * where raylib put the window it has just made. There is no asking the scale before there
         * is a window to ask it of. */
        const int monitor = GetCurrentMonitor();
        const int wide = std::min(static_cast<int>(static_cast<float>(options.width) * runtime.scale), GetMonitorWidth(monitor));
        const int tall = std::min(static_cast<int>(static_cast<float>(options.height) * runtime.scale), GetMonitorHeight(monitor));
        const Vector2 origin = GetMonitorPosition(monitor);
        SetWindowSize(wide, tall);
        SetWindowPosition(
            static_cast<int>(origin.x) + (GetMonitorWidth(monitor) - wide) / 2,
            static_cast<int>(origin.y) + (GetMonitorHeight(monitor) - tall) / 2);
    }

    if (sized)
    {
        const bool reachable = OnAMonitor(*placement);
        if (reachable)
        {
            SetWindowPosition(placement->x, placement->y);
        }

        /* What it restores to, noted before maximising takes the chance away. Taken from the
         * placement rather than asked of a window that is still hidden, which TrackPlacement
         * leaves alone. */
        const Vector2 position = GetWindowPosition();
        runtime.normal.x = static_cast<int32_t>(position.x);
        runtime.normal.y = static_cast<int32_t>(position.y);
        runtime.normal.width = placement->width;
        runtime.normal.height = placement->height;
        runtime.normal.maximized = placement->maximized;
        runtime.tracked = true;
        if (placement->maximized)
        {
            MaximizeWindow();
        }
    }

    if (!options.hidden)
    {
        ClearWindowState(FLAG_WINDOW_HIDDEN);
    }

    SetExitKey(KEY_NULL);

    glfwSetWindowRefreshCallback(handle, WindowRefreshed);
    runtime.raylibCrossing = glfwSetCursorEnterCallback(handle, PointerCrossed);
    runtime.raylibButton = glfwSetMouseButtonCallback(handle, ButtonChanged);
    runtime.raylibScroll = glfwSetScrollCallback(handle, WheelTurned);
    runtime.raylibKey = glfwSetKeyCallback(handle, KeyChanged);
    runtime.raylibCharacter = glfwSetCharCallback(handle, CharacterTyped);
    ClearInput();

    /* Nothing about a window that came before this one says anything about this one, whose clock
     * has started again from nothing. */
    runtime.cursor = MOUSE_CURSOR_DEFAULT;
    runtime.shown = 0;
    runtime.stale = true;
    runtime.settled = 0;
    runtime.began = 0.0;
    runtime.ended = 0.0;
    runtime.pointer = Vector2{};
    runtime.width = 0;
    runtime.height = 0;
    runtime.hidden = options.hidden;
    runtime.minimised = false;
    runtime.focused = false;
    runtime.open = true;
    return true;
}

void CloseWindow()
{
    if (!runtime.open)
    {
        return;
    }

    ::CloseWindow();
    ClearInput();
    runtime.tracked = false;
    runtime.open = false;
}

float WindowScale()
{
    return runtime.scale;
}

void SetHidden(bool hidden)
{
    if (!runtime.open)
    {
        return;
    }

    if (hidden)
    {
        SetWindowState(FLAG_WINDOW_HIDDEN);
        return;
    }

    ClearWindowState(FLAG_WINDOW_HIDDEN);
    /* Only a minimised one: RestoreWindow also takes a maximised window back to its bounds, so a
     * window left maximised would never stay that way. */
    if (IsWindowMinimized())
    {
        RestoreWindow();
    }

    /* The window system asks for a window it has just shown to be drawn. Not waited for: nothing
     * was keeping what a hidden window showed. */
    runtime.stale = true;
}

void Focus()
{
    if (!runtime.open)
    {
        return;
    }

    ClearWindowState(FLAG_WINDOW_HIDDEN);
    if (IsWindowMinimized())
    {
        RestoreWindow();
    }

    SetWindowFocused();
    runtime.stale = true;
}

void SetClipboard(const char* text)
{
    if (text == nullptr ||
        !runtime.open)
    {
        return;
    }

    SetClipboardText(text);
}

bool TakeCloseRequest()
{
    if (!runtime.open ||
        !WindowShouldClose())
    {
        return false;
    }

    void* handle = glfwGetCurrentContext();
    if (handle != nullptr)
    {
        glfwSetWindowShouldClose(handle, 0);
    }

    return true;
}

bool Unseen()
{
    return runtime.open &&
           (IsWindowState(FLAG_WINDOW_HIDDEN) || IsWindowMinimized());
}

bool GetPlacement(Placement& placement)
{
    if (!runtime.open ||
        !runtime.tracked)
    {
        return false;
    }

    placement = runtime.normal;
    return true;
}

void TrackPlacement()
{
    if (!runtime.open ||
        IsWindowState(FLAG_WINDOW_HIDDEN) ||
        IsWindowMinimized())
    {
        return;
    }

    runtime.normal.maximized = IsWindowMaximized();
    if (runtime.normal.maximized &&
        runtime.tracked)
    {
        return;
    }

    const Vector2 position = GetWindowPosition();
    runtime.normal.x = static_cast<int32_t>(position.x);
    runtime.normal.y = static_cast<int32_t>(position.y);
    runtime.normal.width = GetScreenWidth();
    runtime.normal.height = GetScreenHeight();
    runtime.tracked = true;
}

bool OnAMonitor(const Placement& placement)
{
    constexpr int reach = 100;
    const int count = GetMonitorCount();
    for (int index = 0; index < count; index++)
    {
        const Vector2 origin = GetMonitorPosition(index);
        const int left = static_cast<int>(origin.x);
        const int top = static_cast<int>(origin.y);
        const int right = left + GetMonitorWidth(index);
        const int bottom = top + GetMonitorHeight(index);
        const int overlap = std::min(placement.x + placement.width, right) - std::max(placement.x, left);
        if (overlap >= reach &&
            placement.y >= top &&
            placement.y < bottom - reach)
        {
            return true;
        }
    }

    return false;
}

bool PopKey(KeyPress& press)
{
    if (runtime.keys.empty())
    {
        return false;
    }

    press = runtime.keys.front();
    runtime.keys.pop_front();
    return true;
}

int32_t TakeWheelNotches()
{
    runtime.wheelRemainder += runtime.wheelNotches;
    runtime.wheelNotches = 0.0f;
    const int32_t notches = static_cast<int32_t>(runtime.wheelRemainder);
    runtime.wheelRemainder -= static_cast<float>(notches);
    return notches;
}

void FeedImGui(float elapsed, bool keys)
{
    ImGuiIO& io = ImGui::GetIO();
    io.DisplaySize = ImVec2(static_cast<float>(GetScreenWidth()), static_cast<float>(GetScreenHeight()));
    io.DeltaTime = elapsed;

    /* Nowhere, for a pointer that has left the window: see Runtime::pointerInside. */
    const Vector2 mouse = GetMousePosition();
    if (runtime.pointerGone)
    {
        io.AddMousePosEvent(-FLT_MAX, -FLT_MAX);
    }
    else
    {
        io.AddMousePosEvent(mouse.x, mouse.y);
    }

    /* Every press and release since the last frame built, in the order they came. ImGui takes a
     * press and a release handed over together a frame apart, so what is drawn is clicked as it
     * would be by a button that was held. */
    for (const Press& press : runtime.presses)
    {
        io.AddMouseButtonEvent(press.button, press.down);
    }

    runtime.presses.clear();

    if (runtime.wheelAcross != 0.0f ||
        runtime.wheelDown != 0.0f)
    {
        io.AddMouseWheelEvent(runtime.wheelAcross, runtime.wheelDown);
        runtime.wheelAcross = 0.0f;
        runtime.wheelDown = 0.0f;
    }

    if (keys)
    {
        /* Read as raylib's state, which follows every press and release: GLFW's mods for a
         * modifier's own event are the state before it, under X11. */
        io.AddKeyEvent(ImGuiMod_Ctrl, IsKeyDown(KEY_LEFT_CONTROL) || IsKeyDown(KEY_RIGHT_CONTROL));
        io.AddKeyEvent(ImGuiMod_Shift, IsKeyDown(KEY_LEFT_SHIFT) || IsKeyDown(KEY_RIGHT_SHIFT));
        io.AddKeyEvent(ImGuiMod_Alt, IsKeyDown(KEY_LEFT_ALT) || IsKeyDown(KEY_RIGHT_ALT));
        io.AddKeyEvent(ImGuiMod_Super, IsKeyDown(KEY_LEFT_SUPER) || IsKeyDown(KEY_RIGHT_SUPER));
        for (const KeyChange& change : runtime.keyChanges)
        {
            io.AddKeyEvent(change.key, change.down);
        }

        for (const unsigned int character : runtime.characters)
        {
            io.AddInputCharacter(character);
        }
    }

    runtime.keyChanges.clear();
    runtime.characters.clear();
}

unsigned int LetterOf(const KeyPress& press)
{
    const unsigned int typed = press.character != 0
        ? press.character
        : OnlyCharacter(glfwGetKeyName(press.key, press.scancode));
    if (typed == 0)
    {
        return 0;
    }

    if (typed < 0x80)
    {
        /* Which letter, and nothing of its case. A capital says that Shift or Caps Lock was on and
         * not which of them, so read as typed Caps Lock turned a plain key into its shifted
         * command. */
        return typed >= 'A' && typed <= 'Z' ? typed + ('a' - 'A') : typed;
    }

    /* A layout with no Latin letters types its own from the keys a US keyboard has A to Z on, so
     * not one letter a head reads could be typed there. Then the key is read as the letter a US
     * keyboard has in its place, which is what such a keyboard has printed on it beside its own.
     * Only where no key of the layout types that letter: a layout that has it somewhere else keeps
     * it there, or a Turkish keyboard's dotless i, where a US keyboard has R, would be R. */
    if (press.key >= KEY_A &&
        press.key <= KEY_Z)
    {
        const unsigned int letter = static_cast<unsigned int>(press.key - KEY_A) + 'a';
        if (!LayoutTypes(letter))
        {
            return letter;
        }
    }

    /* And the five keys that are not letters, on a layout with no Latin letters at all: Russian and
     * Arabic type letters of their own where a US keyboard has [ and ], and Persian its own digits.
     * Only on such a layout: a German keyboard types u with a diaeresis from where [ is, and
     * reading it by position would give a letter of its user's own language a command. */
    if (!LayoutIsLatin())
    {
        switch (press.key)
        {
            case KEY_LEFT_BRACKET: return '[';
            case KEY_RIGHT_BRACKET: return ']';
            case KEY_ZERO: return '0';
            case KEY_MINUS: return '-';
            case KEY_EQUAL: return '=';
            default: break;
        }
    }

    return 0;
}

float BeginPresent(ImGuiContext* context)
{
    ImGui::SetCurrentContext(context);
    const double now = GetTime();
    const float elapsed = runtime.began > 0.0 && now > runtime.began
        ? static_cast<float>(now - runtime.began)
        : static_cast<float>(FrameSeconds);
    runtime.began = now;
    return elapsed;
}

bool Arrived()
{
    bool arrived = false;

    const Vector2 pointer = GetMousePosition();
    if (pointer.x != runtime.pointer.x ||
        pointer.y != runtime.pointer.y)
    {
        runtime.pointer = pointer;
        arrived = true;
    }

    /* A press or a release waiting to be handed to ImGui, a button held, or one of a press and
     * release handed over together that ImGui is keeping for the frame after. */
    const ImGuiContext* context = ImGui::GetCurrentContext();
    if (!runtime.presses.empty() ||
        runtime.held[0] ||
        runtime.held[1] ||
        runtime.held[2] ||
        (context != nullptr && context->InputEventsQueue.Size > 0))
    {
        arrived = true;
    }

    if (runtime.wheelAcross != 0.0f ||
        runtime.wheelDown != 0.0f)
    {
        arrived = true;
    }

    /* The pointer leaving the window, or coming back to where it left from: raylib's position for
     * it is the same before and after. */
    const bool gone =
        !runtime.pointerInside &&
        !runtime.held[0] &&
        !runtime.held[1] &&
        !runtime.held[2];
    if (gone != runtime.pointerGone)
    {
        runtime.pointerGone = gone;
        arrived = true;
    }

    /* By the press rather than by what is down, since raylib reports Caps Lock and Num Lock as
     * held for as long as they are on. */
    if (runtime.keyed)
    {
        runtime.keyed = false;
        arrived = true;
    }

    const int width = GetScreenWidth();
    const int height = GetScreenHeight();
    const bool hidden = IsWindowState(FLAG_WINDOW_HIDDEN);
    const bool minimised = IsWindowMinimized();
    const bool focused = IsWindowFocused();
    if (width != runtime.width ||
        height != runtime.height ||
        hidden != runtime.hidden ||
        minimised != runtime.minimised)
    {
        /* A window of another size has another framebuffer, and one that was not on the screen
         * was not being kept by anything. */
        runtime.stale = true;
        arrived = true;
    }

    if (focused != runtime.focused)
    {
        arrived = true;
    }

    runtime.width = width;
    runtime.height = height;
    runtime.hidden = hidden;
    runtime.minimised = minimised;
    runtime.focused = focused;
    return arrived;
}

bool Hidden()
{
    return runtime.hidden || runtime.minimised;
}

bool CanLeaveAlone(bool arrived)
{
    return !arrived &&
           !runtime.stale &&
           runtime.settled >= settledFrames;
}

bool Show(ImDrawData* drawData, uint8_t red, uint8_t green, uint8_t blue, bool arrived)
{
    const int cursor = CursorOf(ImGui::GetMouseCursor());
    if (cursor != runtime.cursor)
    {
        runtime.cursor = cursor;
        SetMouseCursor(cursor);
    }

    const uint64_t frame = Fingerprint(drawData);
    if (runtime.stale ||
        frame != runtime.shown ||
        TexturesWaiting(drawData))
    {
        BeginDrawing();
        ClearBackground(Color{red, green, blue, 255});
        RenderDrawData(drawData);
        rlDrawRenderBatchActive();
        SwapScreenBuffer();
        runtime.shown = frame;
        runtime.stale = false;
        runtime.settled = 0;
        return true;
    }

    if (arrived)
    {
        runtime.settled = 0;
    }
    else if (runtime.settled < settledFrames)
    {
        runtime.settled++;
    }

    return false;
}

void Rest()
{
    const double left = FrameSeconds - (GetTime() - runtime.ended);
    if (left > 0.0)
    {
        /* Never longer than a frame, whatever the clock has done. */
        WaitTime(std::min(left, FrameSeconds));
    }

    runtime.characterFollows = false;
    PollInputEvents();
    runtime.ended = GetTime();
    TrackPlacement();
}

void MarkStale()
{
    runtime.stale = true;
}
}
