#!/bin/sh
set -eu
# Actual P/Invoke against the packaged dylib through the current SDK declarations.
# Default: compile the exact Xsolla declaration section, without requiring Unity Editor.
# Optional first argument: a freshly Unity-built Balancy.dll instead.
# No SDK initialization, auth, provider requests, profile writes or game launch.
repo=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
output=$(mktemp -d "${TMPDIR:-/tmp}/balancy-native-abi.XXXXXX")
trap 'rm -rf "$output"' EXIT INT TERM
cat > "$output/Abi.csproj" <<'PROJECT'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>
PROJECT
python3 - "$repo/Runtime/LibraryMethods.cs" "$output/Bindings.cs" <<'PYCODE'
from pathlib import Path
import sys
source=Path(sys.argv[1]).read_text()
a=source.index('internal delegate void XsollaCallback')
a=source.rfind('[UnmanagedFunctionPointer',0,a)
b=source.index('[UnmanagedFunctionPointer',source.index('internal static extern void balancyXsollaComplete',a))
Path(sys.argv[2]).write_text('using System; using System.Runtime.InteropServices; namespace Balancy { internal static class LibraryMethods { internal const string DllName="libBalancyCore"; public static class API {\n'+source[a:b]+'\n}}}')
PYCODE
cat > "$output/Program.cs" <<'CS'
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
class Program {
 static int responses;
 public static void Response(int id,bool success,string json) {
  using var data=JsonDocument.Parse(json);
  if(success||data.RootElement.GetProperty("error").GetString()!="xsolla_user_unavailable"||id!=123+responses)throw new Exception("Native callback ABI/value mismatch");
  responses++;
 }
 static void Main(string[] args) {
  var assembly=args[0]=="--source"?typeof(Program).Assembly:Assembly.LoadFrom(Path.GetFullPath(args[0]));var native=NativeLibrary.Load(Path.GetFullPath(args[1]));
  NativeLibrary.SetDllImportResolver(assembly,(name,a,path)=>name=="libBalancyCore"?native:IntPtr.Zero);
  var api=assembly.GetType("Balancy.LibraryMethods+API",true)!;
  object? Call(string name,params object[] values)=>(api.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public) ?? throw new Exception("Missing SDK declaration "+name+"; rebuild the supplied Unity assembly")).Invoke(null,values);
  string Copy(string name,params object[] values)=>Marshal.PtrToStringUTF8((IntPtr)Call(name,values)!)!;
  using(var session=JsonDocument.Parse(Copy("balancyXsollaGetSessionIdentity")))if(session.RootElement.ValueKind!=JsonValueKind.Object)throw new Exception("Session pointer ABI");
  foreach(var choice in new[]{false,true}) {
   using var prepared=JsonDocument.Parse(Copy("balancyXsollaPreparePurchaseWithCapability",0,IntPtr.Zero,IntPtr.Zero,choice));
   if(prepared.RootElement.GetProperty("error").GetString()!="xsolla_user_unavailable")throw new Exception("Capability bool/string ABI");
  }
  using(var legacy=JsonDocument.Parse(Copy("balancyXsollaPreparePurchase",0,IntPtr.Zero,IntPtr.Zero)))if(legacy.RootElement.GetProperty("error").GetString()!="xsolla_user_unavailable")throw new Exception("Legacy prepare pointer ABI");
  var callback=Delegate.CreateDelegate(api.GetNestedType("XsollaCallback",BindingFlags.NonPublic|BindingFlags.Public)!,typeof(Program).GetMethod(nameof(Response))!);
  Call("balancyXsollaRequest","invalid","{}",123,callback);
  Call("balancyXsollaComplete","00000000-0000-4000-8000-000000000001",124,callback);
  GC.KeepAlive(callback);if(responses!=2)throw new Exception("Native callbacks not synchronous for unavailable context");
  Console.WriteLine("PASS actual packaged native P/Invoke: five exports, UTF8 pointers, both I1 capability values and two real false/error callbacks; no initialized user or payment IO.");
 }
}
CS
DOTNET_NOLOGO=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_CLI_HOME="$output/home" dotnet run --project "$output/Abi.csproj" --no-launch-profile -- \
 "${1:---source}" "${2:-$repo/Plugins/macOS/libBalancyCore.dylib}"
