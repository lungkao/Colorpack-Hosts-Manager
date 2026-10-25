// Hosts Manager (Windows) — หน้าต่างเพิ่มโดเมน และหน้าต่างเลือกนำเข้า / Add + Import dialogs

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HostsManager
{
    public class AddDialog : Form
    {
        readonly TextBox ip, domains, note;
        readonly CheckBox www;

        public string IpText => ip.Text.Trim();
        public List<string> DomainList => Util.SplitWords(domains.Text);
        public bool AddWww => www.Checked;
        public string NoteText => note.Text.Trim();

        public AddDialog(string lastIp)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = CP.Body;
            BackColor = CP.Paper;
            Text = L.T("ชี้โดเมนไป server ใหม่", "Point domains to a new server");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Location = new Point(14, 14) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));

            ip = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Width = 400, Text = lastIp ?? "", Font = CP.Mono };
            ip.TextChanged += (s, e) => ip.ForeColor = ip.Text.Length == 0 || Util.IsValidIP(ip.Text) ? SystemColors.WindowText : Color.Red;
            domains = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Width = 400, Multiline = true, Height = 80, ScrollBars = ScrollBars.Vertical, Font = CP.Mono };
            www = new CheckBox { Text = L.T("เพิ่ม www. ให้อัตโนมัติ", "Add www. automatically"), Checked = true, AutoSize = true };
            note = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Width = 400 };

            AddRow(t, L.T("IP server ใหม่", "New server IP"), ip);
            AddRow(t, L.T("โดเมน", "Domains"), domains);
            AddRow(t, "", new Label
            {
                Text = L.T("ใส่ได้หลายโดเมน คั่นด้วยเว้นวรรค จุลภาค หรือขึ้นบรรทัดใหม่",
                           "Several allowed — separate with spaces, commas or new lines"),
                AutoSize = true, ForeColor = CP.Gray, Font = CP.Small
            });
            AddRow(t, "", www);
            AddRow(t, L.T("หมายเหตุ", "Note"), note);
            AddRow(t, "", new Label
            {
                Text = L.T("ถ้าโดเมนมีอยู่แล้ว จะอัปเดต IP ให้แทนการเพิ่มซ้ำ",
                           "If a domain already exists, its IP is updated instead of adding a duplicate"),
                AutoSize = true, ForeColor = CP.BlueDeep, BackColor = CP.BlueTint, Font = CP.Small, Padding = new Padding(8, 4, 8, 4)
            });

            var ok = new RoundButton(RoundButton.Kinds.Primary) { Text = L.T("เพิ่ม", "Add"), MinimumSize = new Size(100, 0) };
            var cancel = new RoundButton { Text = L.T("ยกเลิก", "Cancel"), MinimumSize = new Size(100, 0), DialogResult = DialogResult.Cancel };
            ok.Click += (s, e) =>
            {
                if (!Util.IsValidIP(ip.Text))
                {
                    MessageBox.Show(this, L.T("กรุณาใส่ IP ให้ถูกต้อง", "Please enter a valid IP address"), Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ip.Focus();
                    return;
                }
                if (DomainList.Count == 0)
                {
                    MessageBox.Show(this, L.T("กรุณาใส่โดเมนอย่างน้อย 1 รายการ", "Please enter at least one domain"), Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    domains.Focus();
                    return;
                }
                DialogResult = DialogResult.OK;
            };
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            t.Controls.Add(buttons, 0, t.RowCount);
            t.SetColumnSpan(buttons, 2);
            t.RowCount++;

            Controls.Add(t);
            AcceptButton = ok;
            CancelButton = cancel;
            Shown += (s, e) => (ip.Text.Length == 0 ? ip : domains).Focus();
        }

        static void AddRow(TableLayoutPanel t, string label, Control c)
        {
            int r = t.RowCount;
            t.Controls.Add(new Label { Text = label, AutoSize = true, Font = CP.Label, ForeColor = CP.Gray, Margin = new Padding(0, 8, 12, 0) }, 0, r);
            c.Margin = new Padding(0, 4, 0, 4);
            t.Controls.Add(c, 1, r);
            t.RowCount++;
        }
    }

    public class ImportDialog : Form
    {
        public readonly List<ImportCandidate> Candidates;
        readonly TextBox search;
        readonly CheckBox showAll;
        readonly DataGridView list;
        readonly DataGridViewCheckBoxColumn colSel;
        readonly Label count, emptyNote;
        readonly Button ok;
        bool updating;

        public ImportDialog(List<ImportCandidate> candidates)
        {
            Candidates = candidates;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = CP.Body;
            BackColor = CP.Paper;
            Text = L.T("เลือกนำเข้ารายการเดิม", "Choose entries to import");
            ClientSize = new Size(900, 580);
            MinimumSize = new Size(640, 400);
            ShowInTaskbar = false;
            ShowIcon = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var head = new Label
            {
                Dock = DockStyle.Top, AutoSize = false, Height = 64, Padding = new Padding(0, 4, 0, 0), Font = CP.Small, ForeColor = CP.Gray,
                Text = L.T("เลือกบรรทัดจากไฟล์ hosts เดิมที่จะนำเข้ามาจัดการ\nบรรทัดที่ไม่ได้เลือกจะอยู่ในไฟล์เหมือนเดิม ไม่ถูกลบ · บรรทัดของระบบ (localhost) ไม่แสดงในนี้",
                           "Choose lines from the existing hosts file to manage here.\nUnselected lines stay in the file untouched · system lines (localhost) are not listed")
            };

            search = new TextBox { Width = 340, Margin = new Padding(0, 6, 8, 0) };
            SetCue(search, L.T("ค้นหา IP / โดเมน / หมายเหตุ", "Search IP / domain / note"));
            search.TextChanged += (s, e) => Rebuild();
            var selAll = new RoundButton(RoundButton.Kinds.Secondary, small: true) { Text = L.T("☑ เลือกทั้งหมด", "☑ Select all") };
            selAll.Click += (s, e) => SelectVisible(true);
            var selNone = new RoundButton(RoundButton.Kinds.Secondary, small: true) { Text = L.T("☐ ไม่เลือกเลย", "☐ Select none") };
            selNone.Click += (s, e) => SelectVisible(false);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
            tools.Controls.AddRange(new Control[] { search, selAll, selNone });

            int hidden = candidates.Count(c => !c.RealDomain);
            showAll = new CheckBox
            {
                Dock = DockStyle.Top, AutoSize = true, Visible = hidden > 0, Padding = new Padding(2, 4, 0, 4),
                Text = L.T($"แสดงทั้งหมด รวม {hidden} บรรทัดที่ไม่ใช่โดเมนจริง (เช่น .local, .internal, ชื่อเครื่อง)",
                           $"Show all, including {hidden} non-public names (.local, .internal, hostnames)")
            };
            showAll.CheckedChanged += (s, e) => Rebuild();

            list = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            CP.StyleGrid(list);
            list.BorderStyle = BorderStyle.FixedSingle;
            colSel = new DataGridViewCheckBoxColumn { HeaderText = "", Width = 36 };
            var mono = CP.Mono;
            var cIp = new DataGridViewTextBoxColumn { HeaderText = "IP", Width = 140, ReadOnly = true };
            cIp.DefaultCellStyle.Font = mono;
            var cHosts = new DataGridViewTextBoxColumn { HeaderText = L.T("โดเมน", "Domains"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true };
            cHosts.DefaultCellStyle.Font = mono;
            var cTag = new DataGridViewTextBoxColumn { HeaderText = "", Width = 140, ReadOnly = true };
            var cNote = new DataGridViewTextBoxColumn { HeaderText = L.T("หมายเหตุ", "Note"), Width = 150, ReadOnly = true };
            var cWhere = new DataGridViewTextBoxColumn { HeaderText = L.T("ตำแหน่ง", "Position"), Width = 95, ReadOnly = true };
            list.Columns.AddRange(colSel, cIp, cHosts, cTag, cNote, cWhere);
            foreach (DataGridViewColumn c in list.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            list.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (list.IsCurrentCellDirty) list.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            list.CellValueChanged += (s, e) =>
            {
                if (updating || e.RowIndex < 0 || e.ColumnIndex != colSel.Index) return;
                if (list.Rows[e.RowIndex].Tag is ImportCandidate c) c.Selected = list.Rows[e.RowIndex].Cells[colSel.Index].Value is bool b && b;
                UpdateCount();
            };
            list.CellClick += (s, e) =>
            {
                // คลิกที่ไหนในแถวก็ติ๊กได้ / click anywhere on a row to toggle
                if (e.RowIndex < 0 || e.ColumnIndex == colSel.Index) return;
                var cell = list.Rows[e.RowIndex].Cells[colSel.Index];
                cell.Value = !(cell.Value is bool b && b);
            };

            count = new Label { AutoSize = true, ForeColor = CP.Gray, Font = CP.Small, Margin = new Padding(0, 8, 0, 0) };
            emptyNote = new Label
            {
                AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                Font = CP.H3, ForeColor = CP.Ink, BackColor = CP.Paper, Visible = false
            };
            ok = new RoundButton(RoundButton.Kinds.Primary) { MinimumSize = new Size(120, 0) };
            ok.Click += (s, e) => { DialogResult = DialogResult.OK; };
            var cancel = new RoundButton { Text = L.T("ยกเลิก", "Cancel"), MinimumSize = new Size(100, 0), DialogResult = DialogResult.Cancel };
            var bottomRight = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            bottomRight.Controls.Add(ok);
            bottomRight.Controls.Add(cancel);
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(0, 8, 0, 0) };
            bottom.Controls.Add(count);
            bottom.Controls.Add(bottomRight);

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 10, 14, 10) };
            body.Controls.Add(emptyNote);
            body.Controls.Add(list);
            body.Controls.Add(showAll);
            body.Controls.Add(tools);
            body.Controls.Add(head);
            body.Controls.Add(bottom);
            Controls.Add(body);
            AcceptButton = ok;
            CancelButton = cancel;

            Rebuild();
            Shown += (s, e) => { list.ClearSelection(); search.Focus(); };
        }

        bool Matches(ImportCandidate c)
        {
            if (!showAll.Checked && !c.RealDomain) return false;
            var q = search.Text.Trim().ToLowerInvariant();
            if (q.Length == 0) return true;
            return c.Entry.Ip.ToLowerInvariant().Contains(q)
                || c.Entry.Hosts.ToLowerInvariant().Contains(q)
                || c.Entry.Note.ToLowerInvariant().Contains(q);
        }

        void Rebuild()
        {
            updating = true;
            list.Rows.Clear();
            foreach (var c in Candidates.Where(Matches))
            {
                string tag = c.AlreadyManaged ? L.T("⚠ มีในรายการแล้ว", "⚠ Already listed")
                           : !c.RealDomain ? L.T("ไม่ใช่โดเมนจริง", "not public") : "";
                int i = list.Rows.Add(c.Selected, c.Entry.Ip, c.Entry.Hosts, tag, c.Entry.Note,
                    c.FromPrefix ? L.T("ต้นไฟล์", "top") : L.T("ท้ายไฟล์", "bottom"));
                list.Rows[i].Tag = c;
                if (c.AlreadyManaged) list.Rows[i].Cells[3].Style.ForeColor = Color.FromArgb(217, 119, 6);
                else if (!c.RealDomain) list.Rows[i].Cells[3].Style.ForeColor = Color.Gray;
            }
            updating = false;
            list.ClearSelection();
            // ไม่มีอะไรให้นำเข้า → บอกตรงๆ / say clearly when there is nothing to import
            bool none = list.Rows.Count == 0;
            emptyNote.Visible = none;
            list.Visible = !none;
            int hidden = Candidates.Count(c => !c.RealDomain);
            emptyNote.Text = search.Text.Trim().Length > 0
                ? L.T("ไม่พบรายการที่ตรงกับคำค้น", "Nothing matches your search")
                : L.T("✓ ไม่พบโดเมนจริงในไฟล์ hosts เดิมที่ยังไม่ได้นำเข้า", "✓ No real domains left in the existing hosts file")
                  + (hidden > 0 && !showAll.Checked
                      ? "\n\n" + L.T($"ซ่อนชื่อภายใน {hidden} รายการ (เช่น ชื่อเครื่อง, .local) — ติ๊ก “แสดงทั้งหมด” ด้านบนเพื่อดู",
                                       $"{hidden} internal names hidden (hostnames, .local) — tick “Show all” above to see them")
                      : "");
            UpdateCount();
        }

        void SelectVisible(bool on)
        {
            foreach (var c in Candidates.Where(Matches)) c.Selected = on;
            Rebuild();
        }

        void UpdateCount()
        {
            int sel = Candidates.Count(c => c.Selected);
            count.Text = L.T($"เลือกแล้ว {sel} จาก {Candidates.Count} บรรทัด", $"{sel} of {Candidates.Count} selected");
            ok.Text = L.T($"นำเข้า {sel} รายการ", $"Import {sel}");
            ok.Enabled = sel > 0;
        }

        // ข้อความจางในช่องค้นหา / placeholder text
        const int EM_SETCUEBANNER = 0x1501;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        static void SetCue(TextBox box, string text)
        {
            box.HandleCreated += (s, e) => SendMessage(box.Handle, EM_SETCUEBANNER, (IntPtr)1, text);
        }
    }
}
