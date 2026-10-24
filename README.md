# Hosts Manager

แอป macOS สำหรับแก้ไฟล์ `/etc/hosts` แบบง่ายๆ ระหว่างย้ายเว็บไป server ใหม่ พร้อมปุ่ม Flush DNS
A small macOS app for editing `/etc/hosts` during website migrations, with a one-click DNS flush.

## ความสามารถ / Features

- เพิ่มโดเมนชี้ไป IP server ใหม่ (ใส่หลายโดเมนพร้อมกัน, เติม `www.` อัตโนมัติ)
  Point domains to a new server IP (bulk add, automatic `www.`)
- เปิด/ปิดแต่ละรายการได้โดยไม่ต้องลบ — Toggle entries on/off without deleting them
- บันทึกแล้ว Flush DNS ให้อัตโนมัติ และสำรองไฟล์เดิมไว้ที่ `/etc/hosts.hostsmanager.bak`
  Saving flushes DNS and backs up the previous file to `/etc/hosts.hostsmanager.bak`
- ตรวจโดเมนซ้ำ (ข้ามแถว, ในแถวเดียวกัน, และชนกับบรรทัดเดิมในไฟล์)
  Duplicate detection (across rows, within a row, and against existing lines in the file)
- นำเข้ารายการเดิมแบบเลือกได้ กรองเฉพาะโดเมนจริง
  Selective import of existing entries, filtered to real public domains
- ตรวจว่าโดเมนชี้ไปที่ไหน เทียบเครื่องนี้กับ DNS สาธารณะ
  Check where a domain resolves on this Mac vs. public DNS
- แก้ไฟล์ดิบทั้งไฟล์ได้ — Raw editor for the whole file
- ภาษาไทย / English

แอปแก้เฉพาะบรรทัดในบล็อก `# >>> HostsManager >>>` … `# <<< HostsManager <<<` ส่วนอื่นของไฟล์ไม่ถูกแตะ
The app only rewrites lines inside its own marked block; the rest of the file is left as is.

## ติดตั้ง / Install

ดาวน์โหลด `.dmg` จากหน้า Releases แล้วลากแอปไปที่ Applications
Download the `.dmg` from Releases and drag the app into Applications.

แอปยังไม่ได้ notarize — เปิดครั้งแรกให้ไปที่ **System Settings › Privacy & Security › Open Anyway**
The app is not notarized — on first launch use **System Settings › Privacy & Security › Open Anyway**, or:

```
xattr -dr com.apple.quarantine "/Applications/Hosts Manager.app"
```

ต้องใช้ macOS 13 ขึ้นไป · Requires macOS 13+

## Build จากซอร์ส / Build from source

ต้องมี Xcode Command Line Tools (`xcode-select --install`)

```
./build.sh --install   # build และติดตั้งลง /Applications
./make_dmg.sh          # สร้าง dist/HostsManager-<version>.dmg (Apple Silicon + Intel)
```

| ไฟล์ / File | หน้าที่ / Purpose |
|---|---|
| `HostsManager.swift` | ซอร์สทั้งหมดของแอป (SwiftUI) · the whole app |
| `icon.swift` | วาดไอคอนแอป · draws the app icon |
| `build.sh` | build + ติดตั้ง · build and install locally |
| `make_dmg.sh` | สร้างไฟล์ติดตั้ง DMG · build a distributable DMG |

ตอนบันทึก แอปจะขอรหัสผ่าน admin ผ่าน macOS เพื่อเขียน `/etc/hosts`
Saving asks for an administrator password through macOS to write `/etc/hosts`.

## ☕ Support this App

If you find **Hosts Manager** helpful, you can support the developer by buying a coffee:
ถ้า **Hosts Manager** มีประโยชน์กับคุณ สามารถสนับสนุนผู้พัฒนาด้วยการเลี้ยงกาแฟได้ที่นี่:

👉&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<a href="https://buymeacoffee.com/cheuachatchai" >![buy_me_a_coffee](https://github.com/conseilgouz/plg_system_cgwebp_j4/assets/19435246/4fda4cb5-64f1-4717-81ae-c71a0fc26c2d)</a>
