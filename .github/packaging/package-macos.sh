#!/usr/bin/env bash
set -euo pipefail

PUBLISH_ROOT="${1:?missing publish root}"
VERSION="${2:?missing version}"
OUTPUT_DIR="${3:?missing output dir}"

APP_PATH=$(find "$PUBLISH_ROOT" -maxdepth 2 -type d -name "*.app" | head -n 1)
if [ -z "${APP_PATH:-}" ]; then
  echo "未找到 .app 包: $PUBLISH_ROOT" >&2
  exit 1
fi

mkdir -p "$OUTPUT_DIR"
PKG_PATH="$OUTPUT_DIR/devnexus-macos-${VERSION}.pkg"

pkgbuild \
  --component "$APP_PATH" \
  --install-location "/Applications" \
  "$PKG_PATH"

echo "$PKG_PATH"
