// Hosts Manager — แอปแก้ไฟล์ /etc/hosts สำหรับทีมย้ายเว็บ + ปุ่ม Flush DNS
// Hosts Manager — edit /etc/hosts for site migrations + Flush DNS
// Build: ./build.sh --install   (Xcode Command Line Tools)
// macOS 13+

import SwiftUI
import AppKit
import Network

// MARK: - Constants

let hostsPath = "/etc/hosts"
let beginMarker = "# >>> HostsManager >>>"
let endMarker = "# <<< HostsManager <<<"
let offPrefix = "#off "
let flushCmd = "dscacheutil -flushcache; killall -HUP mDNSResponder"

// MARK: - Language

enum Lang: String, CaseIterable {
    case th, en
}

enum L10n {
    static var lang: Lang = Lang(rawValue: UserDefaults.standard.string(forKey: "lang") ?? "") ?? .th
}

/// เลือกข้อความตามภาษาที่ใช้อยู่
func tr(_ th: String, _ en: String) -> String {
    L10n.lang == .th ? th : en
}

// MARK: - Helpers

extension String {
    var trimmed: String { trimmingCharacters(in: .whitespacesAndNewlines) }
}

func isValidIP(_ s: String) -> Bool {
    let t = s.trimmed
    return IPv4Address(t) != nil || IPv6Address(t) != nil
}

func cleanDomain(_ s: String) -> String {
    var d = s.trimmed.lowercased()
    for p in ["https://", "http://"] where d.hasPrefix(p) {
        d.removeFirst(p.count)
    }
    if let i = d.firstIndex(where: { $0 == "/" || $0 == ":" }) {
        d = String(d[..<i])
    }
    return d
}

/// โดเมนจริง (สาธารณะ): มีจุด, ตัวอักษรถูกต้อง, นามสกุลเป็นตัวอักษร ≥ 2 ตัว และไม่ใช่ชื่อภายใน
let internalSuffixes = ["local", "localhost", "localdomain", "internal", "lan", "home", "test",
                        "invalid", "example", "arpa", "intranet", "corp", "private"]

func isRealDomain(_ host: String) -> Bool {
    let h = host.lowercased().trimmingCharacters(in: CharacterSet(charactersIn: "."))
    let labels = h.split(separator: ".", omittingEmptySubsequences: false).map(String.init)
    guard labels.count >= 2, h.count <= 253 else { return false }
    let allowed = CharacterSet(charactersIn: "abcdefghijklmnopqrstuvwxyz0123456789-")
    for l in labels {
        guard !l.isEmpty, l.count <= 63, !l.hasPrefix("-"), !l.hasSuffix("-"),
              l.unicodeScalars.allSatisfy({ allowed.contains($0) }) || l.hasPrefix("xn--") else { return false }
    }
    let tld = labels.last!
    guard tld.count >= 2, tld.allSatisfy({ $0.isLetter }) || tld.hasPrefix("xn--") else { return false }
    return !internalSuffixes.contains(tld)
}

/// single-quote สำหรับ shell
func shq(_ s: String) -> String {
    "'" + s.replacingOccurrences(of: "'", with: "'\\''") + "'"
}

/// รันคำสั่งธรรมดา (ไม่ต้องใช้สิทธิ์ admin)
func shell(_ path: String, _ args: [String]) -> String {
    let p = Process()
    p.executableURL = URL(fileURLWithPath: path)
    p.arguments = args
    let pipe = Pipe()
    p.standardOutput = pipe
    p.standardError = pipe
    do { try p.run() } catch { return "" }
    let data = pipe.fileHandleForReading.readDataToEndOfFile()
    p.waitUntilExit()
    return String(data: data, encoding: .utf8) ?? ""
}

/// รันคำสั่งด้วยสิทธิ์ admin (macOS จะถามรหัสผ่าน)
func runAdmin(_ cmd: String) -> (ok: Bool, message: String) {
    let escaped = cmd
        .replacingOccurrences(of: "\\", with: "\\\\")
        .replacingOccurrences(of: "\"", with: "\\\"")
    let src = "do shell script \"\(escaped)\" with administrator privileges"
    var err: NSDictionary?
    let result = NSAppleScript(source: src)?.executeAndReturnError(&err)
    if let err {
        let num = err[NSAppleScript.errorNumber] as? Int ?? 0
        if num == -128 { return (false, tr("ยกเลิกแล้ว", "Cancelled")) }
        return (false, err[NSAppleScript.errorMessage] as? String ?? tr("เกิดข้อผิดพลาด", "Unknown error"))
    }
    return (true, result?.stringValue ?? "")
}

func nowString() -> String {
    let f = DateFormatter()
    f.dateFormat = "HH:mm:ss"
    return f.string(from: Date())
}

// MARK: - Model

struct HostEntry: Identifiable, Equatable {
    var id = UUID()
    var enabled: Bool = true
    var ip: String = ""
    var hosts: String = ""
    var note: String = ""

    var isValid: Bool { !ip.trimmed.isEmpty && !hosts.trimmed.isEmpty }

    var hostList: [String] {
        hosts.split(whereSeparator: \.isWhitespace).map(String.init)
    }

