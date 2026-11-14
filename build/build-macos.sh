#!/usr/bin/env bash
# Builds the macOS artifacts: QuickNote.app (portable zip) and a DMG installer.
# Usage: build/build-macos.sh [local|nightly|release] [build-number] [osx-arm64|osx-x64]
set -euo pipefail

CHANNEL="${1:-local}"
BUILD_NUMBER="${2:-0}"
RUNTIME="${3:-osx-arm64}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DATE="$(date -u +%Y%m%d)"
MAJOR_MINOR="$(tr -d '[:space:]' < "$ROOT/VERSION")"
case "$CHANNEL" in
  release) VERSION="$MAJOR_MINOR.$BUILD_NUMBER-$DATE"; BUNDLE_VERSION="$MAJOR_MINOR.$BUILD_NUMBER" ;;
  nightly) VERSION="nightly-$BUILD_NUMBER-$DATE";      BUNDLE_VERSION="0.0.$BUILD_NUMBER" ;;
  *)       VERSION="local-$DATE";                      BUNDLE_VERSION="0.0.0" ;;
esac

ARTIFACTS="$ROOT/artifacts"
PUBLISH_DIR="$ARTIFACTS/publish/$RUNTIME"
APP_DIR="$ARTIFACTS/mac-$RUNTIME/QuickNote.app"
rm -rf "$PUBLISH_DIR" "$ARTIFACTS/mac-$RUNTIME"
mkdir -p "$PUBLISH_DIR" "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"

echo "==> QuickNote $VERSION ($RUNTIME)"
dotnet publish "$ROOT/src/QuickNote.App" -c Release -r "$RUNTIME" --self-contained true \
  -p:DebugType=None -p:DebugSymbols=false \
  -p:QnChannel="$CHANNEL" -p:QnBuildNumber="$BUILD_NUMBER" -p:QnBuildDate="$DATE" \
  -o "$PUBLISH_DIR"

cp -R "$PUBLISH_DIR/." "$APP_DIR/Contents/MacOS/"
cp "$ROOT/assets/icons/quicknote.icns" "$APP_DIR/Contents/Resources/QuickNote.icns"
chmod +x "$APP_DIR/Contents/MacOS/QuickNote"

cat > "$APP_DIR/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>QuickNote</string>
  <key>CFBundleDisplayName</key><string>QuickNote</string>
  <key>CFBundleIdentifier</key><string>app.quicknote</string>
  <key>CFBundleExecutable</key><string>QuickNote</string>
  <key>CFBundleIconFile</key><string>QuickNote</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$BUNDLE_VERSION</string>
  <key>CFBundleVersion</key><string>$BUILD_NUMBER</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>CFBundleDocumentTypes</key>
  <array>
    <dict>
      <key>CFBundleTypeName</key><string>QuickNote verschlüsselte Notiz</string>
      <key>CFBundleTypeExtensions</key><array><string>txts</string></array>
      <key>CFBundleTypeRole</key><string>Editor</string>
    </dict>
    <dict>
      <key>CFBundleTypeName</key><string>Markdown</string>
      <key>CFBundleTypeExtensions</key><array><string>md</string></array>
      <key>CFBundleTypeRole</key><string>Editor</string>
    </dict>
  </array>
</dict>
</plist>
PLIST

# Ad-hoc signature so the bundle starts on Apple Silicon (no developer account required).
codesign --force --deep --sign - "$APP_DIR" 2>/dev/null || echo "codesign not available, skipping"

# Portable: zipped .app
PORTABLE="$ARTIFACTS/QuickNote-$VERSION-$RUNTIME-portable.zip"
rm -f "$PORTABLE"
(cd "$(dirname "$APP_DIR")" && ditto -c -k --sequesterRsrc --keepParent QuickNote.app "$PORTABLE")
echo "Portable: $PORTABLE"

# DMG: app + Applications shortcut
DMG_STAGING="$ARTIFACTS/dmg-$RUNTIME"
DMG="$ARTIFACTS/QuickNote-$VERSION-$RUNTIME.dmg"
rm -rf "$DMG_STAGING" "$DMG"
mkdir -p "$DMG_STAGING"
cp -R "$APP_DIR" "$DMG_STAGING/"
ln -s /Applications "$DMG_STAGING/Applications"
hdiutil create -volname "QuickNote $VERSION" -srcfolder "$DMG_STAGING" -ov -format UDZO "$DMG"
echo "DMG:      $DMG"
