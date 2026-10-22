/*
 * The embedded font, and the machine's own for the characters it does not have: see runtime.h.
 *
 * JetBrains Mono has Latin, Greek, Cyrillic and the symbols code is written in, and it was the
 * only font a head had: Chinese, Japanese, Korean, Arabic, Hebrew, Thai and emoji all drew as the
 * replacement glyph. The other heads have their toolkits' font fallback. ImGui has none of its own,
 * but it does draw a character from the first of a font's sources to have it, so the machine's
 * fonts are merged into the window's font as further sources.
 *
 * Drawn, and not shaped: each character is the glyph its font has for it, where the grid put it.
 * Arabic is its letters unjoined and in the order they are stored, and an emoji made of several is
 * as many of them as its cells hold. That is enough to see which characters a text holds.
 *
 * Only the fonts a character on screen has needed, and only once one has. Every font fontconfig
 * knows of can be hundreds of megabytes of files, and a screen of ASCII, which is nearly every
 * screen, costs a pass over its bytes and nothing else. Until a font lands its characters are the
 * replacement glyph they always were.
 */
#include "platformheads/runtime.h"

#include "imgui.h"
/* For ImTextCharFromUtf8. */
#include "imgui_internal.h"

#include <algorithm>
#include <climits>
#include <condition_variable>
#include <cstring>
#include <deque>
#include <fstream>
#include <memory>
#include <mutex>
#include <set>
#include <thread>
#include <utility>
#include <vector>

/* For fontconfig, which is found at run time rather than linked. */
#if !defined(_WIN32)
#include <dlfcn.h>
#endif

namespace
{
/* As many of the machine's fonts as are ever merged in. ImGui numbers a font's sources in four
 * bits, and the embedded font is the first of them. */
constexpr size_t fontLimit = 15;

/*
 * One of the machine's fonts, read whole: its bytes, which of the faces in them, and the scale that
 * makes the window's size an em for that face, as AddEmbeddedFont's emScale does for its font.
 */
struct FoundFont
{
    std::vector<unsigned char> data;
    int face = 0;
    float scale = 1.0f;
};

/*
 * Those fonts are looked for and read on a thread of their own: fontconfig reading its caches and
 * a CJK collection coming off the disk are tens of milliseconds each, and the first is seconds on a
 * machine whose caches are stale. Shared with that thread through a shared_ptr, and the thread
 * detached rather than joined: a process that exits without stopping it would otherwise destroy a
 * joinable std::thread, which terminates the process.
 */
struct FontFinder
{
    std::mutex mutex;
    std::condition_variable wake;

    /* Characters the window's font cannot draw, each asked about once. */
    std::vector<uint32_t> wanted;
    std::vector<FoundFont> found;
    bool stopping = false;
};

/*
 * fontconfig's FcFontSet, whose layout is part of its ABI, and the entry points this uses. Found in
 * the library when a character first needs them rather than linked. Linked, a machine without
 * fontconfig could not load a head at all, and building one would need fontconfig's headers.
 */
struct FontSet
{
    int count;
    int capacity;
    void** fonts;
};

struct Fontconfig
{
    void* (*initLoadConfigAndFonts)() = nullptr;
    void* (*nameParse)(const unsigned char* name) = nullptr;
    int (*configSubstitute)(void* config, void* pattern, int kind) = nullptr;
    void (*defaultSubstitute)(void* pattern) = nullptr;
    FontSet* (*fontSort)(void* config, void* pattern, int trim, void** charset, int* result) = nullptr;
    int (*patternGetString)(const void* pattern, const char* object, int index, unsigned char** value) = nullptr;
    int (*patternGetInteger)(const void* pattern, const char* object, int index, int* value) = nullptr;
    int (*patternGetBool)(const void* pattern, const char* object, int index, int* value) = nullptr;
    int (*patternGetCharSet)(const void* pattern, const char* object, int index, void** value) = nullptr;
    int (*charSetHasChar)(const void* charset, unsigned int codepoint) = nullptr;
};

/*
 * Every font on the machine that says which characters it has, in the order fontconfig falls back
 * through them from a monospace font for this user's language. That is the answer every other
 * program here is given, and it is what puts the Japanese forms of the Han characters ahead of the
 * Chinese ones for a Japanese reader.
 */
struct SystemFonts
{
    bool opened = false;
    Fontconfig fontconfig;