    func line() -> String {
        var seen = Set<String>()
        let hs = hostList.filter { seen.insert($0.lowercased()).inserted }
        var s = "\(ip.trimmed)\t\(hs.joined(separator: " "))"
        if !note.trimmed.isEmpty { s += "\t# \(note.trimmed)" }
        return enabled ? s : offPrefix + s
    }

    static func parse(_ raw: String) -> HostEntry? {
        var line = raw.trimmingCharacters(in: .whitespaces)
        var enabled = true
        if line.hasPrefix(offPrefix) {
            enabled = false
            line = String(line.dropFirst(offPrefix.count))
        } else if line.hasPrefix("#") {
            return nil
        }
        var note = ""
        if let r = line.range(of: "#") {
            note = String(line[r.upperBound...]).trimmed
            line = String(line[..<r.lowerBound])
        }
        let parts = line.split(whereSeparator: \.isWhitespace).map(String.init)
        guard parts.count >= 2 else { return nil }
        return HostEntry(enabled: enabled,
                         ip: parts[0],
                         hosts: parts.dropFirst().joined(separator: " "),
                         note: note)
    }
}

// MARK: - Store

@MainActor
final class HostsStore: ObservableObject {
    @Published var entries: [HostEntry] = []
    @Published var prefix: [String] = []   // บรรทัดก่อนบล็อกของแอป
    @Published var suffix: [String] = []   // บรรทัดหลังบล็อกของแอป
    @Published var savedSnapshot = ""
    @Published var status = ""
    @Published var lastIP = ""

    @Published var checkDomain = ""
    @Published var checkResult = ""
    @Published var checking = false

    // สถานะหน้าจอ (เก็บไว้ที่นี่เพื่อให้ compile ได้ด้วย Command Line Tools)
    @Published var tab = 0
    @Published var showAdd = false
    @Published var rawDraft = ""
    @Published var showImport = false
    @Published var importCandidates: [ImportCandidate] = []
    @Published var importFilter = ""
    @Published var importShowAll = false

    @Published var lang: Lang = L10n.lang {
        didSet {
            L10n.lang = lang
            UserDefaults.standard.set(lang.rawValue, forKey: "lang")
        }
    }

    var composed: String {
        var out = prefix
        while let last = out.last, last.trimmed.isEmpty { out.removeLast() }
        let valid = entries.filter(\.isValid)
        if !valid.isEmpty {
            out.append("")
            out.append(beginMarker)
            out += valid.map { $0.line() }
            out.append(endMarker)
        }
        out += suffix
        return out.joined(separator: "\n") + "\n"
    }

    var isDirty: Bool { composed != savedSnapshot }

    private var didInitialLoad = false
    /// โหลดครั้งแรกครั้งเดียว (กันไม่ให้การเปลี่ยนภาษาทิ้งงานที่ยังไม่บันทึก)
    func loadOnce() {
        guard !didInitialLoad else { return }
        didInitialLoad = true
        load()
    }

    // MARK: ตรวจความซ้ำซ้อน / Duplicate checks

    static let reservedHosts: Set<String> = ["localhost", "broadcasthost", "ip6-localhost", "ip6-loopback"]

    struct Issue {
        var text: String
        var severe: Bool
    }

    /// โดเมนในส่วนอื่นของไฟล์ (นอกบล็อกของแอป): host -> (ip, อยู่ก่อนบล็อกหรือไม่)
    var systemHosts: [String: (ip: String, before: Bool)] {
        var d: [String: (ip: String, before: Bool)] = [:]
        for (lines, before) in [(prefix, true), (suffix, false)] {
            for l in lines {
                guard let e = HostEntry.parse(l), e.enabled else { continue }
                for h in e.hostList.map({ $0.lowercased() }) where d[h] == nil {
                    d[h] = (e.ip, before)
                }
            }
        }
        return d
    }

