#
# What every native head built on raylib and Dear ImGui has to get right about its build, in one
# place, so that a fix made for one app is not waiting to be found again in the next.
#
#   include(FetchContent)
#   FetchContent_Declare(
#     platformheads
#     GIT_REPOSITORY https://github.com/SimonCropp/PlatformHeads.git
#     GIT_TAG <tag>
#     GIT_SHALLOW ON)
#   FetchContent_MakeAvailable(platformheads)
#   include(${platformheads_SOURCE_DIR}/cmake/PlatformHeads.cmake)
#
#   platformheads_defaults()
#   platformheads_fetch(FORMATS PNG)
#   platformheads_add_library(myapp_ui SOURCES src/myapp.cpp)
#
include_guard(GLOBAL)
include(FetchContent)

# Pinned here, so every app that takes a tag of this file builds against the same two. Set
# either before platformheads_fetch to try another.
if(NOT DEFINED PLATFORMHEADS_RAYLIB_TAG)
  set(PLATFORMHEADS_RAYLIB_TAG 6.0)
endif()
if(NOT DEFINED PLATFORMHEADS_IMGUI_TAG)
  set(PLATFORMHEADS_IMGUI_TAG v1.92.9b)
endif()

#
# The language standards and the visibility every head is built with. A macro and not a
# function, because these are ordinary variables and have to be set in the caller's scope.
#
macro(platformheads_defaults)
  set(CMAKE_C_STANDARD 11)
  set(CMAKE_CXX_STANDARD 17)
  set(CMAKE_CXX_STANDARD_REQUIRED ON)
  set(CMAKE_POSITION_INDEPENDENT_CODE ON)
  set(CMAKE_C_VISIBILITY_PRESET hidden)
  set(CMAKE_CXX_VISIBILITY_PRESET hidden)
  set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

  if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "" FORCE)
  endif()
endmacro()

