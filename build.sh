#!/bin/bash
# สร้าง "Hosts Manager.app" จาก HostsManager.swift
# ใช้: ./build.sh            -> สร้างไว้ในโฟลเดอร์ build/
#      ./build.sh --install  -> สร้างแล้วคัดลอกไปที่ /Applications
set -euo pipefail
cd "$(dirname "$0")"

if ! command -v swiftc >/dev/null 2>&1; then
  echo "ยังไม่มี Swift compiler — ติดตั้งด้วยคำสั่ง: xcode-select --install"
  exit 1
fi

APP="Hosts Manager.app"
OUT="build/$APP"
rm -rf "$OUT"
mkdir -p "$OUT/Contents/MacOS" "$OUT/Contents/Resources"

echo "กำลัง compile..."
swiftc -O -parse-as-library \
  -target "$(uname -m)-apple-macos13.0" \
  HostsManager.swift \
  -o "$OUT/Contents/MacOS/HostsManager"

# ไอคอน: สร้างจาก icon.swift ครั้งแรกครั้งเดียว (ลบ AppIcon.icns ถ้าอยากสร้างใหม่)
if [[ -f icon.swift && ! -f AppIcon.icns ]]; then
  echo "กำลังสร้างไอคอน..."
  swiftc -O icon.swift -o build/makeicon
  build/makeicon icon_1024.png
  rm -rf AppIcon.iconset && mkdir AppIcon.iconset
  for s in 16 32 128 256 512; do
    sips -z $s $s icon_1024.png --out "AppIcon.iconset/icon_${s}x${s}.png" >/dev/null
    d=$((s*2))
    sips -z $d $d icon_1024.png --out "AppIcon.iconset/icon_${s}x${s}@2x.png" >/dev/null
  done
  iconutil -c icns AppIcon.iconset -o AppIcon.icns
fi
if [[ -f AppIcon.icns ]]; then cp AppIcon.icns "$OUT/Contents/Resources/AppIcon.icns"; fi

cat > "$OUT/Contents/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>HostsManager</string>
  <key>CFBundleIdentifier</key><string>local.hostsmanager</string>
  <key>CFBundleName</key><string>Hosts Manager</string>
  <key>CFBundleDisplayName</key><string>Hosts Manager</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>CFBundleIconFile</key><string>AppIcon</string>
</dict>
</plist>
EOF

codesign --force --sign - "$OUT" >/dev/null 2>&1 || true
echo "✅ สร้างเสร็จ: $OUT"

if [[ "${1:-}" == "--install" ]]; then
  pkill -x HostsManager 2>/dev/null || true
  sleep 1
  rm -rf "/Applications/$APP"
  cp -R "$OUT" /Applications/
  touch "/Applications/$APP"
  echo "✅ ติดตั้งที่ /Applications/$APP แล้ว"
  open "/Applications/$APP"
else
  open "$OUT"
fi