    /// ปัญหาความซ้ำของแต่ละแถว (key = id ของแถว)
    var issues: [UUID: [Issue]] {
        let sys = systemHosts
        var firstOwner: [String: HostEntry] = [:]
        var out: [UUID: [Issue]] = [:]
        func add(_ id: UUID, _ text: String, _ severe: Bool) {
            out[id, default: []].append(Issue(text: text, severe: severe))
        }
        for e in entries where e.isValid {
            let ip = e.ip.trimmed
            var seenInRow = Set<String>()
            for h in e.hostList.map({ $0.lowercased() }) {
                if !seenInRow.insert(h).inserted {
                    add(e.id, tr("\(h) ใส่ซ้ำในแถวเดียวกัน (จะบันทึกแค่ครั้งเดียว)",
                                 "\(h) is listed twice in this row (saved once)"), false)
                    continue
                }
                if let s = sys[h] {
                    if s.before {
                        add(e.id, tr("\(h) มีอยู่แล้วในส่วนอื่นของไฟล์ (\(s.ip)) ซึ่งจะถูกใช้ก่อน — กด “เลือกนำเข้ารายการเดิม” เพื่อรวมมาจัดการ",
                                     "\(h) already exists elsewhere in the file (\(s.ip)) and takes priority — use “Choose entries to import” to manage it here"),
                            e.enabled && s.ip != ip)
                    } else {
                        add(e.id, tr("\(h) มีในส่วนท้ายของไฟล์ด้วย (\(s.ip)) แต่แถวนี้จะถูกใช้ก่อน",
                                     "\(h) also appears later in the file (\(s.ip)), but this row wins"), false)
                    }
                }
                if let o = firstOwner[h] {
                    let same = o.ip.trimmed == ip
                    let bothOn = o.enabled && e.enabled
                    let text: String
                    if bothOn && !same {
                        text = tr("\(h) ซ้ำกับแถวด้านบนที่ชี้ไป \(o.ip.trimmed) — ระบบจะใช้แถวด้านบน แถวนี้ไม่มีผล",
                                  "\(h) duplicates a row above pointing to \(o.ip.trimmed) — the row above wins, this one has no effect")
                    } else if same {
                        text = tr("\(h) ซ้ำกับแถวด้านบน (IP เดียวกัน) — ลบแถวใดแถวหนึ่งได้",
                                  "\(h) duplicates a row above (same IP) — you can delete one")
                    } else {
                        text = tr("\(h) ซ้ำกับอีกแถวที่ชี้ไป \(o.ip.trimmed) (มีแถวหนึ่งปิดอยู่)",
                                  "\(h) duplicates another row pointing to \(o.ip.trimmed) (one of them is off)")
                    }
                    add(e.id, text, bothOn && !same)
                    add(o.id, tr("\(h) มีซ้ำในแถวด้านล่าง", "\(h) is duplicated in a row below"), false)
                } else {
                    firstOwner[h] = e
                }
            }
        }
        return out
    }

    var severeCount: Int { issues.values.flatMap { $0 }.filter(\.severe).count }

    private func importable(_ line: String) -> HostEntry? {
        guard let e = HostEntry.parse(line), e.enabled else { return nil }
        let hs = Set(e.hostList.map { $0.lowercased() })
        return hs.isDisjoint(with: Self.reservedHosts) ? e : nil
    }

    /// จำนวนบรรทัดเดิมที่เป็นโดเมนจริงและยังไม่ได้อยู่ในการจัดการของแอป
    var importableCount: Int {
        (prefix + suffix).filter { l in
            guard let e = importable(l) else { return false }
            return e.hostList.contains { isRealDomain($0) }
        }.count
    }

    var hiddenNonDomainCount: Int { importCandidates.filter { !$0.realDomain }.count }

    struct ImportCandidate: Identifiable {
        var id = UUID()
        var entry: HostEntry
        var fromPrefix: Bool
        var lineIndex: Int
        var selected = false
        var alreadyManaged = false
        var realDomain = true
    }

    /// เปิดหน้าต่างเลือกบรรทัดเดิมที่จะนำเข้า
    func openImport() {
        let managed = Set(entries.flatMap { $0.hostList.map { $0.lowercased() } })
        var list: [ImportCandidate] = []
        for (lines, isPrefix) in [(prefix, true), (suffix, false)] {
            for (i, l) in lines.enumerated() {
                guard let e = importable(l) else { continue }
                let dup = !Set(e.hostList.map { $0.lowercased() }).isDisjoint(with: managed)
                let real = e.hostList.contains { isRealDomain($0) }
                list.append(ImportCandidate(entry: e, fromPrefix: isPrefix, lineIndex: i,
                                            alreadyManaged: dup, realDomain: real))
            }
        }
        importCandidates = list
        importFilter = ""
        importShowAll = false
        showImport = true
    }

    func candidateMatches(_ c: ImportCandidate) -> Bool {
        if !importShowAll && !c.realDomain { return false }
        let q = importFilter.trimmed.lowercased()
        if q.isEmpty { return true }
        return c.entry.ip.lowercased().contains(q)
            || c.entry.hosts.lowercased().contains(q)
            || c.entry.note.lowercased().contains(q)
    }

    /// เลือก/ไม่เลือกเฉพาะรายการที่ตรงกับคำค้นอยู่
    func selectVisibleCandidates(_ on: Bool) {
        for i in importCandidates.indices where candidateMatches(importCandidates[i]) {
            importCandidates[i].selected = on
        }
    }

    /// ย้ายบรรทัดที่เลือกเข้ามาอยู่ในบล็อกของแอป โดยรักษาลำดับความสำคัญเดิม
    func importSelected() {
        let chosen = importCandidates.filter(\.selected)
        guard !chosen.isEmpty else { showImport = false; return }
        func mark(_ e: HostEntry) -> HostEntry {
            var e = e
            if e.note.isEmpty { e.note = tr("นำเข้าจากไฟล์เดิม", "imported") }
            return e
        }
        let pre = chosen.filter { $0.fromPrefix }
        let post = chosen.filter { !$0.fromPrefix }
        let preIdx = Set(pre.map(\.lineIndex))
        let postIdx = Set(post.map(\.lineIndex))
        prefix = prefix.enumerated().filter { !preIdx.contains($0.offset) }.map(\.element)
        suffix = suffix.enumerated().filter { !postIdx.contains($0.offset) }.map(\.element)
        entries = pre.map { mark($0.entry) } + entries + post.map { mark($0.entry) }
        showImport = false
        status = tr("นำเข้า \(chosen.count) รายการจากไฟล์เดิม — ตรวจดูแล้วกดบันทึก",
                    "Imported \(chosen.count) existing entries — review, then Save")
    }

