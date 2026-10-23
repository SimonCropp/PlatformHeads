cmake_minimum_required(VERSION 3.24)
project(sample_ui LANGUAGES C CXX)

# The smallest head there is, built the way an app's is, so that this repository's own workflow
# run exercises the module, the container build and the checks. An app fetches the module by tag
# with FetchContent; this one is beside it.
include(${CMAKE_CURRENT_SOURCE_DIR}/../../cmake/PlatformHeads.cmake)

platformheads_defaults()
platformheads_fetch(FORMATS PNG)
platformheads_add_library(sample_ui SOURCES src/sample.cpp)

# A program that runs the library's runtime under an X server, for sample-native.yml's run job.
# Off for build-native, which builds and checks the library alone.
option(SAMPLE_RUN "Build sample_run, which runs the runtime" OFF)
if(SAMPLE_RUN)
  add_executable(sample_run run/run.cpp)
  target_link_libraries(sample_run PRIVATE sample_ui)
endif()
