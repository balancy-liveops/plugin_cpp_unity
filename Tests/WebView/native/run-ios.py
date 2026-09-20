#!/usr/bin/env python3
"""Build native WKWebView regression app and optionally run on a booted iOS Simulator."""
import argparse, pathlib, subprocess, plistlib, time, shutil
p=argparse.ArgumentParser();p.add_argument('--output',type=pathlib.Path,required=True);p.add_argument('--device');a=p.parse_args()
r=pathlib.Path(__file__).resolve().parents[3];out=a.output;app=out/'NativeTests.app';app.mkdir(parents=True,exist_ok=True)
sdk=subprocess.check_output(['xcrun','--sdk','iphonesimulator','--show-sdk-path'],text=True).strip()
subprocess.run(['xcrun','clang++','-std=c++17','-x','objective-c++','-fobjc-arc','-target','arm64-apple-ios17.0-simulator','-isysroot',sdk,'-framework','UIKit','-framework','WebKit','-framework','Foundation','-framework','CoreGraphics','-framework','QuartzCore','-I'+str(r/'WebView/Plugins/iOS'),str(pathlib.Path(__file__).parent/'ios/main.mm.txt'),str(r/'WebView/Plugins/iOS/BalancyWebView.mm'),'-o',str(app/'NativeTests')],check=True)
(app/'Info.plist').write_bytes(plistlib.dumps(dict(CFBundleIdentifier='com.balancy.webview.ios-tests',CFBundleExecutable='NativeTests',CFBundleName='NativeTests',CFBundlePackageType='APPL',CFBundleVersion='1',CFBundleShortVersionString='1.0',MinimumOSVersion='17.0',LSRequiresIPhoneOS=True,UILaunchScreen={})))
shutil.copy2(r/'WebView/Resources/balancy-webview-bridge.txt',app/'bridge.js')
packaged=app/'Balancy';packaged.mkdir(exist_ok=True)
shutil.copy2(r/'WebView/Resources/balancy-webview-bridge.txt',packaged/'balancy-webview-bridge.js')
(packaged/'packaged snapshot.txt').write_text('packaged-v1')
shutil.copy2(pathlib.Path(__file__).parent/'prefab-fixture.js.txt',app/'prefab-fixture.js')
subprocess.run(['codesign','--force','--sign','-',str(app)],check=True)
if a.device:
 subprocess.run(['xcrun','simctl','terminate',a.device,'com.balancy.webview.ios-tests'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 subprocess.run(['xcrun','simctl','install',a.device,str(app)],check=True)
 container=pathlib.Path(subprocess.check_output(['xcrun','simctl','get_app_container',a.device,'com.balancy.webview.ios-tests','data'],text=True).strip())
 result=container/'Documents/results.txt';result.unlink(missing_ok=True)
 subprocess.run(['xcrun','simctl','launch',a.device,'com.balancy.webview.ios-tests'],check=True)
 for i in range(240):
  if result.exists():
   text=result.read_text();(out/'results.txt').write_text(text);print(text);raise SystemExit(0 if 'RESULT failures=0 ' in text else 1)
  time.sleep(.5)
 raise SystemExit('Timed out: inspect simulator logs')