    // MARK: Load / parse

    func load() {
        do {
            let text = try String(contentsOfFile: hostsPath, encoding: .utf8)
            parse(text)
            savedSnapshot = composed
            if lastIP.isEmpty, let ip = entries.last?.ip { lastIP = ip }
            status = tr("โหลด /etc/hosts แล้ว (\(nowString()))", "Loaded /etc/hosts (\(nowString()))")
        } catch {
            status = tr("อ่านไฟล์ไม่ได้: ", "Can't read file: ") + error.localizedDescription
        }
    }

    func parse(_ text: String) {
        var pre: [String] = [], mid: [String] = [], post: [String] = []
        var state = 0
        var lines = text.components(separatedBy: "\n")
        if lines.last == "" { lines.removeLast() }
        for l in lines {
            let t = l.trimmed
            if state == 0 && t == beginMarker { state = 1; continue }
            if state == 1 && t == endMarker { state = 2; continue }
            switch state {
            case 0: pre.append(l)
            case 1: mid.append(l)
            default: post.append(l)
            }
        }
        prefix = pre
        suffix = post
        entries = mid.compactMap(HostEntry.parse)
    }

    // MARK: Edit

    func add(ip: String, domains: [String], addWWW: Bool, note: String) {
        var added = 0, updated = 0
        for raw in domains {
            let d = cleanDomain(raw)
            guard !d.isEmpty else { continue }
            let base = d.hasPrefix("www.") ? String(d.dropFirst(4)) : d
            let hosts = addWWW ? "\(base) www.\(base)" : d
            if let i = entries.firstIndex(where: { $0.hostList.contains(base) || $0.hostList.contains(d) }) {
                entries[i].ip = ip.trimmed
                entries[i].enabled = true
                if !note.trimmed.isEmpty { entries[i].note = note.trimmed }
                updated += 1
            } else {
                entries.append(HostEntry(ip: ip.trimmed, hosts: hosts, note: note.trimmed))
                added += 1
            }
        }
        lastIP = ip.trimmed
        status = tr("เพิ่ม \(added) / อัปเดต \(updated) รายการ — อย่าลืมกดบันทึก",
                    "Added \(added) / updated \(updated) — don't forget to Save")
    }

    func delete(_ id: UUID) {
        DispatchQueue.main.async { self.entries.removeAll { $0.id == id } }
    }

    func setAll(_ on: Bool) {
        for i in entries.indices { entries[i].enabled = on }
    }

    // MARK: Save / flush

    func save() {
        if let bad = entries.first(where: { $0.isValid && !isValidIP($0.ip) }) {
            status = tr("IP ไม่ถูกต้อง: ", "Invalid IP: ") + "\(bad.ip) (\(bad.hosts))"
            return
        }
        let severe = severeCount
        if severe > 0 {
            let a = NSAlert()
            a.messageText = tr("พบโดเมนซ้ำที่ชี้คนละ IP \(severe) จุด",
                               "Found \(severe) duplicate domain(s) pointing to different IPs")
            a.informativeText = tr("บางแถวจะไม่มีผล เพราะมีบรรทัดอื่นที่ถูกใช้ก่อน ต้องการบันทึกต่อหรือไม่?",
                                   "Some rows will have no effect because another line takes priority. Save anyway?")
            a.alertStyle = .warning
            a.addButton(withTitle: tr("กลับไปแก้", "Go back"))
            a.addButton(withTitle: tr("บันทึกต่อ", "Save anyway"))
            if a.runModal() != .alertSecondButtonReturn {
                status = tr("ยังไม่ได้บันทึก — แก้แถวที่มีเครื่องหมาย ⚠️ ก่อน",
                            "Not saved — fix the rows marked ⚠️ first")
                return
            }
        }
        let text = composed
        let tmp = FileManager.default.temporaryDirectory
            .appendingPathComponent("hosts-\(UUID().uuidString)")
        do {
            try text.write(to: tmp, atomically: true, encoding: .utf8)
        } catch {
            status = tr("เขียนไฟล์ชั่วคราวไม่ได้: ", "Can't write temp file: ") + error.localizedDescription
            return
        }
        defer { try? FileManager.default.removeItem(at: tmp) }

        let cmd = "cp /etc/hosts /etc/hosts.hostsmanager.bak && cat \(shq(tmp.path)) > /etc/hosts && \(flushCmd)"
        let r = runAdmin(cmd)
        if r.ok {
            load()
            status = tr("✅ บันทึกและ Flush DNS แล้ว (\(nowString())) — สำรองไฟล์เดิมไว้ที่ /etc/hosts.hostsmanager.bak",
                        "✅ Saved and flushed DNS (\(nowString())) — backup at /etc/hosts.hostsmanager.bak")
        } else {
            status = tr("❌ บันทึกไม่สำเร็จ: ", "❌ Save failed: ") + r.message
        }
    }

