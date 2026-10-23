#!/usr/bin/env bash
# Runs the runtime compiled into the sample library under an X server, which is what
# sample-native.yml's run job does, and what nothing else in this repository does: an app's pixel
# tests were the first to run a change to it, after it had been tagged.
#
#   bash sample/native/run/run.sh <build directory>
#
# Needs Xvfb, xvfb-run and the Mesa software rasteriser. The build directory is one configured
# with -DSAMPLE_RUN=ON, so it holds sample_run beside libsample_ui.so.
set -euo pipefail

build="$1"
runner="$build/sample_run"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
export LIBGL_ALWAYS_SOFTWARE=1

echo "== a window opened, rested through a second of frames, and closed"
xvfb-run -a --server-args="-screen 0 640x480x24" "$runner" turns

echo "== the X server going is heard before Xlib ends the process"
display=97
Xvfb ":$display" -screen 0 640x480x24 > "$work/xvfb.log" 2>&1 &
xvfb=$!
for _ in $(seq 50); do
  [ -S "/tmp/.X11-unix/X$display" ] && break
  sleep 0.1
done

marker="$work/lost"
DISPLAY=":$display" "$runner" lost "$marker" > "$work/run.log" 2>&1 &
run=$!
for _ in $(seq 100); do
  grep -q listening "$work/run.log" && break
  sleep 0.1
done

kill -TERM "$xvfb"
status=0
wait "$run" || status=$?
wait "$xvfb" 2> /dev/null || true
cat "$work/run.log"

# 1 is Xlib's exit once its handler has returned, which is the runtime handing over to it
if [ "$status" -ne 1 ]; then
  echo "::error::sample_run ended with $status, not Xlib's 1"
  exit 1
fi

if [ ! -f "$marker" ]; then
  echo "::error::the X server went and OnDisplayLost's callback was not called"
  exit 1
fi

echo "heard"