    struct Candidate
    {
        const void* pattern;
        const void* charset;
    };

    std::vector<Candidate> candidates;

    /* Handed over already, and found to be something stb_truetype cannot draw from. By file and
     * face rather than by candidate, because one face can be listed more than once. */
    std::set<std::pair<std::string, int>> taken;
    std::set<std::pair<std::string, int>> unusable;
};

struct Fonts
{
    /* The font the machine's fonts are merged into, or null for a head that merges none. */
    ImFont* font = nullptr;

    /* Started with the first character the font cannot draw, so a window that never shows one
     * never has it, nor fontconfig. */
    std::shared_ptr<FontFinder> finder;

    /* One bit a code point, set once it has been looked at, so it is asked about once and a frame
     * of text already seen costs a pass over its bytes. Empty until something past ASCII turns
     * up. */
    std::vector<bool> asked;

    /* The bytes of each font merged in. ImGui rasterises a glyph out of them when a character is
     * first drawn, so they are kept for as long as the atlas is. */
    std::deque<std::vector<unsigned char>> data;
};

Fonts fonts;

#if !defined(_WIN32)
template <typename Entry>
bool Resolve(void* library, const char* name, Entry& entry)
{
    entry = reinterpret_cast<Entry>(dlsym(library, name));
    return entry != nullptr;
}
#endif

/* On the finder's thread, the first time a character is asked about. What fontconfig hands back is
 * not given back: the candidates point into it for as long as the thread runs, which is as long as
 * the window does, and a process has the one window. */
void OpenSystemFonts(SystemFonts& system)
{
    system.opened = true;
#if !defined(_WIN32)
    void* library = dlopen("libfontconfig.so.1", RTLD_NOW | RTLD_LOCAL);
    if (library == nullptr)
    {
        return;
    }

    Fontconfig& fontconfig = system.fontconfig;
    if (!Resolve(library, "FcInitLoadConfigAndFonts", fontconfig.initLoadConfigAndFonts) ||
        !Resolve(library, "FcNameParse", fontconfig.nameParse) ||
        !Resolve(library, "FcConfigSubstitute", fontconfig.configSubstitute) ||
        !Resolve(library, "FcDefaultSubstitute", fontconfig.defaultSubstitute) ||
        !Resolve(library, "FcFontSort", fontconfig.fontSort) ||
        !Resolve(library, "FcPatternGetString", fontconfig.patternGetString) ||
        !Resolve(library, "FcPatternGetInteger", fontconfig.patternGetInteger) ||
        !Resolve(library, "FcPatternGetBool", fontconfig.patternGetBool) ||
        !Resolve(library, "FcPatternGetCharSet", fontconfig.patternGetCharSet) ||
        !Resolve(library, "FcCharSetHasChar", fontconfig.charSetHasChar))
    {
        return;
    }

    void* config = fontconfig.initLoadConfigAndFonts();
    void* pattern = fontconfig.nameParse(reinterpret_cast<const unsigned char*>("monospace"));
    if (config == nullptr ||
        pattern == nullptr)
    {
        return;
    }

    /* The two steps every match is prepared with: the configuration's rules, which is where
     * "monospace" becomes the fonts the machine means by it, and the defaults, which is where the
     * user's language comes from. */
    fontconfig.configSubstitute(config, pattern, 0);
    fontconfig.defaultSubstitute(pattern);

    /* Untrimmed. Trimming leaves out a font with no character the ones ahead of it lack, which it
     * works out by uniting every character set in turn, and each character is asked of them here
     * anyway. */
    int result = 0;
    const FontSet* sorted = fontconfig.fontSort(config, pattern, 0, nullptr, &result);
    if (sorted == nullptr)
    {
        return;
    }

    for (int index = 0; index < sorted->count; index++)
    {
        void* charset = nullptr;
        if (fontconfig.patternGetCharSet(sorted->fonts[index], "charset", 0, &charset) == 0 &&
            charset != nullptr)
        {
            system.candidates.push_back({sorted->fonts[index], charset});
        }
    }
#endif
}

constexpr uint32_t Tag(char first, char second, char third, char fourth)
{
    return static_cast<uint32_t>(static_cast<unsigned char>(first)) << 24 |
           static_cast<uint32_t>(static_cast<unsigned char>(second)) << 16 |
           static_cast<uint32_t>(static_cast<unsigned char>(third)) << 8 |
           static_cast<uint32_t>(static_cast<unsigned char>(fourth));
}

/*
 * The scale that makes the window's size an em for a face: its ascent plus its descent over its
 * em. Merged at ImGui's own scale it is each font's height that is matched, so one with tall lines
 * comes out small and one with short lines large. An em is what the other heads fall back at, and
 * what leaves a CJK character, an em wide, inside the two cells a grid gives it.
 *
 * False for a face stb_truetype, which is what rasterises here, cannot draw from. It wants
 * outlines, as TrueType's or in a CFF table, and a variable font of the CFF2 kind has neither,
 * which is one of the forms Noto CJK comes in. Asked here, on the finder's thread, the next font
 * with the character is tried instead; left to ImGui, the refusal comes on the render thread as an
 * error, and nothing else is tried.
 */
bool EmScaleOf(const std::vector<unsigned char>& data, int face, float& scale)
{
    const size_t size = data.size();
    const auto u16 = [&data, size](size_t at) -> uint32_t
    {
        return at + 2 <= size
            ? static_cast<uint32_t>(data[at]) << 8 | static_cast<uint32_t>(data[at + 1])
            : 0;
    };
    const auto u32 = [&u16](size_t at) -> uint32_t { return u16(at) << 16 | u16(at + 2); };

    /* A collection starts with where each of its faces does. */
    size_t start = 0;
    if (u32(0) == Tag('t', 't', 'c', 'f'))
    {
        if (static_cast<uint32_t>(face) >= u32(8))
        {
            return false;
        }

        start = u32(12 + static_cast<size_t>(face) * 4);
    }
    else if (face != 0)
    {
        return false;
    }

    size_t head = 0;
    size_t hhea = 0;
    bool cmap = false;
    bool hmtx = false;
    bool glyf = false;
    bool loca = false;
    bool cff = false;
    const uint32_t tables = u16(start + 4);
    for (uint32_t table = 0; table < tables; table++)
    {
        const size_t record = start + 12 + static_cast<size_t>(table) * 16;
        switch (u32(record))
        {
            case Tag('h', 'e', 'a', 'd'): head = u32(record + 8); break;
            case Tag('h', 'h', 'e', 'a'): hhea = u32(record + 8); break;
            case Tag('c', 'm', 'a', 'p'): cmap = true; break;
            case Tag('h', 'm', 't', 'x'): hmtx = true; break;
            case Tag('g', 'l', 'y', 'f'): glyf = true; break;
            case Tag('l', 'o', 'c', 'a'): loca = true; break;
            case Tag('C', 'F', 'F', ' '): cff = true; break;
            default: break;
        }
    }

    const int unitsPerEm = static_cast<int>(u16(head + 18));
    const int ascent = static_cast<int16_t>(u16(hhea + 4));
    const int descent = static_cast<int16_t>(u16(hhea + 6));
    if (head == 0 ||
        hhea == 0 ||
        !cmap ||
        !hmtx ||
        !(cff || (glyf && loca)) ||
        unitsPerEm == 0 ||
        ascent <= descent)
    {
        return false;
    }

    scale = static_cast<float>(ascent - descent) / static_cast<float>(unitsPerEm);
    return true;
}

bool ReadFont(const Fontconfig& fontconfig, const void* pattern, const std::pair<std::string, int>& face, FoundFont& found)
{
    /* Outlines, and nothing else. A bitmap font has none to scale, and a colour font - which is
     * what the emoji font usually is - keeps its pictures in tables stb_truetype does not read.
     * Merged, it would draw every emoji as nothing, ahead of a font with plain ones. */
    int flag = 0;
    if ((fontconfig.patternGetBool(pattern, "outline", 0, &flag) == 0 && flag == 0) ||
        (fontconfig.patternGetBool(pattern, "color", 0, &flag) == 0 && flag != 0))
    {
        return false;
    }

    std::ifstream file(face.first, std::ios::binary | std::ios::ate);
    const std::streamoff length = file.tellg();
    /* ImGui takes a length as an int. */
    if (!file ||
        length <= 0 ||
        length > INT_MAX)
    {
        return false;
    }

    std::vector<unsigned char> data(static_cast<size_t>(length));
    file.seekg(0);
    if (!file.read(reinterpret_cast<char*>(data.data()), length) ||
        !EmScaleOf(data, face.second, found.scale))
    {
        return false;
    }

    found.data = std::move(data);
    found.face = face.second;
    return true;
}

/* The first font in fontconfig's order to have a character and be one that can be drawn from, read
 * whole. False when there is none, and when that font has been handed over already, since the
 * character is then on its way with it. */
bool FindFont(SystemFonts& system, uint32_t codepoint, FoundFont& found)
{
    if (!system.opened)
    {
        OpenSystemFonts(system);
    }

    const Fontconfig& fontconfig = system.fontconfig;
    for (const SystemFonts::Candidate& candidate : system.candidates)
    {
        if (fontconfig.charSetHasChar(candidate.charset, codepoint) == 0)
        {
            continue;
        }

        unsigned char* file = nullptr;
        if (fontconfig.patternGetString(candidate.pattern, "file", 0, &file) != 0 ||
            file == nullptr)
        {
            continue;
        }

        /* The face of a collection is the low half of the index. The high half names an instance
         * of a variable font, which stb_truetype draws as its default whichever is asked for. */
        int index = 0;
        fontconfig.patternGetInteger(candidate.pattern, "index", 0, &index);
        const std::pair<std::string, int> face(reinterpret_cast<const char*>(file), index & 0xFFFF);
        if (system.taken.count(face) != 0)
        {
            return false;
        }

        if (system.unusable.count(face) != 0)
        {
            continue;
        }

        if (ReadFont(fontconfig, candidate.pattern, face, found))
        {
            system.taken.insert(face);
            return true;
        }

        system.unusable.insert(face);
    }

    return false;
}

void FindFonts(std::shared_ptr<FontFinder> finder)
{
    SystemFonts system;
    std::unique_lock<std::mutex> lock(finder->mutex);
    while (true)
    {
        finder->wake.wait(lock, [&finder] { return finder->stopping || !finder->wanted.empty(); });
        if (finder->stopping)
        {
            return;
        }

        std::vector<uint32_t> wanted;
        wanted.swap(finder->wanted);
        lock.unlock();

        /* fontconfig and the disk, and nothing of ImGui's, which belongs to the other thread. */
        std::vector<FoundFont> found;
        for (const uint32_t codepoint : wanted)
        {
            FoundFont font;
            if (FindFont(system, codepoint, font))
            {
                found.push_back(std::move(font));
            }
        }

        lock.lock();
        for (FoundFont& font : found)
        {
            finder->found.push_back(std::move(font));
        }
    }
}
}