    func flush() {
        let r = runAdmin(flushCmd)
        status = r.ok
            ? tr("✅ Flush DNS แล้ว (\(nowString()))", "✅ DNS flushed (\(nowString()))")
            : tr("❌ Flush ไม่สำเร็จ: ", "❌ Flush failed: ") + r.message
    }

    // MARK: Check

    func check(_ domain: String? = nil) {
        if let domain { checkDomain = domain }
        let d = cleanDomain(checkDomain)
        guard !d.isEmpty else { return }
        checking = true
        checkResult = ""
        Task.detached {
            let local = shell("/usr/bin/dscacheutil", ["-q", "host", "-a", "name", d])
            let localIPs = local.split(separator: "\n")
                .filter { $0.hasPrefix("ip_address") || $0.hasPrefix("ipv6_address") }
                .compactMap { $0.split(separator: ":", maxSplits: 1).last.map { String($0).trimmed } }
            let pub = shell("/usr/bin/dig", ["+short", "@1.1.1.1", d, "A"])
                .split(separator: "\n").map(String.init)
                .filter { isValidIP($0) }

            await MainActor.run {
                var out = tr("โดเมน: ", "Domain: ") + d + "\n"
                out += tr("เครื่องนี้เห็น (hosts/แคช):  ", "This Mac sees (hosts/cache): ")
                    + (localIPs.isEmpty ? "-" : localIPs.joined(separator: ", ")) + "\n"
                out += tr("DNS สาธารณะ (1.1.1.1):     ", "Public DNS (1.1.1.1):         ")
                    + (pub.isEmpty ? "-" : pub.joined(separator: ", "))
                if !localIPs.isEmpty && !pub.isEmpty {
                    out += Set(localIPs).isDisjoint(with: Set(pub))
                        ? tr("\n→ เครื่องนี้ชี้ไปคนละที่กับ DNS จริง (กำลังใช้ค่าจาก hosts)",
                             "\n→ This Mac points somewhere different from real DNS (using hosts)")
                        : tr("\n→ ตรงกับ DNS จริงแล้ว", "\n→ Matches real DNS")
                }
                self.checkResult = out
                self.checking = false
            }
        }
    }
}

// MARK: - Views

struct EntryRow: View {
    @Binding var entry: HostEntry
    var issues: [HostsStore.Issue]
    var onCheck: () -> Void
    var onDelete: () -> Void

    private var hasSevere: Bool { issues.contains { $0.severe } }

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(spacing: 8) {
                Toggle("", isOn: $entry.enabled)
                    .labelsHidden()
                    .toggleStyle(.switch)
                    .controlSize(.mini)
                    .frame(width: 40, alignment: .leading)
                    .help(entry.enabled ? tr("กำลังใช้งาน", "Active") : tr("ปิดอยู่ (ไม่มีผล)", "Off (no effect)"))
                TextField("IP", text: $entry.ip)
                    .font(.system(.body, design: .monospaced))
                    .foregroundStyle(isValidIP(entry.ip) ? Color.primary : Color.red)
                    .frame(width: 150)
                TextField(tr("โดเมน", "Domains"), text: $entry.hosts)
                    .font(.system(.body, design: .monospaced))
                    .overlay(RoundedRectangle(cornerRadius: 5)
                        .stroke(Color.orange, lineWidth: hasSevere ? 1.5 : 0))
                TextField(tr("หมายเหตุ", "Note"), text: $entry.note)
                    .frame(width: 150)
                Button(action: onCheck) {
                    Label(tr("ตรวจ", "Check"), systemImage: "magnifyingglass")
                }
                .help(tr("ตรวจว่าโดเมนนี้ชี้ไปที่ไหน", "See where this domain resolves"))
                Button {
                    if let h = entry.hostList.first, let url = URL(string: "http://\(h)") {
                        NSWorkspace.shared.open(url)
                    }
                } label: {
                    Label(tr("เปิดเว็บ", "Open"), systemImage: "safari")
                }
                .help(tr("เปิดเว็บในเบราว์เซอร์", "Open in browser"))
                Button(role: .destructive, action: onDelete) {
                    Label(tr("ลบ", "Delete"), systemImage: "trash")
                }
                .help(tr("ลบแถวนี้", "Delete this row"))
            }
            .textFieldStyle(.roundedBorder)
            .buttonStyle(.bordered)
            .controlSize(.small)
            .labelStyle(.titleAndIcon)

            if !issues.isEmpty {
                VStack(alignment: .leading, spacing: 2) {
                    ForEach(issues.indices, id: \.self) { i in
                        Label(issues[i].text,
                              systemImage: issues[i].severe ? "exclamationmark.triangle.fill" : "info.circle")
                            .font(.caption)
                            .foregroundStyle(issues[i].severe ? Color.orange : Color.secondary)
                    }
                }
                .padding(.leading, 48)
            }
        }
        .opacity(entry.enabled ? 1 : 0.5)
    }
}

struct EntriesView: View {
    @ObservedObject var store: HostsStore

