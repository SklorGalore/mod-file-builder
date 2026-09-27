#!/bin/bash
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
case "${1:-$(uname -m)}" in
    arm64|osx-arm64) runtime=osx-arm64 ;;
    x86_64|osx-x64) runtime=osx-x64 ;;
    *) echo 'Usage: bash scripts/publish-mac.sh [osx-arm64|osx-x64]' >&2; exit 1 ;;
esac
if [[ "$(uname -s)" != Darwin ]]; then
    echo 'Run this packaging script on macOS (it uses codesign and ditto).' >&2
    exit 1
fi
output_dir="$project_root/publish/$runtime"
mkdir -p "$output_dir"
staging_dir="$(mktemp -d "$output_dir/.build.XXXXXX")"
trap 'rm -rf "$staging_dir"' EXIT
app_bundle="$staging_dir/Case Modification Builder.app"
mkdir -p "$app_bundle/Contents/MacOS" "$app_bundle/Contents/Resources"
dotnet publish "$project_root/src/ModFileBuilder.Mac/ModFileBuilder.Mac.csproj" \
    -c Release -r "$runtime" --self-contained true -p:UseAppHost=true \
    -o "$app_bundle/Contents/MacOS"
cp "$project_root/src/ModFileBuilder.Mac/Info.plist" "$app_bundle/Contents/Info.plist"
chmod +x "$app_bundle/Contents/MacOS/ModFileBuilder.Mac"

# Ad-hoc signing makes the local bundle runnable without a Developer ID certificate.
# Sign published files before signing the app host and containing bundle.
# Managed assemblies also need signatures when packaged in Contents/MacOS.
while IFS= read -r -d '' binary; do
    codesign --force --sign - "$binary"
done < <(find "$app_bundle/Contents/MacOS" -type f ! -name ModFileBuilder.Mac -print0)
codesign --force --sign - --entitlements "$project_root/src/ModFileBuilder.Mac/Entitlements.plist" "$app_bundle"
codesign --verify --strict "$app_bundle"

# Replace only this script's generated bundle, after publishing and signing succeed.
final_bundle="$output_dir/Case Modification Builder.app"
if [[ -e "$final_bundle" ]]; then rm -rf "$final_bundle"; fi
mv "$app_bundle" "$final_bundle"
printf 'Built: %s\n' "$final_bundle"
