<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="brand/colorpack-logo-on-dark.png">
    <img src="brand/colorpack-logo-on-light.png" alt="COLORPACK CREATIONS" width="320">
  </picture>
</p>

<h1 align="center">Hosts Manager</h1>

<p align="center">
  <b>Edit the hosts file during website migrations — with one-click DNS flush</b><br>
  แก้ไฟล์ hosts สำหรับย้ายเว็บไป server ใหม่ พร้อมปุ่มล้าง DNS ในคลิกเดียว
</p>

<p align="center">
  <a href="https://github.com/lungkao/Colorpack-Hosts-Manager/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/lungkao/Colorpack-Hosts-Manager?color=3B8FE0&label=release"></a>
  <img alt="macOS 13+" src="https://img.shields.io/badge/macOS-13%2B-111111?logo=apple">
  <img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-3B8FE0?logo=windows">
  <a href="https://github.com/lungkao/Colorpack-Hosts-Manager/actions/workflows/windows.yml"><img alt="Windows build" src="https://github.com/lungkao/Colorpack-Hosts-Manager/actions/workflows/windows.yml/badge.svg"></a>
</p>

<p align="center">
  <b><a href="#english">English</a></b> · <b><a href="#ภาษาไทย">ภาษาไทย</a></b>
</p>

---

## English

### What it does

When you move a client's website to a new server, you point the domain to the new IP in your own `hosts` file so you can test before changing real DNS. Hosts Manager makes that quick and safe:

- **Point domains to a new server** — add several domains at once, `www.` is added automatically
- **Turn entries on/off** without deleting them
- **Save** writes the hosts file, **flushes DNS** and keeps a backup (`hosts.hostsmanager.bak`)
- **Duplicate detection** — across rows, within a row, and against existing lines in the file, with a warning before saving
- **Import from the existing hosts file** — pick lines to manage, filtered to **real public domains** (internal names like `.local`, `.internal` or bare hostnames are hidden)
- **Check a domain** — what this computer resolves vs. public DNS (1.1.1.1)
- **Raw editor** for the whole file
- **Thai / English** interface, Colorpack design

The app only rewrites lines inside its own block (`# >>> HostsManager >>>` … `# <<< HostsManager <<<`). Everything else in the file is left as is. Both platforms use the same block format.

### Install

