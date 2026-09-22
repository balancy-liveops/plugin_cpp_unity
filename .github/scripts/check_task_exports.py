#!/usr/bin/env python3
"""Release gate: every shipped Unity native slice must implement the task ABI.

Run after all platform builds. Missing files, unreadable symbol tables, and absent
exports fail the check. This validates linkage, not on-device runtime behavior.
"""
import argparse
from pathlib import Path
import re
import shutil
import subprocess

REQUIRED = {'balancyTasks_SetLifecycleCallback', 'balancyTasks_Update',
            'balancyTasks_ActivateTask', 'balancyTasks_DeactivateTask',
            'balancyTasks_ClaimReward', 'balancyTasks_RestoreFailedTask'}
ARTIFACTS = [
    ('macOS/libBalancyCore.dylib', ['arm64', 'x86_64']),
    ('Android/arm64-v8a/libBalancyCore.so', [None]),
    ('Android/armeabi-v7a/libBalancyCore.so', [None]),
    ('Android/x86_64/libBalancyCore.so', [None]),
    ('iOS/BalancyCore.xcframework/ios-arm64/libBalancyCore.a', ['arm64']),
    ('iOS/BalancyCore.xcframework/ios-arm64_x86_64-simulator/libBalancyCore.a', ['arm64', 'x86_64']),
    ('WebGL/libBalancyCore.a', [None]),
    ('Windows/x86_64/libBalancyCore.dll', [None]),
]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plugins', type=Path, default=Path(__file__).resolve().parents[2] / 'Plugins')
    parser.add_argument('--nm', default=shutil.which('llvm-nm') or 'llvm-nm')
    parser.add_argument('--readobj', default=shutil.which('llvm-readobj') or 'llvm-readobj')
    args = parser.parse_args()
    failures = 0
    for relative, architectures in ARTIFACTS:
        path = args.plugins / relative
        for arch in architectures:
            label = relative + (f' ({arch})' if arch else '')
            try:
                if not path.is_file():
                    raise RuntimeError('artifact missing')
                if path.suffix == '.dll':
                    command = [args.readobj, '--coff-exports', str(path)]
                else:
                    command = [args.nm, '--defined-only', '--extern-only']
                    if path.suffix == '.so': command.append('--dynamic')
                    if arch: command.append('--arch=' + arch)
                    command.append(str(path))
                result = subprocess.run(command, text=True, capture_output=True, check=True)
                names = set(re.findall(r'\b_?(balancyTasks_\w+)\b', result.stdout))
                missing = REQUIRED - names
                if missing: raise RuntimeError('missing: ' + ', '.join(sorted(missing)))
                print('PASS', label)
            except (OSError, RuntimeError, subprocess.CalledProcessError) as error:
                failures += 1
                print('FAIL', label, ':', error)
    return 1 if failures else 0

if __name__ == '__main__':
    raise SystemExit(main())
