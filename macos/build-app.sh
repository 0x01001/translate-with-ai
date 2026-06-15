#!/usr/bin/env bash
# Builds ReWrite.app for macOS:
#   1. swift build (release)
#   2. assembles the .app bundle
#   3. copies the shared web UI from the Windows project, rewriting the
#      https://rewrite.local virtual origin to the rewrite://local scheme
#      that WKWebView can intercept
#   4. generates the .icns icon and ad-hoc signs the bundle
set -euo pipefail
cd "$(dirname "$0")"

REPO_ROOT=".."
APP_NAME="ReWrite"
VERSION="${VERSION:-0.0.$(date +%y%m%d)}"
DIST="dist/$APP_NAME.app"
CONTENTS="$DIST/Contents"
WEB="$CONTENTS/Resources/web"

echo "==> Building Swift sources (release)"
# Some Command Line Tools installs ship a duplicated SwiftBridging modulemap
# (usr/include/swift/module.modulemap + bridging.modulemap), which breaks every
# Swift compile. Mask the duplicate with a VFS overlay — no sudo required.
CLT_SWIFT_INC="/Library/Developer/CommandLineTools/usr/include/swift"
EXTRA_FLAGS=()
if [[ -f "$CLT_SWIFT_INC/module.modulemap" && -f "$CLT_SWIFT_INC/bridging.modulemap" ]]; then
    mkdir -p .build/clt-fix
    touch .build/clt-fix/empty.modulemap
    cat > .build/clt-fix/overlay.yaml <<EOF
{
  "version": 0,
  "use-external-names": false,
  "roots": [
    {
      "name": "$CLT_SWIFT_INC/module.modulemap",
      "type": "file",
      "external-contents": "$(pwd)/.build/clt-fix/empty.modulemap"
    }
  ]
}
EOF
    EXTRA_FLAGS=(-vfsoverlay "$(pwd)/.build/clt-fix/overlay.yaml")
fi

# Prefer SPM; fall back to direct swiftc when the SwiftPM manifest toolchain
# is broken (also seen with some Command Line Tools installs).
if swift build -c release 2>/dev/null; then
    BIN_PATH="$(swift build -c release --show-bin-path)/ReWriteMac"
else
    echo "    swift build unavailable — compiling with swiftc directly"
    mkdir -p .build/direct
    swiftc -O -swift-version 5 \
        -target "$(uname -m)-apple-macosx13.0" \
        "${EXTRA_FLAGS[@]}" \
        -module-cache-path .build/module-cache \
        Sources/ReWriteMac/*.swift \
        -framework Cocoa -framework WebKit -framework Carbon -framework ServiceManagement \
        -o .build/direct/ReWriteMac
    BIN_PATH=".build/direct/ReWriteMac"
fi

echo "==> Assembling $DIST"
rm -rf dist
mkdir -p "$CONTENTS/MacOS" "$WEB/locales" "$WEB/ui"

cp "$BIN_PATH" "$CONTENTS/MacOS/$APP_NAME"
sed "s/__VERSION__/$VERSION/g" Info.plist.template > "$CONTENTS/Info.plist"

# --- Web UI assets (shared with the Windows build) ---------------------------
# Text assets get the origin rewrite; binary assets are copied as-is.
patch_copy() { sed 's|https://rewrite\.local/|rewrite://local/|g' "$1" > "$2"; }

for f in popup.html popup.js ai-loader.html prompt-templates.js; do
    patch_copy "$REPO_ROOT/Windows/PopupWindow/$f" "$WEB/$f"
done
for f in settings.html settings.js; do
    patch_copy "$REPO_ROOT/Windows/SettingsWindow/$f" "$WEB/$f"
done
for f in tutorial.html tutorial.js; do
    patch_copy "$REPO_ROOT/Windows/TutorialWindow/$f" "$WEB/$f"
done

patch_copy "$REPO_ROOT/ui/tailwind.css" "$WEB/tailwind.css"
cp "$REPO_ROOT/ui/logo.png" "$WEB/logo.png"
cp "$REPO_ROOT/ui/logo.png" "$WEB/ui/logo.png"   # some pages reference ui/logo.png
cp "$REPO_ROOT"/Core/Localization/locales/*.json "$WEB/locales/"

# --- App icon ----------------------------------------------------------------
echo "==> Generating icon"
ICONSET="$(mktemp -d)/logo.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
    sips -z $size $size "$REPO_ROOT/ui/logo.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    sips -z $((size * 2)) $((size * 2)) "$REPO_ROOT/ui/logo.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$CONTENTS/Resources/logo.icns"

# --- Sign --------------------------------------------------------------------
echo "==> Ad-hoc signing"
codesign --force --deep --sign - "$DIST"

echo "==> Done: $DIST (version $VERSION)"
echo "    Run with: open \"$DIST\""