Download from **[Releases](https://github.com/lungkao/Colorpack-Hosts-Manager/releases/latest)**.

**macOS 13+ (Apple Silicon & Intel)** — open `HostsManager-<version>.dmg` and drag the app into Applications. The app is not notarized: on first launch use **System Settings › Privacy & Security › Open Anyway**, or run

```
xattr -dr com.apple.quarantine "/Applications/Hosts Manager.app"
```

**Windows 10 / 11** — unzip `HostsManager-<version>-windows.zip` and run `HostsManager.exe`. No installation needed. Accept the UAC prompt (admin rights are required to edit `C:\Windows\System32\drivers\etc\hosts`). SmartScreen may warn on first run — click **More info › Run anyway** (the app is not code-signed).

### Build from source

| Folder | |
|---|---|
| `macos/` | SwiftUI app — `HostsManager.swift`, `build.sh`, `make_dmg.sh`, `icon.swift` |
| `windows/` | C# WinForms (.NET Framework 4.8) — `HostsCore.cs`, `MainForm.cs`, `Dialogs.cs`, `Theme.cs` |
| `brand/` | Colorpack logo (PNG + `logo.swift` renderer) and the Kanit font (SIL OFL 1.1) |

```
# macOS — needs Xcode Command Line Tools
cd macos && ./build.sh --install     # build and install to /Applications
cd macos && ./make_dmg.sh            # dist/HostsManager-<version>.dmg (universal)

# Windows — needs the .NET SDK 8+
dotnet build windows/HostsManager.csproj -c Release
```

GitHub Actions builds the Windows app on every change, runs the logic self-test and a UI smoke test, and captures screenshots of every window.

---

## ภาษาไทย

### ทำอะไรได้บ้าง

ตอนย้ายเว็บลูกค้าไป server ใหม่ เราต้องแก้ไฟล์ `hosts` ในเครื่องให้โดเมนชี้ไป IP ใหม่ เพื่อทดสอบก่อนเปลี่ยน DNS จริง Hosts Manager ช่วยให้ทำเรื่องนี้ได้เร็วและปลอดภัย:

- **ชี้โดเมนไป server ใหม่** — ใส่หลายโดเมนพร้อมกันได้ และเติม `www.` ให้อัตโนมัติ
- **เปิด/ปิดแต่ละรายการ** ได้โดยไม่ต้องลบ
- **บันทึก** แล้วเขียนไฟล์ hosts, **ล้าง DNS** และสำรองไฟล์เดิมไว้ (`hosts.hostsmanager.bak`) ให้อัตโนมัติ
- **ตรวจโดเมนซ้ำ** — ทั้งข้ามแถว ในแถวเดียวกัน และที่ชนกับบรรทัดเดิมในไฟล์ พร้อมเตือนก่อนบันทึก
- **นำเข้าจากไฟล์ hosts เดิม** — เลือกบรรทัดที่จะนำมาจัดการ โดยคัดมาเฉพาะ **โดเมนจริง** (ชื่อภายในอย่าง `.local`, `.internal` หรือชื่อเครื่อง จะถูกซ่อนไว้)
- **ตรวจโดเมน** — เทียบว่าเครื่องนี้เห็น IP อะไร กับ DNS สาธารณะ (1.1.1.1)
- **แก้ไฟล์ดิบ** ได้ทั้งไฟล์
- หน้าจอ **ภาษาไทย / English** ธีม Colorpack

แอปแก้เฉพาะบรรทัดในบล็อกของตัวเอง (`# >>> HostsManager >>>` … `# <<< HostsManager <<<`) ส่วนอื่นของไฟล์ไม่ถูกแตะ ทั้ง Mac และ Windows ใช้รูปแบบบล็อกเดียวกัน

### ติดตั้ง

ดาวน์โหลดจากหน้า **[Releases](https://github.com/lungkao/Colorpack-Hosts-Manager/releases/latest)**

**macOS 13 ขึ้นไป (ชิป Apple และ Intel)** — เปิดไฟล์ `HostsManager-<เวอร์ชัน>.dmg` แล้วลากแอปไปที่ Applications แอปยังไม่ได้ notarize เปิดครั้งแรกให้ไปที่ **System Settings › Privacy & Security › Open Anyway** หรือใช้คำสั่ง

```
xattr -dr com.apple.quarantine "/Applications/Hosts Manager.app"
```

**Windows 10 / 11** — แตกไฟล์ `HostsManager-<เวอร์ชัน>-windows.zip` แล้วเปิด `HostsManager.exe` ได้เลย ไม่ต้องติดตั้ง กด **Yes** ที่หน้าต่าง UAC (ต้องใช้สิทธิ์ admin ในการแก้ `C:\Windows\System32\drivers\etc\hosts`) ครั้งแรก SmartScreen อาจเตือน ให้กด **More info › Run anyway** (แอปยังไม่ได้เซ็นใบรับรอง)

### Build จากซอร์ส

| โฟลเดอร์ | |
|---|---|
| `macos/` | แอป SwiftUI — `HostsManager.swift`, `build.sh`, `make_dmg.sh`, `icon.swift` |
| `windows/` | C# WinForms (.NET Framework 4.8) — `HostsCore.cs`, `MainForm.cs`, `Dialogs.cs`, `Theme.cs` |
| `brand/` | โลโก้ Colorpack (PNG + ตัววาด `logo.swift`) และฟอนต์ Kanit (สัญญาอนุญาต SIL OFL 1.1) |

```
# macOS — ต้องมี Xcode Command Line Tools
cd macos && ./build.sh --install     # build และติดตั้งลง /Applications
cd macos && ./make_dmg.sh            # ได้ dist/HostsManager-<เวอร์ชัน>.dmg (Universal)

# Windows — ต้องมี .NET SDK 8 ขึ้นไป
dotnet build windows/HostsManager.csproj -c Release
```

GitHub Actions จะ build แอป Windows ทุกครั้งที่แก้โค้ด พร้อมทดสอบตรรกะ, smoke test หน้าจอ และถ่ายภาพหน้าจอทุกหน้าต่างเก็บไว้

---

## ☕ Support this App

If you find **Hosts Manager** helpful, you can support the developer by buying a coffee:
ถ้า **Hosts Manager** มีประโยชน์กับคุณ สามารถสนับสนุนผู้พัฒนาด้วยการเลี้ยงกาแฟได้ที่นี่:

👉&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<a href="https://buymeacoffee.com/cheuachatchai" >![buy_me_a_coffee](https://github.com/conseilgouz/plg_system_cgwebp_j4/assets/19435246/4fda4cb5-64f1-4717-81ae-c71a0fc26c2d)</a>

---

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="brand/colorpack-logo-on-dark.png">
    <img src="brand/colorpack-logo-on-light.png" alt="COLORPACK CREATIONS" width="180">
  </picture><br>
  <b>บริษัท คัลเลอร์แพค ครีเอชั่น จำกัด</b><br>
  <sub>COLORPACK CREATION CO., LTD.</sub><br><br>
  © 2026 บริษัท คัลเลอร์แพค ครีเอชั่น จำกัด สงวนลิขสิทธิ์<br>
  © 2026 COLORPACK CREATION CO., LTD. All rights reserved.<br>
  <a href="https://colorpack.co.th/">colorpack.co.th</a> · Hosts Manager v1.5
</p>

<sub>Kanit font © The Kanit Project Authors, licensed under the SIL Open Font License 1.1 (<code>brand/fonts/OFL.txt</code>).</sub>
