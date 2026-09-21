#!/usr/bin/env python3
"""Compile production C# against Unity 2021 APIs without starting the editor.

This checks API availability only. It does not replace Unity import, IL2CPP,
player builds, native linking or runtime tests. Use an output outside Assets.
"""
from pathlib import Path
import subprocess
import argparse
parser = argparse.ArgumentParser()
parser.add_argument('--unity-contents', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
a = parser.parse_args()
u=a.unity_contents
sdk=Path(__file__).resolve().parents[2]
out=a.output
out.mkdir(parents=True, exist_ok=True)
failed=False
base=[str(u/'NetCoreRuntime/dotnet'),str(u/'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:library','-nostdlib','-unsafe']
refs=['-r:'+str(p) for p in (u/'Managed').rglob('*.dll')]+['-r:'+str(p) for p in (u/'UnityReferenceAssemblies/unity-4.8-api').glob('*.dll')]
refs += ['-r:'+str(u/'Resources/PackageManager/ProjectTemplates/libcache/com.unity.template.3d-8.1.3/ScriptAssemblies'/n) for n in ['UnityEngine.UI.dll','Unity.TextMeshPro.dll']]
refs += ['-r:'+str(u/'UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll')]
for platform,defs in [('editor','UNITY_EDITOR,UNITY_EDITOR_OSX,UNITY_STANDALONE_OSX'),('android','UNITY_ANDROID'),('ios','UNITY_IOS,UNITY_IPHONE')]:
 defines='-define:UNITY_2021_3_OR_NEWER,UNITY_2021_3,UNITY_2021_2_OR_NEWER,UNITY_2020_1_OR_NEWER,UNITY_2019_1_OR_NEWER,ENABLE_IL2CPP,'+defs
 (out/platform).mkdir(exist_ok=True)
 for name,folder in [('Balancy.WebView','WebView'),('Balancy',''),('Balancy.Editor','Editor'),('Balancy.UI','UI'),('Balancy.CheatPanel','CheatPanel')]:
  if name.endswith('.Editor') and platform!='editor':continue
  files=[]
  for p in (sdk/folder).rglob('*.cs'):
   if any(q.name in ['Tests','Editor','UI','CheatPanel','WebView'] for q in p.relative_to(sdk/folder).parents if str(q)!='.'):continue
   files.append(str(p))
  extra=[] if name=='Balancy.WebView' else ['-r:'+str(out/platform/'Balancy.WebView.dll')]
  if name not in ['Balancy.WebView','Balancy']:extra+=['-r:'+str(out/platform/'Balancy.dll')]
  r=subprocess.run(base+refs+extra+[defines,'-out:'+str(out/platform/(name+'.dll'))]+files,capture_output=True,text=True)
  (out/(platform+'-'+name+'.log')).write_text(r.stdout+r.stderr)
  print(platform,name,r.returncode, '\n'.join(x for x in r.stdout.splitlines() if 'error ' in x)[:8000],flush=True)
  if r.returncode:
   failed=True
   break

raise SystemExit(1 if failed else 0)
