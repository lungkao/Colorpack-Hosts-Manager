# Hosts Manager

แอปสำหรับแก้ไฟล์ `hosts` แบบง่ายๆ ระหว่างย้ายเว็บไป server ใหม่ พร้อมปุ่มล้าง DNS — มีทั้ง **macOS** และ **Windows**
A small app for editing the `hosts` file during website migrations, with one-click DNS flush — for **macOS** and **Windows**.

## ความสามารถ / Features

- เพิ่มโดเมนชี้ไป IP server ใหม่ (ใส่หลายโดเมนพร้อมกัน, เติม `www.` อัตโนมัติ)
  Point domains to a new server IP (bulk add, automatic `www.`)
- เปิด/ปิดแต่ละรายการได้โดยไม่ต้องลบ — Toggle entries on/off without deleting them
- บันทึกแล้วล้าง DNS ให้อัตโนมัติ และสำรองไฟล์เดิมไว้ที่ `hosts.hostsmanager.bak`
  Saving flushes DNS and backs up the previous file to `hosts.hostsmanager.bak`
- ตรวจโดเมนซ้ำ (ข้ามแถว, ในแถวเดียวกัน, และชนกับบรรทัดเดิมในไฟล์)
  Duplicate detection (across rows, within a row, and against existing lines in the file)
- นำเข้ารายการเดิมแบบเลือกได้ กรองเฉพาะโดเมนจริง
  Selective import of existing entries, filtered to real public domains
- ตรวจว่าโดเมนชี้ไปที่ไหน เทียบเครื่องนี้กับ DNS สาธารณะ
  Check where a domain resolves on this computer vs. public DNS
- แก้ไฟล์ดิบทั้งไฟล์ได้ — Raw editor for the whole file
- ภาษาไทย / English

แอปแก้เฉพาะบรรทัดในบล็อก `# >>> HostsManager >>>` … `# <<< HostsManager <<<` ส่วนอื่นของไฟล์ไม่ถูกแตะ
The app only rewrites lines inside its own marked block; the rest of the file is left as is.
ทั้งสองเวอร์ชันใช้รูปแบบบล็อกเดียวกัน / Both versions use the same block format.

## ติดตั้ง / Install

ดาวน์โหลดจากหน้า [Releases](https://github.com/lungkao/hosts-manager/releases/latest)
Download from [Releases](https://github.com/lungkao/hosts-manager/releases/latest).

### macOS (13+, Apple Silicon และ Intel)

เปิดไฟล์ `.dmg` แล้วลากแอปไปที่ Applications
Open the `.dmg` and drag the app into Applications.

แอปยังไม่ได้ notarize — เปิดครั้งแรกให้ไปที่ **System Settings › Privacy & Security › Open Anyway**
The app is not notarized — on first launch use **System Settings › Privacy & Security › Open Anyway**, or:

```
xattr -dr com.apple.quarantine "/Applications/Hosts Manager.app"
```

ตอนบันทึก แอปจะขอรหัสผ่าน admin / Saving asks for an administrator password.

### Windows (10 / 11)

แตกไฟล์ `HostsManager-windows.zip` แล้วเปิด `HostsManager.exe` ได้เลย ไม่ต้องติดตั้ง
Unzip `HostsManager-windows.zip` and run `HostsManager.exe` — no installation needed.

- แอปจะขอสิทธิ์ admin (UAC) ตอนเปิด เพราะต้องแก้ `C:\Windows\System32\drivers\etc\hosts`
  The app asks for admin rights (UAC) on launch, since it edits `C:\Windows\System32\drivers\etc\hosts`.
- ครั้งแรก SmartScreen อาจเตือน ให้กด **More info › Run anyway** (แอปยังไม่ได้เซ็นใบรับรอง)
  SmartScreen may warn on first run — click **More info › Run anyway** (the app is not code-signed).

## Build จากซอร์ส / Build from source

| โฟลเดอร์ / Folder | |
|---|---|
| `macos/` | SwiftUI — `HostsManager.swift`, `icon.swift`, `build.sh`, `make_dmg.sh` |
| `windows/` | C# WinForms (.NET Framework 4.8) — `HostsCore.cs`, `MainForm.cs`, `Dialogs.cs`, `Program.cs` |

**macOS** — ต้องมี Xcode Command Line Tools (`xcode-select --install`)

```
cd macos
./build.sh --install   # build และติดตั้งลง /Applications
./make_dmg.sh          # สร้าง dist/HostsManager-<version>.dmg (Apple Silicon + Intel)
```

**Windows** — ต้องมี .NET SDK 8 ขึ้นไป / requires the .NET SDK 8+

```
dotnet build windows/HostsManager.csproj -c Release
windows\bin\Release\net48\HostsManager.exe --selftest report.txt   # ทดสอบตรรกะหลัก / run logic tests
```

GitHub Actions จะ build, ทดสอบ และแพ็ก zip ของ Windows ให้ทุกครั้งที่แก้โฟลเดอร์ `windows/`
GitHub Actions builds, tests and packages the Windows zip on every change under `windows/`.

## ☕ Support this App

If you find **Hosts Manager** helpful, you can support the developer by buying a coffee:
ถ้า **Hosts Manager** มีประโยชน์กับคุณ สามารถสนับสนุนผู้พัฒนาด้วยการเลี้ยงกาแฟได้ที่นี่:

👉&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<a href="https://buymeacoffee.com/cheuachatchai" >![buy_me_a_coffee](https://github.com/conseilgouz/plg_system_cgwebp_j4/assets/19435246/4fda4cb5-64f1-4717-81ae-c71a0fc26c2d)</a>
