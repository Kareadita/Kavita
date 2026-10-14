#!/usr/bin/env bash
# Builds one self-contained <name> per platform, each packed with the project's README into <project>/dist/
# Usage: publish.sh <project folder> <name>
set -euo pipefail
project="$(cd "$1" && pwd)"
name="$2"
cd "$project"

rm -rf dist
mkdir -p dist

for rid in linux-x64 linux-arm64 linux-musl-x64 win-x64 osx-arm64; do
  out="dist/$name-$rid"
  dotnet publish -c Release -r "$rid" -o "$out" -v q
  cp README.md "$out/"

  if [[ "$rid" == win-* ]]; then
    # Git Bash has no zip; Windows' own tar (bsdtar) writes zip with -a
    if command -v zip >/dev/null; then
      (cd dist && zip -qr "$name-$rid.zip" "$name-$rid")
    else
      (cd dist && /c/Windows/System32/tar.exe -a -cf "$name-$rid.zip" "$name-$rid")
    fi
  else
    # --mode keeps the binary executable even when packed on Windows
    tar -czf "dist/$name-$rid.tar.gz" -C dist --mode='u+rwx,go+rx' "$name-$rid"
  fi
done

ls -l dist/*.zip dist/*.tar.gz
