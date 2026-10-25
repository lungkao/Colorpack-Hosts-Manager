// Hosts Manager (Windows) — จุดเริ่มโปรแกรม / entry point
//   HostsManager.exe                      เปิดแอปปกติ / normal start
//   HostsManager.exe --hosts <path>       ใช้ไฟล์อื่นแทน hosts ของระบบ / use another file (testing)
//   HostsManager.exe --selftest <report>  ทดสอบตรรกะหลัก / headless logic tests
//   HostsManager.exe --smoke <log>        เปิดทุกหน้าจออัตโนมัติ / UI smoke test

using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HostsManager
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            string hostsPath = Path.Combine(
                Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows",
                @"System32\drivers\etc\hosts");
            string ArgAfter(string flag)
            {
                int i = Array.IndexOf(args, flag);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
            hostsPath = ArgAfter("--hosts") ?? hostsPath;

            if (args.Contains("--selftest"))
            {
                int code = SelfTest.Run(out var report);
                File.WriteAllText(ArgAfter("--selftest") ?? Path.Combine(Path.GetTempPath(), "hostsmanager-selftest.txt"), report);
                return code;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            L.Lang = Settings.LoadLang();

            if (args.Contains("--smoke"))
            {
                var log = ArgAfter("--smoke") ?? Path.Combine(Path.GetTempPath(), "hostsmanager-smoke.txt");
                try
                {
                    using (var f = new MainForm(hostsPath)) f.RunSmokeTest();
                    File.WriteAllText(log, "SMOKE OK");
                    return 0;
                }
                catch (Exception ex)
                {
                    File.WriteAllText(log, "SMOKE FAILED\r\n" + ex);
                    return 1;
                }
            }

            Application.ThreadException += (s, e) =>
                MessageBox.Show(e.Exception.Message, "Hosts Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Run(new MainForm(hostsPath));
            return 0;
        }
    }

    /// <summary>จำภาษาที่เลือก / remembers the chosen language</summary>
    static class Settings
    {
        static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HostsManager", "lang.txt");

        public static Lang LoadLang()
        {
            try { return File.ReadAllText(FilePath).Trim() == "en" ? Lang.En : Lang.Th; }
            catch { return Lang.Th; }
        }

        public static void SaveLang(Lang lang)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, lang == Lang.En ? "en" : "th");
            }
            catch { }
        }
    }
}