#
# Fetches raylib and Dear ImGui, configured for a head.
#
#   FORMATS  the picture formats raylib is to read and write, by the name its SUPPORT_FILEFORMAT_
#            option ends in: PNG, JPG, BMP, GIF and so on. Every other is left out.
#
# Fetched and not vendored as submodules. Nothing in a normal dotnet build touches an app's
# native folder: the built binaries are committed beside the head that loads them, so only the
# native build needs either, and submodules would put a recursive clone on every checkout to
# serve a path almost nobody takes.
#
function(platformheads_fetch)
  cmake_parse_arguments(PARSE_ARGV 0 arg "" "" "FORMATS")

  # raylib, static so the shipped artifact is a single file for each runtime identifier.
  set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
  set(BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
  set(BUILD_GAMES OFF CACHE BOOL "" FORCE)
  set(CUSTOMIZE_BUILD ON CACHE BOOL "" FORCE)

  # raylib 6.0 reads every SUPPORT_ flag in its config.h into an option that defaults to ON, the
  # ones config.h defines as 0 included (raysan5/raylib#5844, fixed after 6.0), and
  # CUSTOMIZE_BUILD skips config.h's own values. So each of those is on unless it is switched off
  # here.
  #
  # SUPPORT_CUSTOM_FRAME_CONTROL was: EndDrawing then neither puts the frame on the screen, reads
  # input nor waits for the next frame, so a head that leaves those to it has a window that stays
  # blank, takes no keys and spins a core. A capture draws into a texture and never gets that
  # far, so pixel snapshots pass throughout. It took six weeks to be noticed in one app and was
  # then still there in the next.
  #
  # SUPPORT_BUSY_WAIT_LOOP was on the same way, and WaitTime then spins through the whole of
  # every wait. SUPPORT_PARTIALBUSY_WAIT_LOOP is on by raylib's own choice, and spins through the
  # last part of each, for timing precision no head needs.
  #
  # SUPPORT_SCREEN_CAPTURE has F12 save a screenshot into the working directory, which is
  # wherever the app was started from.
  foreach(flag IN ITEMS
      SUPPORT_CUSTOM_FRAME_CONTROL
      SUPPORT_BUSY_WAIT_LOOP
      SUPPORT_PARTIALBUSY_WAIT_LOOP
      SUPPORT_SCREEN_CAPTURE
      SUPPORT_GPU_SKINNING
      SUPPORT_MODULE_RAUDIO
      SUPPORT_MODULE_RMODELS)
    set(${flag} OFF CACHE BOOL "" FORCE)
  endforeach()

  # Every format raylib 6.0 has an option for. The ones asked for are on and the rest are off,
  # those config.h has off included, for the reason above.
  foreach(format IN ITEMS
      PNG BMP TGA JPG GIF QOI PSD DDS HDR PIC PNM KTX ASTC PKM PVR BDF FLAC)
    if(format IN_LIST arg_FORMATS)
      set(SUPPORT_FILEFORMAT_${format} ON CACHE BOOL "" FORCE)
    else()
      set(SUPPORT_FILEFORMAT_${format} OFF CACHE BOOL "" FORCE)
    endif()
  endforeach()

  FetchContent_Declare(
    raylib
    GIT_REPOSITORY https://github.com/raysan5/raylib.git
    GIT_TAG ${PLATFORMHEADS_RAYLIB_TAG}
    GIT_SHALLOW ON)

  # imgui has no CMake build of its own, so it is fetched as plain sources, which
  # platformheads_add_library compiles into the head.
  FetchContent_Declare(
    imgui
    GIT_REPOSITORY https://github.com/ocornut/imgui.git
    GIT_TAG ${PLATFORMHEADS_IMGUI_TAG}
    GIT_SHALLOW ON)

  FetchContent_MakeAvailable(raylib)
  FetchContent_MakeAvailable(imgui)

  set(imgui_SOURCE_DIR ${imgui_SOURCE_DIR} PARENT_SCOPE)
endfunction()

#
# The head's shared library: its own sources and Dear ImGui's, linked against raylib.
#
#   platformheads_add_library(<name> SOURCES <file>...)
#
# The name is the bare one the managed side probes for. It comes out as lib<name>.so,
# lib<name>.dylib or <name>.dll.
#
function(platformheads_add_library name)
  cmake_parse_arguments(PARSE_ARGV 1 arg "" "" "SOURCES")

  add_library(${name} SHARED
    ${arg_SOURCES}
    ${imgui_SOURCE_DIR}/imgui.cpp
    ${imgui_SOURCE_DIR}/imgui_draw.cpp
    ${imgui_SOURCE_DIR}/imgui_tables.cpp
    ${imgui_SOURCE_DIR}/imgui_widgets.cpp)

  target_include_directories(${name} PRIVATE include ${imgui_SOURCE_DIR})

  # ImGui's characters are sixteen bits unless told otherwise, and everything past the basic
  # plane, which is most emoji and the rarer Han characters, is then the replacement character
  # before any font is asked for it. Set on the target, so imgui's own sources are compiled with
  # it too: it changes a type they share with the head's.
  target_compile_definitions(${name} PRIVATE IMGUI_USE_WCHAR32)
  target_link_libraries(${name} PRIVATE raylib)

  # Only the libraries something is actually called in are recorded as needed. raylib hands on
  # everything CMake's FindX11 found, which includes libSM and libICE wherever their headers are
  # installed, and nothing in a head or in raylib calls either. A library recorded as needed has
  # to be on the machine for this one to load at all, and a stock Ubuntu has neither of those
  # two. Ubuntu's own compiler passes this by default; the manylinux container the library is
  # built in for release does not.
  if(UNIX AND NOT APPLE)
    target_link_options(${name} PRIVATE LINKER:--as-needed)
  endif()

  # The managed side probes for a bare name, so it is the same on every platform apart from the
  # platform's own prefix and extension.
  set_target_properties(${name} PROPERTIES
    OUTPUT_NAME "${name}"
    C_VISIBILITY_PRESET hidden
    CXX_VISIBILITY_PRESET hidden)

  if(MSVC)
    target_compile_options(${name} PRIVATE /W3 /permissive-)
    target_compile_definitions(${name} PRIVATE _CRT_SECURE_NO_WARNINGS)
  else()
    target_compile_options(${name} PRIVATE -Wall -Wextra -Wno-unused-parameter)
  endif()

  if(APPLE)
    set_target_properties(${name} PROPERTIES SUFFIX ".dylib")
  endif()
endfunction()