    var body: some View {
        if store.entries.isEmpty {
            VStack(spacing: 12) {
                Spacer()
                Image(systemName: "server.rack").font(.system(size: 40)).foregroundStyle(.secondary)
                Text(tr("ยังไม่มีรายการที่ชี้ไป server ใหม่", "No domains pointed to a new server yet"))
                Button(tr("เพิ่มรายการแรก", "Add first entry")) { store.showAdd = true }
                if store.importableCount > 0 {
                    Button(tr("เลือกนำเข้าจากไฟล์ hosts เดิม (\(store.importableCount) โดเมน)",
                              "Import from existing hosts file (\(store.importableCount) domains)…")) {
                        store.openImport()
                    }
                }
                Spacer()
            }
            .frame(maxWidth: .infinity)
        } else {
            let issues = store.issues
            let severe = issues.values.flatMap { $0 }.filter(\.severe).count
            let importableCount = store.importableCount
            VStack(spacing: 0) {
                if severe > 0 || importableCount > 0 {
                    HStack(spacing: 12) {
                        if severe > 0 {
                            Label(tr("พบโดเมนซ้ำที่ชี้คนละ IP \(severe) จุด",
                                     "\(severe) duplicate domain(s) point to different IPs"),
                                  systemImage: "exclamationmark.triangle.fill")
                                .foregroundStyle(.orange)
                        }
                        if importableCount > 0 {
                            Text(tr("มี \(importableCount) โดเมนในไฟล์เดิมที่แอปยังไม่ได้จัดการ",
                                    "\(importableCount) domain(s) in the file aren't managed by this app"))
                                .foregroundStyle(.secondary)
                            Button(tr("เลือกนำเข้ารายการเดิม…", "Choose entries to import…")) { store.openImport() }
                        }
                        Spacer()
                    }
                    .font(.callout)
                    .padding(.horizontal, 14)
                    .padding(.vertical, 8)
                    .background(Color.orange.opacity(0.08))
                }
                List {
                    HStack(spacing: 8) {
                        Text(tr("ใช้งาน", "On")).frame(width: 40, alignment: .leading)
                        Text("IP").frame(width: 150, alignment: .leading)
                        Text(tr("โดเมน", "Domains")).frame(maxWidth: .infinity, alignment: .leading)
                        Text(tr("หมายเหตุ", "Note")).frame(width: 150, alignment: .leading)
                        Spacer().frame(width: 230)
                    }
                    .font(.caption).foregroundStyle(.secondary)

                    ForEach($store.entries) { $e in
                        EntryRow(entry: $e,
                                 issues: issues[e.id] ?? [],
                                 onCheck: { store.check(e.hostList.first ?? "") },
                                 onDelete: { store.delete(e.id) })
                    }
                }
            }
        }
    }
}

struct RawView: View {
    @ObservedObject var store: HostsStore

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(tr("แก้ทั้งไฟล์ /etc/hosts ได้โดยตรง กด “ใช้ข้อความนี้” แล้วกด “บันทึก” ด้านบน",
                    "Edit the whole /etc/hosts file. Click “Apply text”, then “Save” above."))
                .font(.caption).foregroundStyle(.secondary)
            TextEditor(text: $store.rawDraft)
                .font(.system(.body, design: .monospaced))
                .border(Color.secondary.opacity(0.3))
            HStack {
                Button {
                    store.rawDraft = store.composed
                } label: {
                    Label(tr("รีเซ็ตเป็นค่าปัจจุบัน", "Reset to current"), systemImage: "arrow.uturn.backward")
                }
                Spacer()
                Button {
                    store.parse(store.rawDraft)
                    store.rawDraft = store.composed
                    store.status = tr("นำข้อความไปใช้แล้ว — กดบันทึกเพื่อเขียนลงไฟล์",
                                      "Text applied — click Save to write the file")
                } label: {
                    Label(tr("ใช้ข้อความนี้", "Apply text"), systemImage: "checkmark")
                }
                .keyboardShortcut(.return, modifiers: .command)
            }
            .labelStyle(.titleAndIcon)
        }
        .padding()
        .onAppear { store.rawDraft = store.composed }
    }
}

struct CheckPanel: View {
    @ObservedObject var store: HostsStore

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text(tr("ตรวจโดเมน:", "Check domain:"))
                TextField("example.com", text: $store.checkDomain)
                    .textFieldStyle(.roundedBorder)
                    .frame(maxWidth: 280)
                    .onSubmit { store.check() }
                Button {
                    store.check()
                } label: {
                    Label(tr("ตรวจ", "Check"), systemImage: "magnifyingglass")
                }
                .labelStyle(.titleAndIcon)
                if store.checking { ProgressView().controlSize(.small) }
                Spacer()
            }
            if !store.checkResult.isEmpty {
                Text(store.checkResult)
                    .font(.system(.caption, design: .monospaced))
                    .textSelection(.enabled)
            }
        }
        .padding(10)
    }
}

final class AddForm: ObservableObject {
    @Published var ip = ""
    @Published var domains = ""
    @Published var addWWW = true
    @Published var note = ""
}

struct AddEntrySheet: View {
    @ObservedObject var store: HostsStore
    @ObservedObject var form: AddForm
    @Environment(\.dismiss) private var dismiss

