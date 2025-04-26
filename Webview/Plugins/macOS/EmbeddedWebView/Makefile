# Makefile for EmbeddedWebView Plugin

# Compiler and flags
CC = clang
CFLAGS = -Wall -Werror -O2 -fPIC -fobjc-arc
LDFLAGS = -bundle -framework Cocoa -framework WebKit

# Output file
TARGET = EmbeddedWebView

# Source files
SOURCES = WebViewPlugin.m

# Build rules
all: $(TARGET)

$(TARGET): $(SOURCES)
	mkdir -p Contents/MacOS
	$(CC) $(CFLAGS) $(LDFLAGS) -o Contents/MacOS/$(TARGET) $(SOURCES)

clean:
	rm -rf Contents/MacOS/$(TARGET)

.PHONY: all clean
