#!/bin/bash
# Simple test to verify we can compile Cocoa/WebKit code

set -e

echo "Testing compilation..."

# Test basic compilation with a simple test file
clang++ -std=c++11 -framework Cocoa -framework WebKit testcompile.mm -o test_webview

if [ $? -eq 0 ]; then
    echo "Basic compilation test succeeded"
    rm -f test_webview # Clean up
else
    echo "Basic compilation test failed"
    exit 1
fi

echo "All tests passed"
