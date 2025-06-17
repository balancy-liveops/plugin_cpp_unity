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

# Add 16KB alignment for Android 15+ compatibility - linker flags only
set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -Wl,-z,max-page-size=16384 -Wl,-z,separate-code")
set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -Wl,-z,max-page-size=16384 -Wl,-z,separate-code")
set(ANDROID_LD_FLAGS "-Wl,-z,max-page-size=16384 -Wl,-z,separate-code")

# Create the Android WebView library
add_library(BalancyWebViewAndroid ${LIBRARY_TYPE}
    BalancyWebViewJNI.cpp
)

# Link required Android libraries
target_link_libraries(BalancyWebViewAndroid PUBLIC
    log
    android
)

# Add 16KB alignment for Android 15+ compatibility - multiple approaches
target_link_options(BalancyWebViewAndroid PRIVATE -Wl,-z,max-page-size=16384 -Wl,-z,separate-code)
target_link_options(BalancyWebViewAndroid PRIVATE "LINKER:-z,max-page-size=16384")
target_link_options(BalancyWebViewAndroid PRIVATE "LINKER:-z,separate-code")
set_target_properties(BalancyWebViewAndroid PROPERTIES LINK_FLAGS "-Wl,-z,max-page-size=16384 -Wl,-z,separate-code")

# Set output name and properties
set_target_properties(BalancyWebViewAndroid PROPERTIES
    OUTPUT_NAME "BalancyWebViewAndroid"
)

# Include directories for JNI headers
target_include_directories(BalancyWebViewAndroid PRIVATE
    ${ANDROID_NDK}/sources/android/native_app_glue
)

message(STATUS "Android WebView library configured successfully")
