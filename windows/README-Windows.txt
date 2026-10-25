Hosts Manager for Windows
=========================

วิธีใช้ (ภาษาไทย)
-----------------
1) แตกไฟล์ zip แล้ววาง HostsManager.exe ไว้ที่ไหนก็ได้ (เช่น C:\Tools\HostsManager)
   ไม่ต้องติดตั้ง ใช้ได้ทันทีบน Windows 10 / 11
2) ดับเบิลคลิก HostsManager.exe — Windows จะถามสิทธิ์ admin (UAC) ให้กด Yes
   เพราะต้องใช้สิทธิ์ admin ในการแก้ไฟล์ C:\Windows\System32\drivers\etc\hosts
3) ครั้งแรก Windows SmartScreen อาจเตือนว่า "Windows protected your PC"
   ให้กด "More info" > "Run anyway" (เพราะแอปยังไม่ได้เซ็นด้วยใบรับรอง)
4) กดบันทึกแล้ว แอปจะล้าง DNS ให้อัตโนมัติ และสำรองไฟล์เดิมไว้ที่
   C:\Windows\System32\drivers\etc\hosts.hostsmanager.bak

หมายเหตุ: Chrome / Edge อาจมีแคช DNS ของตัวเอง ถ้าเว็บยังไม่เปลี่ยน
ให้ปิด "Use secure DNS" ในการตั้งค่าเบราว์เซอร์ หรือเปิด
chrome://net-internals/#dns (edge://net-internals/#dns) แล้วกด Clear host cache
โปรแกรมแอนติไวรัสบางตัวอาจกันการแก้ไฟล์ hosts — ถ้าบันทึกไม่ได้ ให้อนุญาตในแอนติไวรัส

How to use (English)
--------------------
1) Unzip and put HostsManager.exe anywhere (e.g. C:\Tools\HostsManager).
   No installation needed. Works on Windows 10 / 11.
2) Double-click HostsManager.exe and accept the UAC prompt —
   admin rights are required to edit C:\Windows\System32\drivers\etc\hosts.
3) On first run SmartScreen may say "Windows protected your PC".
   Click "More info" > "Run anyway" (the app is not code-signed).
4) Saving flushes DNS automatically and backs up the previous file to
   C:\Windows\System32\drivers\etc\hosts.hostsmanager.bak

Note: Chrome / Edge may cache DNS themselves. If a site doesn't switch,
turn off "Use secure DNS" or open chrome://net-internals/#dns
(edge://net-internals/#dns) and click "Clear host cache".
Some antivirus tools block hosts-file changes; allow it there if saving fails.

https://github.com/lungkao/hosts-manager
