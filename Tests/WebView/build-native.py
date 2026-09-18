#!/usr/bin/env python3
"""Build/check WebView plugins. Set JAVA_HOME and pass --android-jar; Xcode selected by xcrun.
Use --update to copy the Android AAR and macOS dylib into the SDK after successful checks.
"""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--android-jar', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path, help='Empty output directory')
parser.add_argument('--update', action='store_true')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
classes = args.output / 'classes'
classes.mkdir()  # refuse to reuse stale bytecode
java = Path(os.environ['JAVA_HOME']) / 'bin'
subprocess.run([str(java / 'javac'), '--release', '8', '-classpath', str(args.android_jar), '-d', str(classes),
 str(ROOT / 'WebView/WVAndroidLib.androidlib/AndroidProject/app/src/main/java/com/balancy/webview/BalancyWebViewPlugin.java')], check=True)
jar = args.output / 'classes.jar'
subprocess.run([str(java / 'jar'), 'cf', str(jar), '-C', str(classes), '.'], check=True)
aar = ROOT / 'WebView/Plugins/Android/balancywebview.aar'
with zipfile.ZipFile(aar) as source, zipfile.ZipFile(args.output / aar.name, 'w') as target:
    for entry in source.infolist():
        target.writestr(entry, jar.read_bytes() if entry.filename == 'classes.jar' else source.read(entry.filename))
dylib = ROOT / 'WebView/Plugins/macOS/libBalancyWebViewMac.dylib'
subprocess.run(['xcrun', 'clang++', '-std=c++17', '-fobjc-arc', '-dynamiclib', '-arch', 'arm64', '-arch', 'x86_64',
 '-mmacosx-version-min=11.0', '-framework', 'Cocoa', '-framework', 'WebKit', '-framework', 'Metal', '-framework', 'QuartzCore',
 '-Wl,-undefined,dynamic_lookup', '-install_name', '@rpath/' + dylib.name, '-o', str(args.output / dylib.name),
 str(ROOT / 'WebView/Plugins/macOS/BalancyWebviewMac.mm')], check=True)
subprocess.run(['codesign', '--force', '--sign', '-', str(args.output / dylib.name)], check=True)
sdk = subprocess.check_output(['xcrun', '--sdk', 'iphoneos', '--show-sdk-path'], text=True).strip()
subprocess.run(['xcrun', 'clang++', '-std=c++17', '-fobjc-arc', '-target', 'arm64-apple-ios13.0', '-isysroot', sdk,
 '-fsyntax-only', str(ROOT / 'WebView/Plugins/iOS/BalancyWebView.mm')], check=True)
if args.update:
    shutil.copy2(args.output / aar.name, aar)
    shutil.copy2(args.output / dylib.name, dylib)
print('Native compilation checks passed. Device execution is still required.')
