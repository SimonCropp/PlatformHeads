// A head with two entry points: enough to load, to ask a version of, and to have raylib and Dear
// ImGui linked in and called, which is what decides what the library needs of a machine.
#include <cstdint>

#include "imgui.h"
#include "raylib.h"

#if defined(_WIN32)
#define SAMPLE_API __declspec(dllexport)
#else
#define SAMPLE_API __attribute__((visibility("default")))
#endif

extern "C" {

SAMPLE_API int32_t sample_version(void) {
    return 1;
}

// One frame of an ImGui window into an offscreen texture of the size given, and how many
// vertices it came to. Opens a hidden window, since raylib has no context without one.
SAMPLE_API int32_t sample_frame(int32_t width, int32_t height) {
    SetTraceLogLevel(LOG_WARNING);
    SetConfigFlags(FLAG_WINDOW_HIDDEN);
    InitWindow(width, height, "sample");
    if (!IsWindowReady()) {
        return -1;
    }

    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.IniFilename = nullptr;
    io.DisplaySize = ImVec2(static_cast<float>(width), static_cast<float>(height));
    unsigned char* pixels = nullptr;
    int atlasWidth = 0;
    int atlasHeight = 0;
    io.Fonts->GetTexDataAsRGBA32(&pixels, &atlasWidth, &atlasHeight);

    ImGui::NewFrame();
    ImGui::Begin("sample");
    ImGui::TextUnformatted("sample");
    ImGui::End();
    ImGui::Render();
    const int32_t vertices = ImGui::GetDrawData()->TotalVtxCount;

    ImGui::DestroyContext();
    CloseWindow();
    return vertices;
}

} // extern "C"
