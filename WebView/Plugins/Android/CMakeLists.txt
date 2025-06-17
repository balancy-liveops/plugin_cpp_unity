cmake_minimum_required(VERSION 3.26)
project(BalancyWebViewAndroid VERSION 1.0.0)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_VERBOSE_MAKEFILE ON)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_BUILD_TYPE Release)

# Android configuration
if(NOT ANDROID)
    message(FATAL_ERROR "This CMakeLists.txt is specifically for Android builds")
endif()

set(CMAKE_SYSTEM_NAME Android)
set(LIBRARY_TYPE SHARED)
add_definitions(-D__ANDROID__=1 -DPLATFORM_ANDROID=1)

# Create the Android WebView library
add_library(BalancyWebViewAndroid ${LIBRARY_TYPE}
    BalancyWebViewJNI.cpp
)

# Link required Android libraries
target_link_libraries(BalancyWebViewAndroid PUBLIC
    log
    android
)

# Set output name and properties
set_target_properties(BalancyWebViewAndroid PROPERTIES
    OUTPUT_NAME "BalancyWebViewAndroid"
)

# Include directories for JNI headers
target_include_directories(BalancyWebViewAndroid PRIVATE
    ${ANDROID_NDK}/sources/android/native_app_glue
)

message(STATUS "Android WebView library configured successfully")
