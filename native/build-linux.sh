#!/usr/bin/env bash
# Builds build/lib$LIBRARY.so from the CMake project in $NATIVE, inside a manylinux_2_28
# container, whose glibc 2.28 is the floor the library then needs. A library records the glibc
# symbol versions of the headers it was compiled against, and the loader refuses it anywhere
# older: built on an Ubuntu 24.04 runner a head's library needed GLIBC_2.38, and would not load
# on Ubuntu 22.04, Debian 12, or RHEL 8 or 9.
#
# This is what the build-native workflow runs. To run it by hand, from an app's repository root:
#
#   docker run --rm -v "$PWD:/src" -w /src -e LIBRARY=myapp_ui -e NATIVE=native \
#     "quay.io/pypa/manylinux_2_28_$(uname -m)" bash /path/to/build-linux.sh
set -euo pipefail

: "${LIBRARY:?LIBRARY is the bare name of the library, such as myapp_ui}"
NATIVE="${NATIVE:-native}"

# What raylib's bundled GLFW builds against, for both its X11 and Wayland backends.
dnf install -y \
  git pkgconfig \
  libX11-devel libXext-devel libXrandr-devel libXi-devel libXcursor-devel libXinerama-devel \
  mesa-libGL-devel wayland-devel libxkbcommon-devel

# A head's CMakeLists needs CMake 3.24, newer than the distribution's own. The image carries
# current releases through pipx, so these are only installed where it does not.
export PATH="$HOME/.local/bin:$PATH"
for tool in cmake ninja; do
  if ! command -v "$tool" > /dev/null 2>&1; then
    pipx install "$tool"
  fi
done

# LEGACY links libGL.so.1, which every GL driver ships. This distribution's CMake otherwise
# prefers the GLVND split and links libOpenGL.so.0, which a machine with only libgl1 lacks.
cmake -S "$NATIVE" -B build -G Ninja -DCMAKE_BUILD_TYPE=Release -DOpenGL_GL_PREFERENCE=LEGACY
cmake --build build --config Release

# Here and not in the workflow's collect step: the build directory belongs to this container's
# root, and the runner cannot rewrite what is in it.
strip "build/lib$LIBRARY.so"
