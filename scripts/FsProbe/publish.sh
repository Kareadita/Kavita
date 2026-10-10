#!/usr/bin/env bash
# Builds one self-contained fsprobe per platform, each packed with the README into dist/
set -euo pipefail
cd "$(dirname "$0")"

rm -rf dist
mkdir -p dist

for rid in linux-x64 linux-arm64 linux-musl-x64 win-x64 osx-arm64; do
  out="dist/fsprobe-$rid"
  dotnet publish -c Release -r "$rid" -o "$out" -v q
  cp README.md "$out/"

  if [[ "$rid" == win-* ]]; then
    # Git Bash has no zip; Windows' own tar (bsdtar) writes zip with -a
    if command -v zip >/dev/null; then
      (cd dist && zip -qr "fsprobe-$rid.zip" "fsprobe-$rid")
    else
      (cd dist && /c/Windows/System32/tar.exe -a -cf "fsprobe-$rid.zip" "fsprobe-$rid")
    fi
  else
    # --mode keeps the binary executable even when packed on Windows
    tar -czf "dist/fsprobe-$rid.tar.gz" -C dist --mode='u+rwx,go+rx' "fsprobe-$rid"
  fi
done

ls -l dist/*.zip dist/*.tar.gz
