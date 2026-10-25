#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
../ProbeCommon/publish.sh . fsprobe
