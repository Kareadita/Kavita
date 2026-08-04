#!/usr/bin/env bash
# Downloads a pinned kepubify release binary into <outputDir>/tools/kepubify[.exe]
# for the given .NET RID.
#
# Usage: fetch-kepubify.sh <rid> <outputDir>
# Example: fetch-kepubify.sh linux-x64 ../_output/linux-x64/Kavita
set -euo pipefail

KEPUBIFY_VERSION="${KEPUBIFY_VERSION:-v4.0.4}"
RID="${1:?RID required (e.g. linux-x64)}"
OUTPUT_DIR="${2:?output directory required}"

asset_for_rid() {
    case "$1" in
        linux-x64|linux-musl-x64) echo "kepubify-linux-64bit" ;;
        linux-arm) echo "kepubify-linux-arm" ;;
        linux-arm64|linux-musl-arm64) echo "kepubify-linux-arm64" ;;
        win-x64) echo "kepubify-windows-64bit.exe" ;;
        win-arm64) echo "kepubify-windows-arm64.exe" ;;
        win-x86) echo "kepubify-windows-32bit.exe" ;;
        osx-x64) echo "kepubify-darwin-64bit" ;;
        osx-arm64) echo "kepubify-darwin-arm64" ;;
        *)
            echo "fetch-kepubify: unsupported RID '$1'" >&2
            return 1
            ;;
    esac
}

ASSET="$(asset_for_rid "$RID")"
URL="https://github.com/pgaskin/kepubify/releases/download/${KEPUBIFY_VERSION}/${ASSET}"

TOOLS_DIR="${OUTPUT_DIR}/tools"
mkdir -p "$TOOLS_DIR"

case "$ASSET" in
    *.exe) DEST="${TOOLS_DIR}/kepubify.exe" ;;
    *) DEST="${TOOLS_DIR}/kepubify" ;;
esac

echo "Fetching kepubify ${KEPUBIFY_VERSION} (${ASSET}) → ${DEST}"
if command -v curl >/dev/null 2>&1; then
    curl -fsSL -o "$DEST" "$URL"
elif command -v wget >/dev/null 2>&1; then
    wget -q -O "$DEST" "$URL"
else
    echo "fetch-kepubify: curl or wget required" >&2
    exit 1
fi

chmod +x "$DEST" || true
echo "Bundled kepubify at ${DEST}"