    private var domainList: [String] {
        form.domains.split(whereSeparator: { $0.isWhitespace || $0 == "," }).map(String.init)
    }
    private var canAdd: Bool { isValidIP(form.ip) && !domainList.isEmpty }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(tr("ชี้โดเมนไป server ใหม่", "Point domains to a new server")).font(.headline)
            Form {
                TextField(tr("IP server ใหม่", "New server IP"), text: $form.ip)
                    .foregroundStyle(form.ip.isEmpty || isValidIP(form.ip) ? Color.primary : Color.red)
                TextField(tr("โดเมน", "Domains"), text: $form.domains,
                          prompt: Text(tr("ใส่ได้หลายโดเมน คั่นด้วยเว้นวรรคหรือ ,",
                                          "Several allowed, separated by space or comma")))
                Toggle(tr("เพิ่ม www. ให้อัตโนมัติ", "Add www. automatically"), isOn: $form.addWWW)
                TextField(tr("หมายเหตุ", "Note"), text: $form.note,
                          prompt: Text(tr("เช่น ชื่อลูกค้า", "e.g. client name")))
            }
            Text(tr("ถ้าโดเมนมีอยู่แล้ว จะอัปเดต IP ให้แทนการเพิ่มซ้ำ",
                    "If a domain already exists, its IP is updated instead of adding a duplicate"))
                .font(.caption).foregroundStyle(.secondary)
            HStack {
                Spacer()
                Button(tr("ยกเลิก", "Cancel")) { dismiss() }.keyboardShortcut(.cancelAction)
                Button(tr("เพิ่ม", "Add")) {
                    store.add(ip: form.ip, domains: domainList, addWWW: form.addWWW, note: form.note)
                    dismiss()
                }
                .keyboardShortcut(.defaultAction)
                .disabled(!canAdd)
            }
        }
        .padding(20)
        .frame(width: 500)
        .onAppear {
            form.ip = store.lastIP
            form.domains = ""
            form.note = ""
        }
    }
}

/// หน้าต่างเลือกบรรทัดเดิมที่จะนำเข้า
struct ImportSheet: View {
    @ObservedObject var store: HostsStore

    private var selectedCount: Int { store.importCandidates.filter(\.selected).count }

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(tr("เลือกบรรทัดจากไฟล์ hosts เดิมที่จะนำเข้ามาจัดการ",
                    "Choose lines from the existing hosts file to manage here"))
                .font(.headline)
            Text(tr("บรรทัดที่ไม่ได้เลือกจะอยู่ในไฟล์เหมือนเดิม ไม่ถูกลบ · บรรทัดของระบบ (localhost) ไม่แสดงในนี้",
                    "Unselected lines stay in the file untouched · system lines (localhost) are not listed"))
                .font(.caption).foregroundStyle(.secondary)

            HStack {
                TextField(tr("ค้นหา IP / โดเมน / หมายเหตุ", "Search IP / domain / note"), text: $store.importFilter)
                    .textFieldStyle(.roundedBorder)
                Button {
                    store.selectVisibleCandidates(true)
                } label: {
                    Label(tr("เลือกทั้งหมด", "Select all"), systemImage: "checkmark.square")
                }
                Button {
                    store.selectVisibleCandidates(false)
                } label: {
                    Label(tr("ไม่เลือกเลย", "Select none"), systemImage: "square")
                }
            }
            .labelStyle(.titleAndIcon)

            if store.hiddenNonDomainCount > 0 {
                Toggle(tr("แสดงทั้งหมด รวม \(store.hiddenNonDomainCount) บรรทัดที่ไม่ใช่โดเมนจริง (เช่น .local, .internal, ชื่อเครื่อง)",
                          "Show all, including \(store.hiddenNonDomainCount) non-public names (.local, .internal, hostnames)"),
                       isOn: $store.importShowAll)
                    .toggleStyle(.checkbox)
                    .font(.callout)
            }

            List {
                ForEach($store.importCandidates) { $c in
                    if store.candidateMatches(c) {
                        HStack(spacing: 10) {
                            Toggle("", isOn: $c.selected)
                                .toggleStyle(.checkbox)
                                .labelsHidden()
                            Text(c.entry.ip)
                                .font(.system(.body, design: .monospaced))
                                .frame(width: 150, alignment: .leading)
                            Text(c.entry.hosts)
                                .font(.system(.body, design: .monospaced))
                                .lineLimit(1)
                                .truncationMode(.tail)
                                .frame(maxWidth: .infinity, alignment: .leading)
                            if !c.realDomain {
                                Text(tr("ไม่ใช่โดเมนจริง", "not public"))
                                    .font(.caption).foregroundStyle(.secondary)
                            }
                            if c.alreadyManaged {
                                Label(tr("มีในรายการแล้ว", "Already listed"), systemImage: "exclamationmark.triangle.fill")
                                    .font(.caption).foregroundStyle(.orange)
                            }
                            if !c.entry.note.isEmpty {
                                Text(c.entry.note).font(.caption).foregroundStyle(.secondary).lineLimit(1)
                                    .frame(maxWidth: 160, alignment: .leading)
                            }
                            Text(c.fromPrefix ? tr("ต้นไฟล์", "top") : tr("ท้ายไฟล์", "bottom"))
                                .font(.caption2).foregroundStyle(.secondary)
                                .frame(width: 50, alignment: .trailing)
                        }
                        .contentShape(Rectangle())
                        .onTapGesture { c.selected.toggle() }
                    }
                }
            }
            .frame(minHeight: 320)

