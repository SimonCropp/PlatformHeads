/*
 * ImGui's draw lists through rlgl: see runtime.h. The ImGui backend here is deliberately minimal.
 * A head slices every frame to what is visible on the managed side, so there is no scroll state to
 * keep in sync, and what is left of a backend is honouring texture requests and turning ImDrawData
 * into rlgl calls.
 */
#include "platformheads/runtime.h"

#include "imgui.h"
#include "rlgl.h"

namespace
{
/* ---- texture protocol (ImGuiBackendFlags_RendererHasTextures) ---- */

int FormatOf(const ImTextureData* texture)
{
    return texture->Format == ImTextureFormat_Alpha8
        ? RL_PIXELFORMAT_UNCOMPRESSED_GRAYSCALE
        : RL_PIXELFORMAT_UNCOMPRESSED_R8G8B8A8;
}

void UpdateTexture(ImTextureData* texture)
{
    if (texture->Status == ImTextureStatus_WantCreate)
    {
        const unsigned int id = rlLoadTexture(texture->GetPixels(), texture->Width, texture->Height, FormatOf(texture), 1);
        texture->SetTexID(static_cast<ImTextureID>(id));
        texture->SetStatus(ImTextureStatus_OK);
        return;
    }

    if (texture->Status == ImTextureStatus_WantUpdates)
    {
        /* Re-uploading the whole texture keeps the source rows contiguous, which a sub rectangle
         * of a wider buffer is not. Atlas updates only happen when new glyphs appear, so the extra
         * bandwidth is irrelevant next to the copy that avoiding it would need. */
        rlUpdateTexture(
            static_cast<unsigned int>(texture->TexID),
            0,
            0,
            texture->Width,
            texture->Height,
            FormatOf(texture),
            texture->GetPixels());
        texture->SetStatus(ImTextureStatus_OK);
        return;
    }

    if (texture->Status == ImTextureStatus_WantDestroy)
    {
        rlUnloadTexture(static_cast<unsigned int>(texture->TexID));
        texture->SetTexID(ImTextureID_Invalid);
        texture->SetStatus(ImTextureStatus_Destroyed);
    }
}

void RenderTriangles(
    unsigned int count,
    unsigned int indexStart,
    unsigned int vertexOffset,
    const ImVector<ImDrawIdx>& indices,
    const ImVector<ImDrawVert>& vertices,
    ImTextureID textureId)
{
    if (count < 3)
    {
        return;
    }

    rlBegin(RL_TRIANGLES);
    rlSetTexture(static_cast<unsigned int>(textureId));

    for (unsigned int index = 0; index <= count - 3; index += 3)
    {
        for (unsigned int corner = 0; corner < 3; corner++)
        {
            /* Plus the command's own vertex offset. ImDrawIdx is sixteen bits, so a draw list that
             * runs past 65535 vertices - a maximised 4K window of dense long lines gets there - is
             * split by ImGui into commands whose indices restart from a base recorded here.
             * Without adding it the indices wrapped and the panes drew scrambled, and in a release
             * build, with IM_ASSERT compiled out, nothing said so. */
            const ImDrawVert& vertex = vertices[vertexOffset + indices[indexStart + index + corner]];
            const ImColor colour = ImColor(vertex.col);
            rlColor4f(colour.Value.x, colour.Value.y, colour.Value.z, colour.Value.w);
            rlTexCoord2f(vertex.uv.x, vertex.uv.y);
            rlVertex2f(vertex.pos.x, vertex.pos.y);
        }
    }

    rlEnd();
}
}

namespace ph
{
void RenderDrawData(ImDrawData* drawData)
{
    if (drawData->Textures != nullptr)
    {
        for (ImTextureData* texture : *drawData->Textures)
        {
            if (texture->Status != ImTextureStatus_OK)
            {
                UpdateTexture(texture);
            }
        }
    }

    rlDrawRenderBatchActive();
    rlDisableBackfaceCulling();

    /* The height of what is being drawn to, which is not the window's for a capture: that draws
     * to a render texture of its own size, and BeginTextureMode changes the target without
     * changing what GetScreenHeight reports. */
    const float height = drawData->DisplaySize.y;
    for (int list = 0; list < drawData->CmdListsCount; list++)
    {
        const ImDrawList* commands = drawData->CmdLists[list];
        for (const ImDrawCmd& command : commands->CmdBuffer)
        {
            if (command.UserCallback != nullptr)
            {
                command.UserCallback(commands, &command);
                continue;
            }

            /* ImGui clips in framebuffer space with the origin top left; rlgl scissors from the
             * bottom left. */
            rlEnableScissorTest();
            rlScissor(
                static_cast<int>(command.ClipRect.x),
                static_cast<int>(height - command.ClipRect.w),
                static_cast<int>(command.ClipRect.z - command.ClipRect.x),
                static_cast<int>(command.ClipRect.w - command.ClipRect.y));

            RenderTriangles(
                command.ElemCount,
                command.IdxOffset,
                command.VtxOffset,
                commands->IdxBuffer,
                commands->VtxBuffer,
                command.GetTexID());

            rlDrawRenderBatchActive();
        }
    }

    rlSetTexture(0);
    rlDisableScissorTest();
    rlEnableBackfaceCulling();
}
}
