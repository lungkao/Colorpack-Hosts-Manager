#!/bin/bash
# สร้างไฟล์ติดตั้ง Hosts Manager.dmg สำหรับเครื่องอื่น
# ใช้: ./make_dmg.sh   -> ได้ไฟล์ dist/HostsManager-<version>.dmg
set -euo pipefail
cd "$(dirname "$0")"

VERSION="1.2"
APP="Hosts Manager.app"
STAGE="build/dmg"
OUT="build/release/$APP"
mkdir -p dist build/release
rm -rf "$OUT" "$STAGE"
mkdir -p "$OUT/Contents/MacOS" "$OUT/Contents/Resources"

echo "• compile arm64 (Apple Silicon)..."
swiftc -O -parse-as-library -target arm64-apple-macos13.0 HostsManager.swift -o build/hm-arm64

ARCHS="arm64"
echo "• compile x86_64 (Intel)..."
if swiftc -O -parse-as-library -target x86_64-apple-macos13.0 HostsManager.swift -o build/hm-x86_64 2>build/x86.log; then
  lipo -create build/hm-arm64 build/hm-x86_64 -output "$OUT/Contents/MacOS/HostsManager"
  ARCHS="arm64 + x86_64"
else
  echo "  (build Intel ไม่ได้ — ใช้เฉพาะ Apple Silicon)"
  cp build/hm-arm64 "$OUT/Contents/MacOS/HostsManager"
fi

[[ -f AppIcon.icns ]] && cp AppIcon.icns "$OUT/Contents/Resources/AppIcon.icns"

cat > "$OUT/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>HostsManager</string>
  <key>CFBundleIdentifier</key><string>local.hostsmanager</string>
  <key>CFBundleName</key><string>Hosts Manager</string>
  <key>CFBundleDisplayName</key><string>Hosts Manager</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>${VERSION}</string>
  <key>CFBundleVersion</key><string>${VERSION}</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>CFBundleIconFile</key><string>AppIcon</string>
</dict>
</plist>
EOF

codesign --force --deep --sign - "$OUT"

# เนื้อหาใน DMG: แอป + ลิงก์ Applications + วิธีติดตั้ง
mkdir -p "$STAGE"
cp -R "$OUT" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
cat > "$STAGE/อ่านก่อนติดตั้ง - Read Me.txt" <<'EOF'
Hosts Manager — วิธีติดตั้ง
============================
1) ลาก "Hosts Manager" ไปวางที่โฟลเดอร์ Applications
2) เปิดครั้งแรก ถ้า macOS แจ้งว่าเปิดไม่ได้ / ไม่รู้จักผู้พัฒนา:
   - ไปที่ System Settings > Privacy & Security
   - เลื่อนลงจะเห็นข้อความเกี่ยวกับ "Hosts Manager" > กด "Open Anyway"
   หรือเปิด Terminal แล้วพิมพ์:
   xattr -dr com.apple.quarantine "/Applications/Hosts Manager.app"
3) ต้องใช้ macOS 13 ขึ้นไป
ตอนกดบันทึก แอปจะขอรหัสผ่านเครื่อง (ต้องใช้สิทธิ์ admin เพื่อแก้ /etc/hosts)

Installation (English)
======================
1) Drag "Hosts Manager" into the Applications folder.
2) First launch: if macOS says it can't be opened / unidentified developer,
   go to System Settings > Privacy & Security > "Open Anyway",
   or run in Terminal:
   xattr -dr com.apple.quarantine "/Applications/Hosts Manager.app"
3) Requires macOS 13 or later.
Saving asks for an administrator password (needed to edit /etc/hosts).
EOF

DMG="dist/HostsManager-${VERSION}.dmg"
rm -f "$DMG"
hdiutil create -volname "Hosts Manager" -srcfolder "$STAGE" -ov -format UDZO "$DMG" >/dev/null
echo "✅ $DMG ($ARCHS)"
