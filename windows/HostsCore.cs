// Hosts Manager (Windows) — ตรรกะหลัก: อ่าน/เขียนไฟล์ hosts, ตรวจซ้ำ, นำเข้า, DNS
// Core logic shared by the UI and the self-test. No Windows-only APIs here.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HostsManager
{
    public enum Lang { Th, En }

    /// <summary>เลือกข้อความตามภาษา / pick text by current language</summary>
    public static class L
    {
        public static Lang Lang = Lang.Th;
        public static string T(string th, string en) => Lang == Lang.Th ? th : en;
    }

    public static class Util
    {
        public static bool IsValidIP(string s)
        {
            var t = (s ?? "").Trim();
            if (t.Length == 0) return false;
            if (t.Contains(":"))
                return IPAddress.TryParse(t, out var a6) && a6.AddressFamily == AddressFamily.InterNetworkV6;
            var parts = t.Split('.');
            if (parts.Length != 4) return false;
            foreach (var p in parts)
            {
                if (p.Length == 0 || p.Length > 3 || !p.All(c => c >= '0' && c <= '9')) return false;
                if (int.Parse(p) > 255) return false;
            }
            return true;
        }

        public static string CleanDomain(string s)
        {
            var d = (s ?? "").Trim().ToLowerInvariant();
            foreach (var p in new[] { "https://", "http://" })
                if (d.StartsWith(p)) d = d.Substring(p.Length);
            int i = d.IndexOfAny(new[] { '/', ':' });
            if (i >= 0) d = d.Substring(0, i);
            return d;
        }

        static readonly HashSet<string> InternalSuffixes = new HashSet<string>
        {
            "local", "localhost", "localdomain", "internal", "lan", "home", "test",
            "invalid", "example", "arpa", "intranet", "corp", "private"
        };

        /// <summary>โดเมนจริง (สาธารณะ) / a real public domain name</summary>
        public static bool IsRealDomain(string host)
        {
            var h = (host ?? "").ToLowerInvariant().Trim('.');
            if (h.Length == 0 || h.Length > 253) return false;
            var labels = h.Split('.');
            if (labels.Length < 2) return false;
            foreach (var l in labels)
            {
                if (l.Length == 0 || l.Length > 63 || l[0] == '-' || l[l.Length - 1] == '-') return false;
                bool ascii = l.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-');
                if (!ascii && !l.StartsWith("xn--")) return false;
            }
            var tld = labels[labels.Length - 1];
            if (tld.Length < 2) return false;
            if (!tld.All(c => c >= 'a' && c <= 'z') && !tld.StartsWith("xn--")) return false;
            return !InternalSuffixes.Contains(tld);
        }

        public static List<string> SplitWords(string s) =>
            (s ?? "").Split(new[] { ' ', '\t', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public class HostEntry
    {
        public const string OffPrefix = "#off ";

        public Guid Id = Guid.NewGuid();
        public bool Enabled = true;
        public string Ip = "";
        public string Hosts = "";
        public string Note = "";

        public bool IsValid => Ip.Trim().Length > 0 && Hosts.Trim().Length > 0;

        public List<string> HostList =>
            Hosts.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        public string Line()
        {
            var seen = new HashSet<string>();
            var hs = HostList.Where(h => seen.Add(h.ToLowerInvariant()));
            var s = Ip.Trim() + "\t" + string.Join(" ", hs);
            if (Note.Trim().Length > 0) s += "\t# " + Note.Trim();
            return Enabled ? s : OffPrefix + s;
        }

        public static HostEntry Parse(string raw)
        {
            var line = (raw ?? "").Trim();
            bool enabled = true;
            if (line.StartsWith(OffPrefix))
            {
                enabled = false;
                line = line.Substring(OffPrefix.Length);
            }
            else if (line.StartsWith("#"))
            {
                return null;
            }
            string note = "";
            int hash = line.IndexOf('#');
            if (hash >= 0)
            {
                note = line.Substring(hash + 1).Trim();
                line = line.Substring(0, hash);
            }
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return null;
            return new HostEntry
            {
                Enabled = enabled,
                Ip = parts[0],
                Hosts = string.Join(" ", parts.Skip(1)),
                Note = note
            };
        }
    }

    public class Issue
    {
        public string Text;
        public bool Severe;
    }

    public class ImportCandidate
    {
        public HostEntry Entry;
        public bool FromPrefix;
        public int LineIndex;
        public bool Selected;
        public bool AlreadyManaged;
        public bool RealDomain;
    }

    /// <summary>ไฟล์ hosts ที่แยกเป็น 3 ส่วน: ก่อนบล็อก, รายการของแอป, หลังบล็อก</summary>
    public class HostsDoc
    {
        public const string Begin = "# >>> HostsManager >>>";
        public const string End = "# <<< HostsManager <<<";

        public static readonly HashSet<string> Reserved = new HashSet<string>
        {
            "localhost", "broadcasthost", "ip6-localhost", "ip6-loopback"
        };

        public List<string> Prefix = new List<string>();
        public List<string> Suffix = new List<string>();
        public List<HostEntry> Entries = new List<HostEntry>();

        public void Parse(string text)
        {
            var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            if (lines.Count > 0 && lines[lines.Count - 1] == "") lines.RemoveAt(lines.Count - 1);
            var pre = new List<string>();
            var mid = new List<string>();
            var post = new List<string>();
            int state = 0;
            foreach (var l in lines)
            {
                var t = l.Trim();
                if (state == 0 && t == Begin) { state = 1; continue; }
                if (state == 1 && t == End) { state = 2; continue; }
                (state == 0 ? pre : state == 1 ? mid : post).Add(l);
            }
            Prefix = pre;
            Suffix = post;
            Entries = mid.Select(HostEntry.Parse).Where(e => e != null).ToList();
        }

        /// <summary>ข้อความทั้งไฟล์ (ขึ้นบรรทัดด้วย \n)</summary>
        public string Compose()
        {
            var o = new List<string>(Prefix);
            while (o.Count > 0 && o[o.Count - 1].Trim().Length == 0) o.RemoveAt(o.Count - 1);
            var valid = Entries.Where(e => e.IsValid).ToList();
            if (valid.Count > 0)
            {
                o.Add("");
                o.Add(Begin);
                o.AddRange(valid.Select(e => e.Line()));
                o.Add(End);
            }
            o.AddRange(Suffix);
            return string.Join("\n", o) + "\n";
        }

        // ---------- เพิ่ม / Add ----------

        public (int added, int updated) Add(string ip, IEnumerable<string> domains, bool addWww, string note)
        {
            int added = 0, updated = 0;
            ip = ip.Trim();
            note = (note ?? "").Trim();
            foreach (var raw in domains)
            {
                var d = Util.CleanDomain(raw);
                if (d.Length == 0) continue;
                var bas = d.StartsWith("www.") ? d.Substring(4) : d;
                var hosts = addWww ? bas + " www." + bas : d;
                var existing = Entries.FirstOrDefault(e => e.HostList.Contains(bas) || e.HostList.Contains(d));
                if (existing != null)
                {
                    existing.Ip = ip;
                    existing.Enabled = true;
                    if (note.Length > 0) existing.Note = note;
                    updated++;
                }
                else
                {
                    Entries.Add(new HostEntry { Ip = ip, Hosts = hosts, Note = note });
                    added++;
                }
            }
            return (added, updated);
        }

        // ---------- ตรวจซ้ำ / Duplicate checks ----------

        /// <summary>host -> (ip, อยู่ก่อนบล็อกหรือไม่)</summary>
        public Dictionary<string, (string ip, bool before)> SystemHosts()
        {
            var d = new Dictionary<string, (string, bool)>();
            foreach (var (lines, before) in new[] { (Prefix, true), (Suffix, false) })
            {
                foreach (var l in lines)
                {
                    var e = HostEntry.Parse(l);
                    if (e == null || !e.Enabled) continue;
                    foreach (var h in e.HostList.Select(x => x.ToLowerInvariant()))
                        if (!d.ContainsKey(h)) d[h] = (e.Ip, before);
                }
            }
            return d;
        }

        public Dictionary<Guid, List<Issue>> Issues()
        {
            var sys = SystemHosts();
            var firstOwner = new Dictionary<string, HostEntry>();
            var o = new Dictionary<Guid, List<Issue>>();
            void Add(Guid id, string text, bool severe)
            {
                if (!o.TryGetValue(id, out var list)) o[id] = list = new List<Issue>();
                list.Add(new Issue { Text = text, Severe = severe });
            }
            foreach (var e in Entries.Where(x => x.IsValid))
            {
                var ip = e.Ip.Trim();
                var seenInRow = new HashSet<string>();
                foreach (var h in e.HostList.Select(x => x.ToLowerInvariant()))
                {
                    if (!seenInRow.Add(h))
                    {
                        Add(e.Id, L.T($"{h} ใส่ซ้ำในแถวเดียวกัน (จะบันทึกแค่ครั้งเดียว)",
                                      $"{h} is listed twice in this row (saved once)"), false);
                        continue;
                    }
                    if (sys.TryGetValue(h, out var s))
                    {
                        if (s.before)
                            Add(e.Id, L.T($"{h} มีอยู่แล้วในส่วนอื่นของไฟล์ ({s.ip}) ซึ่งจะถูกใช้ก่อน — ใช้ “เลือกนำเข้ารายการเดิม” เพื่อรวมมาจัดการ",
                                          $"{h} already exists elsewhere in the file ({s.ip}) and takes priority — use “Choose entries to import” to manage it here"),
                                e.Enabled && s.ip != ip);
                        else
                            Add(e.Id, L.T($"{h} มีในส่วนท้ายของไฟล์ด้วย ({s.ip}) แต่แถวนี้จะถูกใช้ก่อน",
                                          $"{h} also appears later in the file ({s.ip}), but this row wins"), false);
                    }
                    if (firstOwner.TryGetValue(h, out var other))
                    {
                        bool same = other.Ip.Trim() == ip;
                        bool bothOn = other.Enabled && e.Enabled;
                        string text;
                        if (bothOn && !same)
                            text = L.T($"{h} ซ้ำกับแถวด้านบนที่ชี้ไป {other.Ip.Trim()} — ระบบจะใช้แถวด้านบน แถวนี้ไม่มีผล",
                                       $"{h} duplicates a row above pointing to {other.Ip.Trim()} — the row above wins, this one has no effect");
                        else if (same)
                            text = L.T($"{h} ซ้ำกับแถวด้านบน (IP เดียวกัน) — ลบแถวใดแถวหนึ่งได้",
                                       $"{h} duplicates a row above (same IP) — you can delete one");
                        else
                            text = L.T($"{h} ซ้ำกับอีกแถวที่ชี้ไป {other.Ip.Trim()} (มีแถวหนึ่งปิดอยู่)",
                                       $"{h} duplicates another row pointing to {other.Ip.Trim()} (one of them is off)");
                        Add(e.Id, text, bothOn && !same);
                        Add(other.Id, L.T($"{h} มีซ้ำในแถวด้านล่าง", $"{h} is duplicated in a row below"), false);
                    }
                    else
                    {
                        firstOwner[h] = e;
                    }
                }
            }
            return o;
        }

        public int SevereCount() => Issues().Values.SelectMany(x => x).Count(x => x.Severe);

        // ---------- นำเข้า / Import ----------

        static HostEntry Importable(string line)
        {
            var e = HostEntry.Parse(line);
            if (e == null || !e.Enabled) return null;
            var hs = new HashSet<string>(e.HostList.Select(h => h.ToLowerInvariant()));
            return hs.Overlaps(Reserved) ? null : e;
        }

        /// <summary>จำนวนบรรทัดเดิมที่เป็นโดเมนจริง / existing lines with a real domain</summary>
        public int ImportableCount() =>
            Prefix.Concat(Suffix).Count(l =>
            {
                var e = Importable(l);
                return e != null && e.HostList.Any(Util.IsRealDomain);
            });

        public List<ImportCandidate> ImportCandidates()
        {
            var managed = new HashSet<string>(Entries.SelectMany(e => e.HostList).Select(h => h.ToLowerInvariant()));
            var list = new List<ImportCandidate>();
            foreach (var (lines, isPrefix) in new[] { (Prefix, true), (Suffix, false) })
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var e = Importable(lines[i]);
                    if (e == null) continue;
                    list.Add(new ImportCandidate
                    {
                        Entry = e,
                        FromPrefix = isPrefix,
                        LineIndex = i,
                        AlreadyManaged = e.HostList.Any(h => managed.Contains(h.ToLowerInvariant())),
                        RealDomain = e.HostList.Any(Util.IsRealDomain)
                    });
                }
            }
            return list;
        }

        /// <summary>ย้ายบรรทัดที่เลือกเข้าบล็อกของแอป โดยรักษาลำดับความสำคัญเดิม</summary>
        public int ImportSelected(IEnumerable<ImportCandidate> candidates)
        {
            var chosen = candidates.Where(c => c.Selected).ToList();
            if (chosen.Count == 0) return 0;
            HostEntry Mark(HostEntry e)
            {
                if (e.Note.Length == 0) e.Note = L.T("นำเข้าจากไฟล์เดิม", "imported");
                return e;
            }
            var pre = chosen.Where(c => c.FromPrefix).ToList();
            var post = chosen.Where(c => !c.FromPrefix).ToList();
            var preIdx = new HashSet<int>(pre.Select(c => c.LineIndex));
            var postIdx = new HashSet<int>(post.Select(c => c.LineIndex));
            Prefix = Prefix.Where((_, i) => !preIdx.Contains(i)).ToList();
            Suffix = Suffix.Where((_, i) => !postIdx.Contains(i)).ToList();
            Entries = pre.Select(c => Mark(c.Entry))
                .Concat(Entries)
                .Concat(post.Select(c => Mark(c.Entry)))
                .ToList();
            return chosen.Count;
        }
    }

    /// <summary>ถาม DNS สาธารณะโดยตรง (UDP) / minimal DNS A-record query</summary>
    public static class PublicDns
    {
        public static List<string> QueryA(string name, string server = "1.1.1.1", int timeoutMs = 3000)
        {
            var id = (ushort)new Random().Next(1, 65535);
            var q = BuildQuery(name, id);
            using (var udp = new UdpClient())
            {
                udp.Client.ReceiveTimeout = timeoutMs;
                udp.Connect(server, 53);
                udp.Send(q, q.Length);
                IPEndPoint ep = null;
                var r = udp.Receive(ref ep);
                return ParseA(r, id);
            }
        }

        public static byte[] BuildQuery(string name, ushort id)
        {
            var q = new List<byte> { (byte)(id >> 8), (byte)id, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0 };
            foreach (var label in name.Trim('.').Split('.'))
            {
                var b = Encoding.ASCII.GetBytes(label);
                q.Add((byte)b.Length);
                q.AddRange(b);
            }
            q.Add(0);
            q.AddRange(new byte[] { 0, 1, 0, 1 });
            return q.ToArray();
        }

        public static List<string> ParseA(byte[] r, int id)
        {
            var res = new List<string>();
            if (r == null || r.Length < 12 || ((r[0] << 8) | r[1]) != id) return res;
            int qd = (r[4] << 8) | r[5];
            int an = (r[6] << 8) | r[7];
            int p = 12;
            for (int i = 0; i < qd; i++) p = SkipName(r, p) + 4;
            for (int i = 0; i < an && p < r.Length; i++)
            {
                p = SkipName(r, p);
                if (p + 10 > r.Length) break;
                int type = (r[p] << 8) | r[p + 1];
                int rdlen = (r[p + 8] << 8) | r[p + 9];
                p += 10;
                if (type == 1 && rdlen == 4 && p + 4 <= r.Length)
                    res.Add($"{r[p]}.{r[p + 1]}.{r[p + 2]}.{r[p + 3]}");
                p += rdlen;
            }
            return res;
        }

        static int SkipName(byte[] r, int p)
        {
            while (p < r.Length)
            {
                int len = r[p];
                if (len == 0) return p + 1;
                if ((len & 0xC0) == 0xC0) return p + 2;
                p += len + 1;
            }
            return p;
        }
    }

    /// <summary>ทดสอบตรรกะหลักโดยไม่ต้องเปิดหน้าจอ / headless tests (HostsManager.exe --selftest)</summary>
    public static class SelfTest
    {
        public static int Run(out string report)
        {
            var log = new StringBuilder();
            int fail = 0;
            void Check(bool ok, string name)
            {
                log.AppendLine((ok ? "PASS " : "FAIL ") + name);
                if (!ok) fail++;
            }

            var saved = L.Lang;
            L.Lang = Lang.En;
            try
            {
                Check(Util.IsValidIP("43.241.57.141"), "ipv4 valid");
                Check(!Util.IsValidIP("1"), "ipv4 short rejected");
                Check(!Util.IsValidIP("256.1.1.1"), "ipv4 range");
                Check(Util.IsValidIP("::1"), "ipv6 valid");
                Check(!Util.IsValidIP("๑.๒.๓.๔"), "thai digits rejected");

                Check(Util.IsRealDomain("colorpack.net"), "real domain");
                Check(Util.IsRealDomain("www.ghgreduction.tgo.or.th"), "real domain .or.th");
                Check(!Util.IsRealDomain("kubernetes.docker.internal"), ".internal rejected");
                Check(!Util.IsRealDomain("mymac"), "hostname rejected");
                Check(!Util.IsRealDomain("1.2.3.4"), "ip rejected");
                Check(Util.CleanDomain(" https://Shop.Example.co.th/path ") == "shop.example.co.th", "clean domain");

                var sample = string.Join("\r\n", new[]
                {
                    "# Copyright (c) 1993-2009 Microsoft Corp.",
                    "#\t127.0.0.1       localhost",
                    "127.0.0.1 localhost",
                    "10.0.0.5 shop.co.th www.shop.co.th",
                    "10.0.0.9 build.internal",
                    "",
                    HostsDoc.Begin,
                    "43.241.57.141\tcolorpack.net www.colorpack.net\t# client A",
                    "#off 43.241.57.142\toff.co.th",
                    HostsDoc.End,
                    "203.0.113.7 late.com"
                }) + "\r\n";

                var doc = new HostsDoc();
                doc.Parse(sample);
                Check(doc.Entries.Count == 2, "parse block entries");
                Check(!doc.Entries[1].Enabled, "parse #off");
                Check(doc.Entries[0].Note == "client A", "parse note");
                Check(doc.Prefix.Count == 6 && doc.Suffix.Count == 1, "prefix/suffix split");
                var c1 = doc.Compose();
                var doc2 = new HostsDoc();
                doc2.Parse(c1);
                Check(doc2.Compose() == c1, "compose round-trip stable");
                Check(c1.Contains("127.0.0.1 localhost"), "system lines kept");

                var r = doc.Add("1.2.3.4", new[] { "https://new-site.com/x", "colorpack.net" }, true, "");
                Check(r.added == 1 && r.updated == 1, "add + update");
                Check(doc.Entries.Last().Hosts == "new-site.com www.new-site.com", "auto www");
                Check(doc.Entries[0].Ip == "1.2.3.4", "update ip");

                doc.Entries.Add(new HostEntry { Ip = "5.5.5.5", Hosts = "new-site.com" });
                var iss = doc.Issues();
                Check(doc.SevereCount() >= 1, "duplicate different ip is severe");
                Check(iss.ContainsKey(doc.Entries.Last().Id), "issue on second row");

                var row = new HostEntry { Ip = "1.1.1.1", Hosts = "a.com a.com b.com" };
                Check(row.Line() == "1.1.1.1\ta.com b.com", "dedupe within row on write");

                Check(doc.ImportableCount() == 2, "importable count (real domains only)");
                var cands = doc.ImportCandidates();
                Check(cands.Count == 3, "import candidates incl. non-domain");
                Check(cands.Count(x => !x.RealDomain) == 1, "non-domain flagged");
                foreach (var c in cands.Where(x => x.Entry.Hosts.StartsWith("shop"))) c.Selected = true;
                var before = doc.Entries.Count;
                Check(doc.ImportSelected(cands) == 1, "import selected");
                Check(doc.Entries.Count == before + 1 && doc.Entries[0].Hosts.StartsWith("shop"), "imported first (keeps priority)");
                Check(!doc.Prefix.Any(l => l.Contains("shop.co.th")), "imported line removed from prefix");
                Check(doc.Prefix.Any(l => l.Contains("build.internal")), "unselected line kept");

                var q = PublicDns.BuildQuery("ab.com", 0x1234);
                var resp = new List<byte>(q);
                resp[2] = 0x81; resp[3] = 0x80; resp[7] = 1;
                resp.AddRange(new byte[] { 0xC0, 0x0C, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 93, 184, 216, 34 });
                var ips = PublicDns.ParseA(resp.ToArray(), 0x1234);
                Check(ips.Count == 1 && ips[0] == "93.184.216.34", "dns answer parse");
            }
            catch (Exception ex)
            {
                log.AppendLine("EXCEPTION " + ex);
                fail++;
            }
            finally
            {
                L.Lang = saved;
            }
            log.AppendLine(fail == 0 ? "ALL PASSED" : fail + " FAILED");
            report = log.ToString();
            return fail == 0 ? 0 : 1;
        }
    }
}
