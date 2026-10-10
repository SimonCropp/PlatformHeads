cmake_minimum_required(VERSION 3.24)
project(sample_ui LANGUAGES C CXX)

# The smallest head there is, built the way an app's is, so that this repository's own workflow
# run exercises the module, the container build and the checks. An app fetches the module by tag
# with FetchContent; this one is beside it.
include(${CMAKE_CURRENT_SOURCE_DIR}/../../cmake/PlatformHeads.cmake)

platformheads_defaults()
platformheads_fetch(FORMATS PNG)
platformheads_add_library(sample_ui SOURCES src/sample.cpp)
