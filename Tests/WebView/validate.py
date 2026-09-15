#!/usr/bin/env python3
"""Run the persistent-state NUnit fixture without opening Unity.
Optional --unity-editor compiles the SDK against the project's generated Unity references.
"""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--project', type=Path, default=ROOT.parents[1])
parser.add_argument('--unity-editor', type=Path, help='Path to Unity.app (macOS)')
parser.add_argument('--nunit', type=Path)
parser.add_argument('--mcs', default=shutil.which('mcs') or '/Library/Frameworks/Mono.framework/Commands/mcs')
parser.add_argument('--mono', default=shutil.which('mono') or '/Library/Frameworks/Mono.framework/Commands/mono')
args = parser.parse_args()
nunit = args.nunit or next(args.project.glob('Library/PackageCache/com.unity.ext.nunit*/net472/unity-custom/nunit.framework.dll'))
with tempfile.TemporaryDirectory(prefix='balancy-webview-tests-') as temporary:
    output = Path(temporary)
    runner = output / 'Runner.cs'
    runner.write_text('''using System;
class Runner {
 static int Main() {
  int passed = 0, failed = 0;
  var fixture = new Balancy.Tests.PersistentWebViewTests();
  foreach (var method in fixture.GetType().GetMethods()) {
   if (!Attribute.IsDefined(method, typeof(NUnit.Framework.TestAttribute))) continue;
   fixture.SetUp();
   try { method.Invoke(fixture, null); Console.WriteLine("PASS " + method.Name); passed++; }
   catch (Exception e) { Console.WriteLine("FAIL " + method.Name + ": " + (e.InnerException ?? e)); failed++; }
   finally { fixture.TearDown(); }
  }
  Console.WriteLine(passed + " passed, " + failed + " failed"); return failed > 0 ? 1 : 0;
 }
}''')
    shutil.copy2(nunit, output / nunit.name)
    subprocess.run([args.mcs, '-r:' + str(nunit), '-out:' + str(output / 'tests.exe'),
                    str(ROOT / 'WebView/Scripts/PersistentViewState.cs'),
                    str(ROOT / 'Tests/EditMode/PersistentWebViewTests.cs'), str(runner)], check=True)
    subprocess.run([args.mono, str(output / 'tests.exe')], check=True)
    if args.unity_editor:
        runtime = args.unity_editor / 'Contents/Resources/Scripting/DotNetSdk'
        compiler = next(runtime.glob('sdk/*/Roslyn/bincore/csc.dll'))
        namespace = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
        def compile_assembly(name, defines, sources, refs):
            rsp = output / (name + '.rsp')
            switches = ['/nologo', '/target:library', '/langversion:9.0', '/unsafe', '/nostdlib+',
                        '/out:' + str(output / (name + '.dll')), '/define:' + defines]
            rsp.write_text('\n'.join('"' + item + '"' for item in switches + ['/reference:' + str(r) for r in refs] + sources))
            result = subprocess.run([str(runtime / 'dotnet'), str(compiler), '@' + str(rsp)],
                cwd=args.project, env={**os.environ, 'DOTNET_CLI_HOME': str(output)}, capture_output=True, text=True)
            if result.returncode:
                print(result.stdout + result.stderr)
                raise SystemExit(result.returncode)
            print('COMPILE PASS ' + name)
        for name in ['Balancy.WebView', 'Balancy']:
            tree = ET.parse(args.project / (name + '.csproj'))
            defines = tree.find('.//m:DefineConstants', namespace).text
            sources = [str(args.project / item.attrib['Include'].replace('\\', '/')) for item in tree.findall('.//m:Compile', namespace)]
            refs = [str(args.project / item.text.replace('\\', '/')) for item in tree.findall('.//m:HintPath', namespace)]
            if name == 'Balancy.WebView':
                sources += [str(ROOT / 'WebView/Scripts' / file) for file in ['PersistentViewState.cs', 'AssemblyInfo.cs']]
            else:
                refs = [ref for ref in refs if not ref.endswith('/Balancy.WebView.dll')]
                refs.append(str(output / 'Balancy.WebView.dll'))
            sources = list(dict.fromkeys(sources))
            compile_assembly(name, defines, sources, refs)
            if name == 'Balancy.WebView':
                for platform in ['UNITY_IOS', 'UNITY_ANDROID', 'UNITY_WEBGL', 'UNITY_STANDALONE_OSX', 'UNITY_STANDALONE_WIN']:
                    compile_assembly(platform, platform + ';UNITY_5_3_OR_NEWER;ENABLE_LEGACY_INPUT_MANAGER', sources, refs)
