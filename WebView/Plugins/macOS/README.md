# Balancy WebView macOS Native Plugin

This folder contains the native macOS implementation for the Balancy WebView plugin.

## Required Files

- `BalancyWebViewMac.h` - Header file for the native implementation
- `BalancyWebViewMac.mm` - Implementation file (Objective-C++)
- `libBalancyWebViewMac.dylib` - Compiled dynamic library

## Building the Native Library

The dynamic library needs to be compiled from the Objective-C++ source files. Follow these steps to build the library:

### Prerequisites

- Xcode Command Line Tools
- macOS 10.13 or higher

### Build Steps

1. Open Terminal and navigate to this directory
2. Run the following command to compile the dynamic library:

```bash
clang++ -std=c++11 -shared -framework Cocoa -framework WebKit \
  -o libBalancyWebViewMac.dylib BalancyWebViewMac.mm \
  -install_name @rpath/libBalancyWebViewMac.dylib
```

3. Verify that the `libBalancyWebViewMac.dylib` file was created in this directory

## Important Notes

- The library must be built specifically for macOS and the Unity Editor
- Ensure that the library has the correct framework dependencies (WebKit and Cocoa)
- The library needs to be compatible with both Intel and Apple Silicon Macs
- For universal binary support, add `-arch x86_64 -arch arm64` to the build command

## Usage

Once the library is built, it will be automatically loaded by Unity when running in the Editor on macOS. The C# code accesses the native functions through P/Invoke declarations.

## Troubleshooting

If you encounter issues with the library:

1. Check that the dynamic library is properly built and located in this folder
2. Verify that the library is compatible with your macOS version
3. Ensure that all required frameworks are properly linked
4. Check the Unity console for any error messages related to loading the native library