            HStack {
                Text(tr("เลือกแล้ว \(selectedCount) จาก \(store.importCandidates.count) บรรทัด",
                        "\(selectedCount) of \(store.importCandidates.count) selected"))
                    .font(.callout).foregroundStyle(.secondary)
                Spacer()
                Button(tr("ยกเลิก", "Cancel")) { store.showImport = false }
                    .keyboardShortcut(.cancelAction)
                Button(tr("นำเข้า \(selectedCount) รายการ", "Import \(selectedCount)")) { store.importSelected() }
                    .keyboardShortcut(.defaultAction)
                    .buttonStyle(.borderedProminent)
                    .disabled(selectedCount == 0)
            }
        }
        .padding(20)
        .frame(width: 820, height: 560)
    }
}

/// แถบคำสั่งด้านบน — ทุกปุ่มมีทั้งไอคอนและชื่อ
struct ActionBar: View {
    @ObservedObject var store: HostsStore

    var body: some View {
        HStack(spacing: 8) {
            Picker("", selection: $store.tab) {
                Text(tr("รายการโดเมน", "Domains")).tag(0)
                Text(tr("แก้ไฟล์ดิบ", "Raw file")).tag(1)
            }
            .pickerStyle(.segmented)
            .labelsHidden()
            .fixedSize()

            Spacer(minLength: 12)

            Button { store.showAdd = true } label: {
                Label(tr("เพิ่ม", "Add"), systemImage: "plus")
            }
            .keyboardShortcut("n", modifiers: .command)
            .help(tr("เพิ่มโดเมน (⌘N)", "Add domains (⌘N)"))

            Menu {
                Button(tr("เปิดทั้งหมด", "Turn all on")) { store.setAll(true) }
                Button(tr("ปิดทั้งหมด", "Turn all off")) { store.setAll(false) }
            } label: {
                Label(tr("เปิด/ปิดทั้งหมด", "All on/off"), systemImage: "switch.2")
            }
            .fixedSize()

            Button { store.load() } label: {
                Label(tr("โหลดใหม่", "Reload"), systemImage: "arrow.clockwise")
            }
            .help(tr("อ่าน /etc/hosts ใหม่ (ทิ้งการแก้ที่ยังไม่บันทึก)", "Re-read /etc/hosts (discards unsaved edits)"))

            Button { store.flush() } label: {
                Label(tr("ล้าง DNS", "Flush DNS"), systemImage: "bolt.horizontal.circle")
            }
            .help(tr("ล้างแคช DNS ของเครื่อง", "Clear this Mac's DNS cache"))

            Button { store.save() } label: {
                Label(tr("บันทึก", "Save"), systemImage: "square.and.arrow.down")
            }
            .buttonStyle(.borderedProminent)
            .keyboardShortcut("s", modifiers: .command)
            .disabled(!store.isDirty)
            .help(tr("บันทึกลง /etc/hosts และ Flush DNS (⌘S)", "Write /etc/hosts and flush DNS (⌘S)"))

            Divider().frame(height: 20)

            Picker("", selection: $store.lang) {
                Text("ไทย").tag(Lang.th)
                Text("EN").tag(Lang.en)
            }
            .pickerStyle(.segmented)
            .labelsHidden()
            .fixedSize()
            .help("ภาษา / Language")
        }
        .labelStyle(.titleAndIcon)
        .buttonStyle(.bordered)
        .padding(10)
    }
}

struct ContentView: View {
    @StateObject private var store = HostsStore()
    @StateObject private var addForm = AddForm()

    var body: some View {
        VStack(spacing: 0) {
            ActionBar(store: store)
            Divider()

            if store.tab == 0 {
                EntriesView(store: store)
            } else {
                RawView(store: store)
            }

            Divider()
            CheckPanel(store: store)
            Divider()

            HStack {
                Text(store.status).font(.caption).foregroundStyle(.secondary).lineLimit(2)
                Spacer()
                if store.isDirty {
                    Label(tr("ยังไม่ได้บันทึก", "Unsaved changes"), systemImage: "circle.fill")
                        .font(.caption).foregroundStyle(.orange)
                }
            }
            .padding(8)
        }
        .id(store.lang)   // วาดหน้าจอใหม่ทั้งหมดเมื่อเปลี่ยนภาษา
        .navigationTitle("Hosts Manager")
        .frame(minWidth: 1040, minHeight: 560)
        .sheet(isPresented: $store.showAdd) { AddEntrySheet(store: store, form: addForm) }
        .sheet(isPresented: $store.showImport) { ImportSheet(store: store) }
        .onAppear { store.loadOnce() }
    }
}

// MARK: - App

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

@main
struct HostsManagerApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate

    var body: some Scene {
        WindowGroup("Hosts Manager") {
            ContentView()
        }
    }
}
