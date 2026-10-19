#!/usr/bin/env node
// Cross-compiled artifact checks, not device/Unity/payment acceptance.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),{execFileSync}=require('node:child_process');
const root=path.resolve(__dirname,'..'),ndk=process.argv[2]||process.env.ANDROID_NDK_HOME;
const requiredExports=['balancyXsollaGetSessionIdentity','balancyXsollaPreparePurchaseWithCapability','balancyXsollaPreparePurchase','balancyXsollaRequest','balancyXsollaComplete'];
const run=(binary,args)=>execFileSync(binary,args,{encoding:'utf8',timeout:30000,maxBuffer:16*1024*1024,stdio:['ignore','pipe','pipe']});
try {
 const ios=[];
 for(const [slice,architectures] of [['ios-arm64',['arm64']],['ios-arm64_x86_64-simulator',['arm64','x86_64']]]){
  const file=path.join(root,'Plugins/iOS/BalancyCore.xcframework',slice,'libBalancyCore.a');assert(fs.existsSync(file),'Missing '+slice);
  assert.deepEqual(run('xcrun',['lipo','-archs',file]).trim().split(/\s+/).sort(),architectures);
  const symbols=run('xcrun',['nm','-gU',file]);for(const name of requiredExports)assert(symbols.includes('_'+name),'Missing iOS export '+name);
  ios.push({slice,architectures,xsollaExports:requiredExports.length});
 }
 assert(ndk,'Pass the Android NDK directory or set ANDROID_NDK_HOME');
 const tools=path.join(ndk,'toolchains/llvm/prebuilt/darwin-x86_64/bin'),android=[];
 for(const [abi,elfClass,machine] of [['arm64-v8a','ELF64','AArch64'],['armeabi-v7a','ELF32','ARM'],['x86_64','ELF64','Advanced Micro Devices X86-64']]){
  const file=path.join(root,'Plugins/Android',abi,'libBalancyCore.so');assert(fs.existsSync(file),'Missing '+abi);
  const header=run(path.join(tools,'llvm-readelf'),['-h',file]);assert(header.includes(elfClass),'Wrong ELF class '+abi);
  assert(new RegExp('Machine:\\s*'+machine+'\\s*$','m').test(header),'Wrong machine '+abi);
  const symbols=run(path.join(tools,'llvm-nm'),['-D','--defined-only',file]);
  for(const name of requiredExports)assert(new RegExp('\\b'+name+'\\s*$','m').test(symbols),'Missing Android export '+name+' '+abi);
  const loads=run(path.join(tools,'llvm-readelf'),['-l','--wide',file]).split('\n').filter(line=>/^\s*LOAD\s/.test(line));assert(loads.length,'No LOAD segments '+abi);
  for(const line of loads)assert(Number(line.trim().split(/\s+/).pop())>=16384,'Missing 16KiB LOAD alignment '+abi);
  android.push({abi,elfClass,machine,xsollaExports:requiredExports.length,loadAlignment16KiB:true});
 }
 console.log(JSON.stringify({ok:true,ios,android,deviceAcceptance:false},null,2));
}catch(error){console.error(error.message);process.exitCode=1;}
