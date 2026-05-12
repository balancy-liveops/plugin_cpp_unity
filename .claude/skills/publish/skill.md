---
name: publish
description: Publish Unity plugin to GitHub/OpenUPM. Use when the user wants to release a new version.
disable-model-invocation: true
argument-hint: "[version, e.g. 1.6.11]"
shell: bash
---

# Publish Unity Plugin

## Pre-flight checks

!`git status --short`
!`git branch --show-current`
!`git log --oneline -5`
!`git tag --sort=-v:refname | head -5`

## Instructions

If a version was provided as argument (`$ARGUMENTS`), use that. If no version was provided (argument is empty), determine the latest tag from the pre-flight output above, parse its major.minor.patch components, and suggest the next possible versions:

- **Patch**: major.minor.(patch+1) — e.g., 1.6.10 → 1.6.11
- **Minor**: major.(minor+1).0 — e.g., 1.6.10 → 1.7.0
- **Major**: (major+1).0.0 — e.g., 1.6.10 → 2.0.0

Present these options and ask the user to pick one (or type a custom version). **Wait for their response before proceeding.**

Once the version is determined (referred to as VERSION below), proceed with the publish steps.

### Step 0 — Validate

- **Branch check**: If the current branch is NOT `main`, **stop immediately** and warn the user:
  - Tell them they are on a different branch
  - Remind them to save/commit any unsaved changes and shelve working files
  - Tell them to switch to main when ready (`git checkout main`)
  - **Do NOT switch branches automatically** — wait for the user to confirm they are on main before proceeding
- **Up-to-date check**: Run `git fetch origin main` then compare HEAD against `origin/main`. If local main is behind or ahead of remote, warn the user and suggest `git pull origin main`. Do NOT proceed until HEAD is in sync with `origin/main`.
- Confirm there are no uncommitted changes. If there are, stop and ask.
- Confirm VERSION is higher than the latest existing tag shown above.

### Step 1 — Build dependencies if needed

A Unity plugin release may include artifacts from two upstream repos. Ask the user whether either has changes that need building before this release:

**C++ core** (`balancy_cpp`) → native binaries in `Plugins/`:
- `Plugins/Android/{arm64-v8a,armeabi-v7a,x86_64}/libBalancyCore.so`
- `Plugins/WebGL/libBalancyCore.a`
- `Plugins/Windows/x86_64/libBalancyCore.dll`
- `Plugins/iOS/libBalancyCore.a`
- `Plugins/macOS/libBalancyCore.dylib`

If the C++ core has changes, the user needs to build it first (e.g., `build_unity_all_platforms.sh` or individual platform scripts in `balancy_cpp`). The build scripts copy the binaries here automatically.

**TypeScript plugin** (`plugin_cpp_typescript`) → WebView resources in `WebView/Resources/`:
- `balancy-webview-bridge.txt`
- `WebGL/balancy-webview.umd.js` and `.map`
- Other `.txt` files (shell, css-animations, js-animations, performance, styles)

If the TypeScript plugin has changes, the user needs to build it first (`npm run build` in `plugin_cpp_typescript`). The build copies these files here automatically.

Ask the user: **"Do the C++ core or TypeScript plugin have changes that need building for this release, or is this a version-only bump?"** Not every release updates these — some are package.json-only bumps (e.g., v1.6.6, v1.6.8 were version-only). Once the user confirms, proceed.

### Step 2 — Bump version and commit

Update the version in `package.json`:

```
# Update "version": "..." in package.json to VERSION
```

Use the Edit tool to change the version field. Then check for all changed files:

```
git diff --stat
git status --short
```

Stage `package.json` plus any updated release artifacts (native binaries, WebView resources). Do NOT stage `.meta` files unless they correspond to newly added files.

```
git add package.json <any-changed-artifacts>
git commit -m "VERSION"
```

The commit message should be just the version number (e.g., `1.6.11`), matching previous release commits like `1.6.10`, `1.6.9`, etc.

### Step 3 — Tag and push

Create and push the version tag. **Ask the user for confirmation before pushing.**

```
git tag vVERSION
git push origin main
git push origin vVERSION
```

This triggers the CI workflow (`.github/workflows/release.yml`) which:
1. Checks out the tag
2. Creates a zip of the package
3. Creates a GitHub Release with the zip attached

OpenUPM watches for new tags and automatically publishes the package.

### Step 4 — Verify

After pushing:

1. Check the GitHub Actions tab to confirm the release workflow completed successfully
2. Check the GitHub Releases page to confirm the release was created
3. OpenUPM typically picks up new tags within a few hours — the user can check https://openupm.com/packages/co.balancy.unity/ later

Clients who installed via Git URL will need to click **Update** in Unity Package Manager, or re-add the package URL to get the new version.

Report the result to the user.

### Important notes

- The CI workflow creates a GitHub Release automatically on tag push — no manual release creation needed.
- OpenUPM watches for tags and publishes automatically — no manual OpenUPM action needed.
- The bridge file (`balancy-webview-bridge.txt`) must come from a completed TypeScript plugin build. Always verify it's current before releasing.
- The release workflow zip filename in `.github/workflows/release.yml` still uses a placeholder name (`com.yourcompany.yourpackage`). This doesn't affect functionality but could be updated to `co.balancy.unity` for clarity.
