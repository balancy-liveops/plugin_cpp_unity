#!/usr/bin/env python3
"""Build a real-WebView test APK without Gradle, then optionally run on an already booted emulator."""
import argparse, os, pathlib, subprocess, zipfile, time, shutil
p=argparse.ArgumentParser(); p.add_argument('--sdk',type=pathlib.Path,required=True);p.add_argument('--java-home',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);p.add_argument('--serial');p.add_argument('--aar',type=pathlib.Path,help='Test the published AAR instead of compiling plugin sources (requires Java 8 bytecode)');p.add_argument('--d8-jar',type=pathlib.Path,help='Use an older R8/D8 jar to check consumer toolchain compatibility');p.add_argument('--core-libs',type=pathlib.Path,help='Directory with ABI/libBalancyCore.so; enables JNI VM regression checks (requires --aar)');a=p.parse_args()
if a.core_libs and not a.aar:p.error('--core-libs requires --aar')
r=pathlib.Path(__file__).resolve().parents[3];src=pathlib.Path(__file__).resolve().parent/'android';out=a.output;out.mkdir(parents=True,exist_ok=True)
classes=out/'classes';classes.mkdir(exist_ok=True);bt=a.sdk/'build-tools/35.0.0';android=a.sdk/'platforms/android-35/android.jar';java=a.java_home/'bin'
env={**os.environ,'JAVA_HOME':str(a.java_home)}
def run(args,**kw): return subprocess.run(list(map(str,args)),check=True,env=env,**kw)
sources=out/'sources';sources.mkdir(exist_ok=True)
for fixture in src.rglob('*.java.txt'):
 target=sources/fixture.relative_to(src).with_suffix('');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(fixture,target)
plugin_inputs=[r/'WebView/WVAndroidLib.androidlib/AndroidProject/app/src/main/java/com/balancy/webview/BalancyWebViewPlugin.java']
classpath=str(android);dex_inputs=[]
if a.aar:
 import io, struct
 with zipfile.ZipFile(a.aar) as archive:
  jar_data=archive.read('classes.jar')
 with zipfile.ZipFile(io.BytesIO(jar_data)) as jar:
  versions={struct.unpack('>H',jar.read(name)[6:8])[0] for name in jar.namelist() if name.endswith('.class')}
  if not versions or max(versions)>52:raise SystemExit(f'AAR requires newer than Java 8: class versions {versions}')
 plugin_jar=out/'plugin.jar';plugin_jar.write_bytes(jar_data)
 classpath+=os.pathsep+str(plugin_jar);plugin_inputs=[];dex_inputs=[plugin_jar]
run([java/'javac','--release','8','-classpath',classpath,'-d',classes,*sources.rglob('*.java'),*plugin_inputs])
d8=[java/'java','-cp',a.d8_jar,'com.android.tools.r8.D8'] if a.d8_jar else [bt/'d8']
run([*d8,'--min-api','22','--lib',android,'--output',out,*classes.rglob('*.class'),*dex_inputs])
apk=out/'native-tests.apk';run([bt/'aapt2','link','-I',android,'--manifest',src/'AndroidManifest.xml','-o',apk])
with zipfile.ZipFile(apk,'a') as f:
 f.write(out/'classes.dex','classes.dex')
 if a.core_libs:
  libs=list(a.core_libs.glob('*/libBalancyCore.so'))
  if not libs:raise SystemExit('No ABI/libBalancyCore.so files found')
  for lib in libs:f.write(lib,'lib/'+str(lib.relative_to(a.core_libs)))
 f.write(r/'WebView/Resources/balancy-webview-bridge.txt','assets/bridge.js')
 f.write(src.parent/'prefab-fixture.js.txt','assets/prefab-fixture.js')
key=out/'test.keystore'
if not key.exists():run([java/'keytool','-genkeypair','-keystore',key,'-storepass','android','-keypass','android','-alias','test','-dname','CN=Local SDK Tests','-keyalg','RSA','-validity','30'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
run([bt/'apksigner','sign','--ks',key,'--ks-pass','pass:android',apk])
if a.serial:
 adb=[a.sdk/'platform-tools/adb','-s',a.serial]
 subprocess.run(list(map(str,[*adb,'uninstall','com.balancy.webview.tests'])),stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 run([*adb,'install',apk],stdout=subprocess.DEVNULL)
 run([*adb,'shell','am','force-stop','com.balancy.webview.tests'])
 run([*adb,'shell','run-as','com.balancy.webview.tests','rm','-f','files/results.txt'])
 run([*adb,'shell','am','start','-n','com.balancy.webview.tests/.MainActivity',*(['--ez','testNativeRuntime','true'] if a.core_libs else [])])
 for i in range(120):
  result=subprocess.run(list(map(str,[*adb,'shell','run-as','com.balancy.webview.tests','cat','files/results.txt'])),capture_output=True,text=True)
  if result.returncode==0 and 'RESULT ' in result.stdout:
   (out/'results.txt').write_text(result.stdout);print(result.stdout);raise SystemExit(0 if 'RESULT failures=0 ' in result.stdout else 1)
  time.sleep(.5)
 raise SystemExit('Timed out: inspect adb logcat')
print(apk)
