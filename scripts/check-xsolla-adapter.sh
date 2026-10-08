#!/bin/sh
set -eu
# Runs shipped C# adapter against deterministic transport/native/View fixtures; no Unity editor or payment call.
repo=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
output=$(mktemp -d "${TMPDIR:-/tmp}/balancy-xsolla-csharp.XXXXXX")
trap 'rm -rf "$output"' EXIT INT TERM
DOTNET_NOLOGO=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_CLI_HOME="$output/home" dotnet run \
  --project "$repo/Tests/Standalone/XsollaCheckout/XsollaCheckout.csproj" --no-launch-profile \
  --property:BaseIntermediateOutputPath="$output/obj/" --property:OutputPath="$output/bin/"
