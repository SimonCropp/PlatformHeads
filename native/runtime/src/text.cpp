/*
 * An app's strings and labels: see runtime.h.
 */
#include "platformheads/runtime.h"

#include "imgui.h"
#include "imgui_internal.h"

#include <algorithm>

namespace ph
{
bool Slice(const uint8_t* strings, int32_t stringsLength, int32_t offset, int32_t length, const char** begin, const char** end)
{
    if (strings == nullptr ||
        offset < 0 ||
        length < 0 ||
        static_cast<int64_t>(offset) + static_cast<int64_t>(length) > static_cast<int64_t>(stringsLength))
    {
        return false;
    }

    *begin = reinterpret_cast<const char*>(strings) + offset;
    *end = *begin + length;
    return true;
}

std::string Copy(const uint8_t* strings, int32_t stringsLength, int32_t offset, int32_t length)
{
    const char* begin;
    const char* end;
    if (!Slice(strings, stringsLength, offset, length, &begin, &end))
    {
        return {};
    }

    return {begin, static_cast<size_t>(end - begin)};
}

void TextWithin(const char* begin, const char* end, float width)
{
    const float room = std::max(width, 0.0f);
    const ImVec2 size = ImGui::CalcTextSize(begin, end);
    const ImVec2 position = ImGui::GetCursorScreenPos();
    const ImVec2 limit(position.x + room, position.y + size.y);
    ImGui::Dummy(ImVec2(std::min(size.x, room), size.y));
    ImGui::RenderTextEllipsis(ImGui::GetWindowDrawList(), position, limit, limit.x, begin, end, &size);
}

/*
 * A label is never an identity here, since every item is given one by its index. So one with the
 * mark in it is drawn by this side, where the item would have drawn it, over an item that is given
 * no text at all. One without it is left to the item, which is every label there has been.
 */
bool Marked(const std::string& label)
{
    return label.find("##") != std::string::npos;
}

namespace
{
void DrawLabel(const ImVec2& position, const std::string& label)
{
    ImGui::GetWindowDrawList()->AddText(
        position,
        ImGui::GetColorU32(ImGuiCol_Text),
        label.data(),
        label.data() + label.size());
}
}

bool SelectableLabel(const std::string& label, bool selected, ImGuiSelectableFlags flags, const ImVec2& size)
{
    if (!Marked(label))
    {
        return ImGui::Selectable(label.c_str(), selected, flags, size);
    }

    /* Where Selectable puts its text: at the cursor, on the line's baseline. */
    const ImGuiWindow* window = ImGui::GetCurrentWindow();
    const ImVec2 position(window->DC.CursorPos.x, window->DC.CursorPos.y + window->DC.CurrLineTextBaseOffset);
    const bool pressed = ImGui::Selectable("##label", selected, flags, size);
    DrawLabel(position, label);
    return pressed;
}

float LabelWidth(const std::string& label)
{
    return ImGui::CalcTextSize(label.data(), label.data() + label.size()).x;
}

bool ButtonLabel(const std::string& label)
{
    if (!Marked(label))
    {
        return ImGui::Button(label.c_str());
    }

    /* The size Button gives itself from its text, and the text where it puts it: inside the
     * frame's padding. */
    const ImGuiStyle& style = ImGui::GetStyle();
    const bool pressed = ImGui::Button(
        "##label",
        ImVec2(
            LabelWidth(label) + style.FramePadding.x * 2.0f,
            ImGui::GetTextLineHeight() + style.FramePadding.y * 2.0f));
    const ImVec2 corner = ImGui::GetItemRectMin();
    DrawLabel(ImVec2(corner.x + style.FramePadding.x, corner.y + style.FramePadding.y), label);
    return pressed;
}

void HeadersRow(const std::string* headers, int first, int columns)
{
    ImGui::TableNextRow(ImGuiTableRowFlags_Headers, ImGui::TableGetHeaderRowHeight());
    for (int column = 0; column < columns; column++)
    {
        if (!ImGui::TableSetColumnIndex(column))
        {
            continue;
        }

        ImGui::PushID(column);
        if (column >= first &&
            Marked(headers[column - first]))
        {
            const std::string& header = headers[column - first];
            const ImVec2 position = ImGui::GetCursorScreenPos();
            ImGui::TableHeader("");
            const float edge = ImGui::TableGetCellBgRect(ImGui::GetCurrentTable(), column).Max.x;
            const ImVec2 size = ImGui::CalcTextSize(header.data(), header.data() + header.size());
            ImGui::RenderTextEllipsis(
                ImGui::GetWindowDrawList(),
                position,
                ImVec2(edge, position.y + size.y),
                edge,
                header.data(),
                header.data() + header.size(),
                &size);
        }
        else
        {
            ImGui::TableHeader(ImGui::TableGetColumnName(column));
        }

        ImGui::PopID();
    }
}
}