namespace ph
{
ImFont* AddEmbeddedFont(const uint8_t* ttf, int32_t length, float size, float emScale)
{
    /* ImGui frees font data with its own allocator, so it is handed a copy rather than memory owned
     * by the managed heap. */
    void* copy = IM_ALLOC(static_cast<size_t>(length));
    memcpy(copy, ttf, static_cast<size_t>(length));
    ImFontConfig config;
    config.FontDataOwnedByAtlas = true;
    config.ExtraSizeScale = emScale;
    return ImGui::GetIO().Fonts->AddFontFromMemoryTTF(copy, length, size <= 0.0f ? 15.0f : size, &config);
}

void UseSystemFonts(ImFont* font)
{
    fonts.font = font;
}

void FindFontsFor(const char* text, size_t length)
{
    if (fonts.font == nullptr ||
        text == nullptr ||
        fonts.data.size() >= fontLimit)
    {
        return;
    }

    const char* const end = text + length;
    std::vector<uint32_t> wanted;
    while (text < end)
    {
        /* ASCII, all of which an embedded font has, and which is nearly every byte of nearly every
         * screen. */
        if (static_cast<unsigned char>(*text) < 0x80)
        {
            text++;
            continue;
        }

        unsigned int codepoint = 0;
        text += std::max(1, ImTextCharFromUtf8(&codepoint, text, end));
        if (codepoint > IM_UNICODE_CODEPOINT_MAX)
        {
            continue;
        }

        if (fonts.asked.empty())
        {
            fonts.asked.resize(static_cast<size_t>(IM_UNICODE_CODEPOINT_MAX) + 1);
        }

        if (fonts.asked[codepoint])
        {
            continue;
        }

        fonts.asked[codepoint] = true;
        if (!fonts.font->IsGlyphInFont(static_cast<ImWchar>(codepoint)))
        {
            wanted.push_back(codepoint);
        }
    }

    if (wanted.empty())
    {
        return;
    }

    if (!fonts.finder)
    {
        fonts.finder = std::make_shared<FontFinder>();
        std::thread(FindFonts, fonts.finder).detach();
    }

    {
        const std::lock_guard<std::mutex> lock(fonts.finder->mutex);
        fonts.finder->wanted.insert(fonts.finder->wanted.end(), wanted.begin(), wanted.end());
    }

    fonts.finder->wake.notify_one();
}

bool TakeFonts()
{
    if (!fonts.finder)
    {
        return false;
    }

    std::vector<FoundFont> found;
    {
        const std::lock_guard<std::mutex> lock(fonts.finder->mutex);
        found.swap(fonts.finder->found);
    }

    bool merged = false;
    ImGuiIO& io = ImGui::GetIO();
    for (FoundFont& font : found)
    {
        if (fonts.data.size() >= fontLimit)
        {
            return merged;
        }

        fonts.data.push_back(std::move(font.data));
        std::vector<unsigned char>& data = fonts.data.back();

        /* Merged into the font added before it, which is the window's. */
        ImFontConfig config;
        config.MergeMode = true;
        config.FontNo = static_cast<ImU32>(font.face);
        config.ExtraSizeScale = font.scale;
        config.FontDataOwnedByAtlas = false;

        /* A font stb_truetype turns out not to read after all is left out. That is nothing for
         * ImGui to assert or to write to its log, which is what it does with a font it is given and
         * cannot use. */
        const bool asserts = io.ConfigErrorRecoveryEnableAssert;
        const bool logs = io.ConfigErrorRecoveryEnableDebugLog;
        io.ConfigErrorRecoveryEnableAssert = false;
        io.ConfigErrorRecoveryEnableDebugLog = false;
        const ImFont* added = io.Fonts->AddFontFromMemoryTTF(
            data.data(),
            static_cast<int>(data.size()),
            0.0f,
            &config);
        io.ConfigErrorRecoveryEnableAssert = asserts;
        io.ConfigErrorRecoveryEnableDebugLog = logs;
        if (added == nullptr)
        {
            fonts.data.pop_back();
            continue;
        }

        merged = true;
    }

    return merged;
}

void StopFontFinder()
{
    if (fonts.finder)
    {
        {
            const std::lock_guard<std::mutex> lock(fonts.finder->mutex);
            fonts.finder->stopping = true;
            fonts.finder->wanted.clear();
            fonts.finder->found.clear();
        }

        fonts.finder->wake.notify_one();
        fonts.finder.reset();
    }

    fonts.font = nullptr;
    fonts.data.clear();
    fonts.asked.clear();
}
}
