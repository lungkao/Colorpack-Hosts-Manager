// Hosts Manager (Windows) — หน้าต่างหลัก (ธีม Colorpack) / main window (Colorpack theme)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HostsManager
{
    public class MainForm : Form
    {
        readonly string hostsPath;
        HostsDoc doc = new HostsDoc();
        string savedSnapshot = "";
        string lastIP = "";
        bool updating;

        static readonly Color Orange = CP.OrangeDeep;
        static readonly Color Muted = CP.Gray;
        static readonly Font Mono = CP.Mono;

        // header + toolbar
        HeaderBand header;
        RoundButton pillList, pillRaw, btnAdd, btnImport, btnAll, btnReload, btnFlush, btnSave;
        ContextMenuStrip allMenu;
        ToolStripMenuItem miAllOn, miAllOff;

        // tabs
        TabControl tabs;
        TabPage tabList, tabRaw;

        // list tab
        Panel banner;
        Label bannerLabel;
        RoundButton bannerImport, bannerSave;
        HashSet<string> savedLines = new HashSet<string>();

        /// <summary>แถวนี้เพิ่มใหม่หรือแก้ไขแล้วยังไม่บันทึก / row is new or changed since the last save</summary>
        bool IsUnsaved(HostEntry e) => e.IsValid && !savedLines.Contains(e.Line());

        /// <summary>แถวที่ลบไปแล้วแต่ยังไม่บันทึก / rows removed but not saved yet</summary>
        int UnsavedRemovedCount()
        {
            var current = new HashSet<string>(doc.Entries.SelectMany(x => x.HostList).Select(h => h.ToLowerInvariant()));
            return savedLines.Count(l =>
            {
                var e = HostEntry.Parse(l);
                return e != null && !e.HostList.Any(h => current.Contains(h.ToLowerInvariant()));
            });
        }
        DataGridView grid;
        DataGridViewCheckBoxColumn colOn;
        DataGridViewTextBoxColumn colIp, colHosts, colNote, colIssues;
        DataGridViewButtonColumn colCheck, colOpen, colDel;
        Panel empty;
        Label emptyLabel;
        RoundButton emptyAdd, emptyImport;

        // raw tab
        Label rawHint;
        TextBox raw;
        RoundButton rawReset, rawApply;

        // check panel + status
        Panel checkWrap;
        Card checkCard;
        FlowLayoutPanel checkFlow;
        Label checkLabel;
        TextBox checkInput;
        RoundButton checkBtn;
        Label checkResult;
        Label statusLabel, dirtyLabel;

        public MainForm(string hostsPath)
        {
            this.hostsPath = hostsPath;
            BuildUi();
            ApplyTexts();
            LoadHosts();
        }

        // ================= UI =================

        void BuildUi()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = CP.Body;
            BackColor = CP.Ground;
            Text = "Hosts Manager — Colorpack";
            ClientSize = new Size(1240, 780);
            MinimumSize = new Size(960, 600);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.S) { e.SuppressKeyPress = true; if (btnSave.Enabled) Save(); }
                if (e.Control && e.KeyCode == Keys.N) { e.SuppressKeyPress = true; ShowAdd(); }
            };

            // ---- header band ----
            header = new HeaderBand();
            header.Th.Click += (s, e) => SetLang(Lang.Th);
            header.En.Click += (s, e) => SetLang(Lang.En);

            // ---- toolbar ----
            pillList = new RoundButton(RoundButton.Kinds.PillOn) { Margin = new Padding(0, 4, 2, 0) };
            pillRaw = new RoundButton(RoundButton.Kinds.Pill) { Margin = new Padding(2, 4, 0, 0) };
            pillList.Click += (s, e) => tabs.SelectedTab = tabList;
            pillRaw.Click += (s, e) => tabs.SelectedTab = tabRaw;
            var leftBar = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, BackColor = CP.Ground };
            leftBar.Controls.AddRange(new Control[] { pillList, pillRaw });

            btnAdd = new RoundButton();
            btnAdd.Click += (s, e) => ShowAdd();
            btnImport = new RoundButton();
            btnImport.Click += (s, e) => ShowImport();
            miAllOn = new ToolStripMenuItem("", null, (s, e) => SetAll(true));
            miAllOff = new ToolStripMenuItem("", null, (s, e) => SetAll(false));
            allMenu = new ContextMenuStrip { Font = CP.Body };
            allMenu.Items.AddRange(new ToolStripItem[] { miAllOn, miAllOff });
            btnAll = new RoundButton();
            btnAll.Click += (s, e) => allMenu.Show(btnAll, new Point(0, btnAll.Height));
            btnReload = new RoundButton();
            btnReload.Click += (s, e) =>
            {
                if (IsDirty && !Confirm(L.T("ทิ้งการแก้ไขที่ยังไม่บันทึก แล้วโหลดไฟล์ใหม่?", "Discard unsaved changes and reload the file?"))) return;
                LoadHosts();
            };
            btnFlush = new RoundButton();
            btnFlush.Click += (s, e) => Flush();
            btnSave = new RoundButton(RoundButton.Kinds.Primary);
            btnSave.Click += (s, e) => Save();
            var rightBar = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = CP.Ground };
            rightBar.Controls.AddRange(new Control[] { btnAdd, btnImport, btnAll, btnReload, btnFlush, btnSave });
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 66, Padding = new Padding(16, 12, 16, 4), BackColor = CP.Ground };
            toolbar.Controls.Add(rightBar);
            toolbar.Controls.Add(leftBar);

            // ---- list tab ----
            bannerLabel = new Label { AutoSize = true, ForeColor = CP.OrangeDeep, Margin = new Padding(0, 8, 14, 0) };
            bannerImport = new RoundButton(RoundButton.Kinds.Secondary, small: true) { Margin = new Padding(0, 2, 0, 0) };
            bannerImport.Click += (s, e) => ShowImport();
            var bannerFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = CP.OrangeTint };
            bannerFlow.Controls.Add(bannerLabel);
            bannerFlow.Controls.Add(bannerImport);
            bannerSave = new RoundButton(RoundButton.Kinds.Primary, small: true) { Margin = new Padding(8, 2, 0, 0) };
            bannerSave.Click += (s, e) => Save();
            bannerFlow.Controls.Add(bannerSave);
            banner = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(14, 6, 14, 4), BackColor = CP.OrangeTint, Visible = false };
            banner.Controls.Add(bannerFlow);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            CP.StyleGrid(grid);
            colOn = new DataGridViewCheckBoxColumn { Width = 70, SortMode = DataGridViewColumnSortMode.NotSortable };
            colIp = new DataGridViewTextBoxColumn { Width = 150, SortMode = DataGridViewColumnSortMode.NotSortable };
            colIp.DefaultCellStyle.Font = Mono;
            colHosts = new DataGridViewTextBoxColumn { AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45, SortMode = DataGridViewColumnSortMode.NotSortable };
            colHosts.DefaultCellStyle.Font = Mono;
            colHosts.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            colNote = new DataGridViewTextBoxColumn { Width = 150, SortMode = DataGridViewColumnSortMode.NotSortable };
            colIssues = new DataGridViewTextBoxColumn { AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 40, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable };
            colIssues.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            colIssues.DefaultCellStyle.Font = CP.Small;
            colCheck = new DataGridViewButtonColumn { Width = 82, UseColumnTextForButtonValue = true };
            colOpen = new DataGridViewButtonColumn { Width = 92, UseColumnTextForButtonValue = true };
            colDel = new DataGridViewButtonColumn { Width = 70, UseColumnTextForButtonValue = true };
            grid.Columns.AddRange(colOn, colIp, colHosts, colNote, colIssues, colCheck, colOpen, colDel);
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += GridCellValueChanged;
            grid.CellContentClick += GridCellContentClick;
            grid.CellPainting += GridCellPainting;
            grid.CellMouseClick += (s, e) =>
            {
                // คลิกที่สวิตช์ก็เปิด/ปิดได้เลย / toggle on a single click
                if (e.RowIndex >= 0 && e.ColumnIndex == colOn.Index && grid.Rows[e.RowIndex].Tag is HostEntry en)
                {
                    en.Enabled = !en.Enabled;
                    updating = true;
                    grid.Rows[e.RowIndex].Cells[colOn.Index].Value = en.Enabled;
                    updating = false;
                    UpdateState();
                }
            };
            grid.DataError += (s, e) => { e.ThrowException = false; };
            colOn.ReadOnly = true;

            emptyLabel = new Label { AutoSize = true, Font = CP.H3, ForeColor = CP.Ink };
            emptyAdd = new RoundButton(RoundButton.Kinds.Primary);
            emptyAdd.Click += (s, e) => ShowAdd();
            emptyImport = new RoundButton();
            emptyImport.Click += (s, e) => ShowImport();
            var emptyFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false,
                Anchor = AnchorStyles.None, BackColor = CP.Paper
            };
            emptyFlow.Controls.AddRange(new Control[] { emptyLabel, emptyAdd, emptyImport });
            foreach (Control c in emptyFlow.Controls) c.Margin = new Padding(0, 6, 0, 6);
            var emptyTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, BackColor = CP.Paper };
            emptyTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            emptyTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            emptyTable.Controls.Add(emptyFlow, 0, 0);
            empty = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = CP.Paper };
            empty.Controls.Add(emptyTable);

            tabList = new TabPage { Padding = new Padding(0), BackColor = CP.Paper };
            tabList.Controls.Add(grid);
            tabList.Controls.Add(empty);
            tabList.Controls.Add(banner);

            // ---- raw tab ----
            rawHint = new Label { Dock = DockStyle.Top, Height = 32, ForeColor = CP.Gray, Font = CP.Small, Padding = new Padding(2, 6, 2, 0) };
            raw = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false,
                Font = Mono, AcceptsTab = true, BackColor = CP.Ground, BorderStyle = BorderStyle.None
            };
            rawReset = new RoundButton();
            rawReset.Click += (s, e) => raw.Text = ToCrlf(doc.Compose());
            rawApply = new RoundButton(RoundButton.Kinds.Primary);
            rawApply.Click += (s, e) =>
            {
                doc.Parse(raw.Text);
                RebuildGrid();
                raw.Text = ToCrlf(doc.Compose());
                SetStatus(() => L.T("นำข้อความไปใช้แล้ว — กดบันทึกเพื่อเขียนลงไฟล์", "Text applied — click Save to write the file"));
            };
            var rawButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 6, 0, 0), BackColor = CP.Paper };
            rawButtons.Controls.Add(rawApply);
            rawButtons.Controls.Add(rawReset);
            tabRaw = new TabPage { Padding = new Padding(10), BackColor = CP.Paper };
            tabRaw.Controls.Add(raw);
            tabRaw.Controls.Add(rawButtons);
            tabRaw.Controls.Add(rawHint);

            // ซ่อนหัวแท็บ ใช้ปุ่มเม็ดยาด้านบนแทน / hide tab headers; the pills switch tabs
            tabs = new TabControl
            {
                Dock = DockStyle.Fill, Appearance = TabAppearance.FlatButtons,
                ItemSize = new Size(0, 1), SizeMode = TabSizeMode.Fixed
            };
            tabs.TabPages.Add(tabList);
            tabs.TabPages.Add(tabRaw);
            tabs.SelectedIndexChanged += (s, e) =>
            {
                if (tabs.SelectedTab == tabRaw) raw.Text = ToCrlf(doc.Compose());
                pillList.Kind = tabs.SelectedTab == tabList ? RoundButton.Kinds.PillOn : RoundButton.Kinds.Pill;
                pillRaw.Kind = tabs.SelectedTab == tabRaw ? RoundButton.Kinds.PillOn : RoundButton.Kinds.Pill;
            };
            var bodyCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(8) };
            bodyCard.Controls.Add(tabs);
            var bodyWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 8), BackColor = CP.Ground };
            bodyWrap.Controls.Add(bodyCard);

            // ---- check panel ----
            checkLabel = new Label { AutoSize = true, Font = CP.Label, ForeColor = CP.Gray, Margin = new Padding(0, 10, 8, 0) };
            checkInput = new TextBox { Width = 320, Font = Mono, Margin = new Padding(0, 7, 8, 0) };
            checkInput.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Check(checkInput.Text); } };
            checkBtn = new RoundButton(RoundButton.Kinds.Secondary, small: true) { Margin = new Padding(0, 4, 0, 0) };
            checkBtn.Click += (s, e) => Check(checkInput.Text);
            var checkRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), BackColor = CP.Paper };
            checkRow.Controls.AddRange(new Control[] { checkLabel, checkInput, checkBtn });
            checkResult = new Label
            {
                AutoSize = true, Font = CP.Small, ForeColor = CP.Ink, BackColor = CP.BlueTint,
                Padding = new Padding(10, 6, 10, 6), Margin = new Padding(0, 8, 0, 0), Visible = false
            };
            checkFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false,
                Location = new Point(16, 8), BackColor = CP.Paper
            };
            checkFlow.Controls.Add(checkRow);
            checkFlow.Controls.Add(checkResult);
            checkCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(0) };
            checkCard.Controls.Add(checkFlow);
            checkWrap = new Panel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(16, 0, 16, 0), BackColor = CP.Ground };
            checkWrap.Controls.Add(checkCard);

            // ---- status ----
            statusLabel = new Label { Dock = DockStyle.Fill, Font = CP.Small, ForeColor = CP.Gray, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            dirtyLabel = new Label { Dock = DockStyle.Right, AutoSize = true, Font = CP.Label, ForeColor = CP.OrangeDeep, Visible = false, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 6, 0, 0) };
            var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(20, 2, 20, 2), BackColor = CP.Ground };
            statusPanel.Controls.Add(statusLabel);
            statusPanel.Controls.Add(dirtyLabel);

            var footer = new CreditFooter();

            Controls.Add(bodyWrap);
            Controls.Add(checkWrap);
            Controls.Add(statusPanel);
            Controls.Add(toolbar);
            Controls.Add(footer);
            Controls.Add(header);
            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>ความสูงการ์ดตรวจโดเมนตามเนื้อหา / fit the check card to its content</summary>
        void FitCheckCard()
        {
            checkResult.Visible = checkResult.Text.Length > 0;
            checkResult.MaximumSize = new Size(Math.Max(300, checkWrap.Width - 80), 0);
            checkWrap.Height = checkFlow.PreferredSize.Height + 18;
        }

        /// <summary>วาดสวิตช์เปิด/ปิด และปุ่มมนในตาราง / paint toggle switches and rounded buttons in the grid</summary>
        void GridCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            bool isBtn = e.ColumnIndex == colCheck.Index || e.ColumnIndex == colOpen.Index || e.ColumnIndex == colDel.Index;
            bool isToggle = e.ColumnIndex == colOn.Index;
            if (!isBtn && !isToggle) return;
            e.PaintBackground(e.CellBounds, false);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var b = e.CellBounds;
            if (isToggle)
            {
                bool on = e.Value is bool v && v;
                var r = new RectangleF(b.X + (b.Width - 40) / 2f, b.Y + (b.Height - 22) / 2f, 40, 22);
                using (var path = CP.Round(r, 11)) using (var br = new SolidBrush(on ? CP.Green : CP.Line)) g.FillPath(br, path);
                var knob = new RectangleF(on ? r.Right - 19 : r.X + 3, r.Y + 3, 16, 16);
                g.FillEllipse(Brushes.White, knob);
            }
            else
            {
                bool del = e.ColumnIndex == colDel.Index;
                string text = ((DataGridViewButtonColumn)grid.Columns[e.ColumnIndex]).Text;
                var size = TextRenderer.MeasureText(text, CP.Small);
                float w = Math.Min(b.Width - 8, size.Width + 20), h = 30;
                var r = new RectangleF(b.X + (b.Width - w) / 2f, b.Y + (b.Height - h) / 2f, w, h);
                using (var path = CP.Round(r, 9))
                {
                    g.FillPath(Brushes.White, path);
                    using (var p = new Pen(del ? Color.FromArgb(150, CP.Red) : CP.Line, 1.5f)) g.DrawPath(p, path);
                }
                TextRenderer.DrawText(g, text, CP.Small, Rectangle.Round(r), del ? CP.RedDeep : CP.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            e.Handled = true;
        }

        /// <summary>ใส่ข้อความทุกจุดตามภาษา / (re)apply all texts for the current language</summary>
        void ApplyTexts()
        {
            header.Subtitle.Text = L.T("แก้ไฟล์ hosts สำหรับย้ายเว็บไป server ใหม่", "Edit the hosts file for site migrations");
            header.LangLabel.Text = L.T("ภาษา", "Language");
            header.SetLang(L.Lang);

            pillList.Text = L.T("รายการโดเมน", "Domains");
            pillRaw.Text = L.T("แก้ไฟล์ดิบ", "Raw file");
            btnAdd.Text = L.T("＋  เพิ่ม", "＋  Add");
            btnImport.Text = L.T("⇩  นำเข้าจากไฟล์เดิม", "⇩  Import existing");
            btnAll.Text = L.T("เปิด/ปิดทั้งหมด  ▾", "All on/off  ▾");
            miAllOn.Text = L.T("เปิดทั้งหมด", "Turn all on");
            miAllOff.Text = L.T("ปิดทั้งหมด", "Turn all off");
            btnReload.Text = L.T("↻  โหลดใหม่", "↻  Reload");
            btnFlush.Text = L.T("⚡  ล้าง DNS", "⚡  Flush DNS");
            btnSave.Text = L.T("✓  บันทึก", "✓  Save");

            colOn.HeaderText = L.T("ใช้งาน", "On");
            colIp.HeaderText = "IP";
            colHosts.HeaderText = L.T("โดเมน (คั่นด้วยเว้นวรรค)", "Domains (space separated)");
            colNote.HeaderText = L.T("หมายเหตุ", "Note");
            colIssues.HeaderText = L.T("คำเตือน", "Warnings");
            colCheck.HeaderText = "";
            colCheck.Text = L.T("ตรวจ", "Check");
            colOpen.HeaderText = "";
            colOpen.Text = L.T("เปิดเว็บ", "Open");
            colDel.HeaderText = "";
            colDel.Text = L.T("ลบ", "Delete");

            emptyLabel.Text = L.T("ยังไม่มีรายการที่ชี้ไป server ใหม่", "No domains pointed to a new server yet");
            emptyAdd.Text = L.T("＋  เพิ่มรายการแรก", "＋  Add first entry");

            rawHint.Text = L.T("แก้ทั้งไฟล์ได้โดยตรง กด “ใช้ข้อความนี้” แล้วกด “บันทึก” ด้านบน",
                               "Edit the whole file. Click “Apply text”, then “Save” above.");
            rawReset.Text = L.T("รีเซ็ตเป็นค่าปัจจุบัน", "Reset to current");
            rawApply.Text = L.T("ใช้ข้อความนี้", "Apply text");

            checkLabel.Text = L.T("ตรวจโดเมน", "Check domain");
            checkBtn.Text = L.T("ตรวจ", "Check");
            dirtyLabel.Text = L.T("● ยังไม่ได้บันทึก", "● Unsaved changes");
            if (statusText != null) statusLabel.Text = statusText();

            grid.Invalidate();
            UpdateState();
            FitCheckCard();
        }

        void SetLang(Lang lang)
        {
            if (L.Lang == lang) return;
            L.Lang = lang;
            Settings.SaveLang(lang);
            checkResult.Text = "";
            ApplyTexts();
        }

        // ================= data <-> grid =================

        bool IsDirty => doc.Compose() != savedSnapshot;

        void RebuildGrid()
        {
            updating = true;
            grid.Rows.Clear();
            foreach (var e in doc.Entries)
            {
                int i = grid.Rows.Add(e.Enabled, e.Ip, e.Hosts, e.Note, "");
                grid.Rows[i].Tag = e;
            }
            updating = false;
            grid.ClearSelection();
            grid.CurrentCell = null;
            UpdateState();
        }

        /// <summary>คำเตือน, สี, แบนเนอร์, ปุ่มบันทึก / refresh everything derived from the data</summary>
        void UpdateState()
        {
            if (grid == null) return;
            updating = true;
            var issues = doc.Issues();
            int severe = 0, unsaved = 0;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!(row.Tag is HostEntry e)) continue;
                issues.TryGetValue(e.Id, out var list);
                list = list ?? new List<Issue>();
                bool hasSevere = list.Any(x => x.Severe);
                if (hasSevere) severe += list.Count(x => x.Severe);
                row.Cells[colIssues.Index].Value = string.Join(Environment.NewLine,
                    list.Select(x => (x.Severe ? "⚠ " : "ⓘ ") + x.Text));
                row.Cells[colIssues.Index].Style.ForeColor = hasSevere ? Orange : Muted;
                row.Cells[colHosts.Index].Style.BackColor = hasSevere ? Color.FromArgb(255, 237, 213) : Color.Empty;
                row.Cells[colIp.Index].Style.ForeColor = Util.IsValidIP(e.Ip) ? Color.Empty : Color.Red;
                row.DefaultCellStyle.ForeColor = e.Enabled ? Color.Empty : Color.FromArgb(160, 160, 160);
                // แถวที่ยังไม่บันทึก: พื้นส้มอ่อน + ป้ายบอก / unsaved rows: orange tint + label
                bool unsavedRow = IsUnsaved(e);
                if (unsavedRow) unsaved++;
                row.DefaultCellStyle.BackColor = unsavedRow ? CP.OrangeTint : Color.Empty;
                if (unsavedRow)
                {
                    var note = L.T("✎ ยังไม่บันทึก — กด “บันทึก” เพื่อให้มีผล", "✎ Not saved yet — click “Save” to apply");
                    var cur = row.Cells[colIssues.Index].Value as string ?? "";
                    row.Cells[colIssues.Index].Value = cur.Length == 0 ? note : note + Environment.NewLine + cur;
                    if (!hasSevere) row.Cells[colIssues.Index].Style.ForeColor = CP.OrangeDeep;
                }
            }
            unsaved += UnsavedRemovedCount();
            updating = false;

            int importable = doc.ImportableCount();
            bool hasRows = doc.Entries.Count > 0;
            grid.Visible = hasRows;
            empty.Visible = !hasRows;
            emptyImport.Visible = importable > 0;
            emptyImport.Text = L.T($"เลือกนำเข้าจากไฟล์ hosts เดิม ({importable} โดเมน)…",
                                   $"Import from existing hosts file ({importable} domains)…");

            var parts = new List<string>();
            if (unsaved > 0) parts.Add(L.T($"✎ มีการแก้ไข {unsaved} รายการที่ยังไม่บันทึก — ยังไม่มีผลกับเครื่อง",
                                           $"✎ {unsaved} change(s) not saved yet — they don't affect this PC until you save"));
            if (severe > 0) parts.Add(L.T($"⚠ พบโดเมนซ้ำที่ชี้คนละ IP {severe} จุด", $"⚠ {severe} duplicate domain(s) point to different IPs"));
            if (importable > 0) parts.Add(L.T($"มี {importable} โดเมนในไฟล์เดิมที่แอปยังไม่ได้จัดการ", $"{importable} domain(s) in the file aren't managed by this app"));
            banner.Visible = hasRows && parts.Count > 0;
            bannerLabel.Text = string.Join("    ", parts);
            bannerLabel.ForeColor = severe > 0 || unsaved > 0 ? Orange : Muted;
            bannerSave.Visible = unsaved > 0;
            bannerSave.Text = L.T("บันทึกตอนนี้", "Save now");
            bannerImport.Visible = importable > 0;
            bannerImport.Text = L.T("เลือกนำเข้ารายการเดิม…", "Choose entries to import…");

            bool dirty = IsDirty;
            dirtyLabel.Visible = dirty;
            btnSave.Enabled = dirty;
        }

        void GridCellValueChanged(object sender, DataGridViewCellEventArgs a)
        {
            if (updating || a.RowIndex < 0) return;
            var row = grid.Rows[a.RowIndex];
            if (!(row.Tag is HostEntry e)) return;
            var v = row.Cells[a.ColumnIndex].Value;
            if (a.ColumnIndex == colOn.Index) e.Enabled = v is bool b && b;
            else if (a.ColumnIndex == colIp.Index) e.Ip = (v as string ?? "").Trim();
            else if (a.ColumnIndex == colHosts.Index) e.Hosts = (v as string ?? "").Trim();
            else if (a.ColumnIndex == colNote.Index) e.Note = (v as string ?? "").Trim();
            else return;
            UpdateState();
        }

        void GridCellContentClick(object sender, DataGridViewCellEventArgs a)
        {
            if (a.RowIndex < 0) return;
            if (!(grid.Rows[a.RowIndex].Tag is HostEntry e)) return;
            if (a.ColumnIndex == colCheck.Index)
            {
                var h = e.HostList.FirstOrDefault();
                if (h != null) { checkInput.Text = h; Check(h); }
            }
            else if (a.ColumnIndex == colOpen.Index)
            {
                var h = e.HostList.FirstOrDefault();
                // เปิดผ่าน explorer เพื่อให้เบราว์เซอร์ไม่รันด้วยสิทธิ์ admin
                if (h != null) CP.OpenUrl("http://" + h);
            }
            else if (a.ColumnIndex == colDel.Index)
            {
                BeginInvoke(new Action(() =>
                {
                    doc.Entries.Remove(e);
                    RebuildGrid();
                }));
            }
        }

        // ================= actions =================

        void LoadHosts()
        {
            try
            {
                doc.Parse(File.ReadAllText(hostsPath));
                savedSnapshot = doc.Compose();
                savedLines = new HashSet<string>(doc.Entries.Where(x => x.IsValid).Select(x => x.Line()));
                if (lastIP.Length == 0 && doc.Entries.Count > 0) lastIP = doc.Entries.Last().Ip;
                RebuildGrid();
                if (tabs.SelectedTab == tabRaw) raw.Text = ToCrlf(doc.Compose());
                SetStatus(() => L.T($"โหลดไฟล์ hosts แล้ว ({Now()})", $"Loaded hosts file ({Now()})") + "  —  " + hostsPath);
            }
            catch (Exception ex)
            {
                SetStatus(() => L.T("อ่านไฟล์ไม่ได้: ", "Can't read file: ") + ex.Message);
            }
        }

        void ShowAdd()
        {
            using (var d = new AddDialog(lastIP))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var r = doc.Add(d.IpText, d.DomainList, d.AddWww, d.NoteText);
                lastIP = d.IpText;
                RebuildGrid();
                SetStatus(() => L.T($"เพิ่ม {r.added} / อัปเดต {r.updated} รายการ — อย่าลืมกดบันทึก",
                              $"Added {r.added} / updated {r.updated} — don't forget to Save"));
            }
        }

        void ShowImport()
        {
            using (var d = new ImportDialog(doc.ImportCandidates()))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                int n = doc.ImportSelected(d.Candidates);
                RebuildGrid();
                SetStatus(() => L.T($"นำเข้า {n} รายการจากไฟล์เดิม — ตรวจดูแล้วกดบันทึก",
                              $"Imported {n} existing entries — review, then Save"));
            }
        }

        void SetAll(bool on)
        {
            foreach (var e in doc.Entries) e.Enabled = on;
            RebuildGrid();
        }

        void Save()
        {
            grid.EndEdit();
            var bad = doc.Entries.FirstOrDefault(e => e.IsValid && !Util.IsValidIP(e.Ip));
            if (bad != null)
            {
                SetStatus(() => L.T("IP ไม่ถูกต้อง: ", "Invalid IP: ") + $"{bad.Ip} ({bad.Hosts})");
                MessageBox.Show(this, L.T("IP ไม่ถูกต้อง: ", "Invalid IP: ") + bad.Ip, "Hosts Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int severe = doc.SevereCount();
            if (severe > 0)
            {
                var msg = L.T($"พบโดเมนซ้ำที่ชี้คนละ IP {severe} จุด\n\nบางแถวจะไม่มีผล เพราะมีบรรทัดอื่นที่ถูกใช้ก่อน ต้องการบันทึกต่อหรือไม่?",
                              $"Found {severe} duplicate domain(s) pointing to different IPs.\n\nSome rows will have no effect because another line takes priority. Save anyway?");
                if (MessageBox.Show(this, msg, "Hosts Manager", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                {
                    SetStatus(() => L.T("ยังไม่ได้บันทึก — แก้แถวที่มีเครื่องหมาย ⚠ ก่อน", "Not saved — fix the rows marked ⚠ first"));
                    return;
                }
            }
            try
            {
                var backup = hostsPath + ".hostsmanager.bak";
                if (File.Exists(hostsPath))
                {
                    File.Copy(hostsPath, backup, true);
                    var attr = File.GetAttributes(hostsPath);
                    if ((attr & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(hostsPath, attr & ~FileAttributes.ReadOnly);
                }
                File.WriteAllText(hostsPath, ToCrlf(doc.Compose()), new UTF8Encoding(false));
                bool flushed = RunFlush();
                LoadHosts();
                SetStatus(() => flushed
                    ? L.T($"✓ บันทึกและล้าง DNS แล้ว ({Now()}) — สำรองไฟล์เดิมไว้ที่ {backup}",
                          $"✓ Saved and flushed DNS ({Now()}) — backup at {backup}")
                    : L.T($"✓ บันทึกแล้ว ({Now()}) แต่ล้าง DNS ไม่สำเร็จ", $"✓ Saved ({Now()}) but DNS flush failed"));
            }
            catch (UnauthorizedAccessException)
            {
                var msg = L.T("ไม่มีสิทธิ์เขียนไฟล์ hosts — ปิดแอปแล้วเปิดใหม่ด้วย “Run as administrator” หรือโปรแกรมแอนติไวรัสอาจกันการแก้ไฟล์นี้อยู่",
                              "No permission to write the hosts file — reopen the app with “Run as administrator”, or your antivirus may be blocking changes to this file.");
                SetStatus(() => "✗ " + msg);
                MessageBox.Show(this, msg, "Hosts Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                SetStatus(() => L.T("✗ บันทึกไม่สำเร็จ: ", "✗ Save failed: ") + ex.Message);
                MessageBox.Show(this, ex.Message, "Hosts Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void Flush()
        {
            bool ok = RunFlush();
            SetStatus(() => ok
                ? L.T($"✓ ล้าง DNS แล้ว ({Now()})", $"✓ DNS flushed ({Now()})")
                : L.T("✗ ล้าง DNS ไม่สำเร็จ", "✗ DNS flush failed"));
        }

        static bool RunFlush()
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns")
                {
                    CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    if (p == null) return false;
                    p.WaitForExit(10000);
                    return p.HasExited && p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        void Check(string input)
        {
            var d = Util.CleanDomain(input);
            if (d.Length == 0) return;
            checkInput.Text = d;
            checkBtn.Enabled = false;
            checkResult.Text = L.T("กำลังตรวจ…", "Checking…");
            FitCheckCard();
            Task.Run(() =>
            {
                List<string> local, pub;
                try
                {
                    local = System.Net.Dns.GetHostAddresses(d).Select(a => a.ToString()).Distinct().ToList();
                }
                catch { local = new List<string>(); }
                try { pub = PublicDns.QueryA(d); }
                catch { pub = new List<string>(); }
                return (local, pub);
            }).ContinueWith(t =>
            {
                if (IsDisposed) return;
                var (local, pub) = t.Result;
                var sb = new StringBuilder();
                sb.AppendLine(L.T("โดเมน: ", "Domain: ") + d);
                sb.AppendLine(L.T("เครื่องนี้เห็น (hosts/แคช): ", "This PC sees (hosts/cache): ") + (local.Count == 0 ? "-" : string.Join(", ", local)));
                sb.Append(L.T("DNS สาธารณะ (1.1.1.1): ", "Public DNS (1.1.1.1):        ") + (pub.Count == 0 ? "-" : string.Join(", ", pub)));
                if (local.Count > 0 && pub.Count > 0)
                    sb.Append(local.Intersect(pub).Any()
                        ? L.T("\n→ ตรงกับ DNS จริงแล้ว", "\n→ Matches real DNS")
                        : L.T("\n→ เครื่องนี้ชี้ไปคนละที่กับ DNS จริง (กำลังใช้ค่าจาก hosts)",
                              "\n→ This PC points somewhere different from real DNS (using hosts)"));
                if (doc.Entries.Any(x => IsUnsaved(x) && x.HostList.Any(h => h.ToLowerInvariant() == d)))
                    sb.Append(L.T("\n⚠ โดเมนนี้อยู่ในรายการที่ยังไม่บันทึก — ค่าที่เห็นยังไม่ใช่ IP ใหม่ กด “บันทึก” ก่อน แล้วตรวจอีกครั้ง",
                                  "\n⚠ This domain is in a row that isn't saved yet — the result above is NOT the new IP. Click “Save” first, then check again"));
                else if (IsDirty)
                    sb.Append(L.T("\n⚠ ยังมีการแก้ไขที่ไม่ได้บันทึก — กด “บันทึก” เพื่อให้มีผลกับเครื่อง",
                                  "\n⚠ There are unsaved changes — click “Save” to apply them"));
                checkResult.Text = sb.ToString();
                checkBtn.Enabled = true;
                FitCheckCard();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // ================= helpers =================

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            grid.EndEdit();
            if (!smoke && e.CloseReason == CloseReason.UserClosing && IsDirty &&
                !Confirm(L.T("ยังมีการแก้ไขที่ไม่ได้บันทึก ปิดแอปเลยหรือไม่?", "You have unsaved changes. Close anyway?")))
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        bool Confirm(string msg) =>
            MessageBox.Show(this, msg, "Hosts Manager", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        Func<string> statusText;
        string stamp = "";

        /// <summary>ข้อความสถานะที่แปลใหม่ได้เมื่อสลับภาษา / status text that re-translates on language switch</summary>
        void SetStatus(Func<string> f)
        {
            stamp = DateTime.Now.ToString("HH:mm:ss");
            statusText = f;
            statusLabel.Text = f();
        }

        string Now() => stamp;

        static string ToCrlf(string s) => s.Replace("\r\n", "\n").Replace("\n", "\r\n");

        static void TryStart(string file, string args)
        {
            try { Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true }); } catch { }
        }

        // ================= smoke test (CI) =================

        bool smoke;

        /// <summary>เปิดทุกหน้าจอแบบอัตโนมัติเพื่อตรวจว่าไม่ crash / exercise the UI headlessly</summary>
        public void RunSmokeTest()
        {
            smoke = true;
            Show();
            Application.DoEvents();
            doc.Add("203.0.113.10", new[] { "smoke-test.com" }, true, "smoke");
            doc.Add("203.0.113.11", new[] { "other.com" }, false, "");
            doc.Entries.Add(new HostEntry { Ip = "203.0.113.99", Hosts = "smoke-test.com" });
            RebuildGrid();
            foreach (var lang in new[] { Lang.En, Lang.Th })
            {
                L.Lang = lang;
                ApplyTexts();
                tabs.SelectedTab = tabRaw;
                Application.DoEvents();
                tabs.SelectedTab = tabList;
                Application.DoEvents();
                using (var a = new AddDialog("1.2.3.4")) { a.Show(this); Application.DoEvents(); a.Close(); }
                using (var i = new ImportDialog(doc.ImportCandidates())) { i.Show(this); Application.DoEvents(); i.Close(); }
            }
            SetAll(false);
            SetAll(true);
            if (doc.SevereCount() < 1) throw new Exception("expected a severe duplicate in smoke data");
            Close();
        }

        /// <summary>ถ่ายภาพหน้าจอทุกหน้าต่างทั้งสองภาษา (ใช้ใน CI) / capture screenshots of every window</summary>
        public void CaptureScreens(string dir)
        {
            smoke = true;
            Directory.CreateDirectory(dir);
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.WorkingArea;
            Show();
            doc.Add("203.0.113.10", new[] { "client-shop.co.th", "colorpack-demo.com" }, true, "ลูกค้า A / client A");
            doc.Add("203.0.113.20", new[] { "news-site.com" }, true, "");
            doc.Entries.Add(new HostEntry { Ip = "203.0.113.99", Hosts = "news-site.com", Note = "old server" });
            doc.Entries.Add(new HostEntry { Ip = "203.0.113.30", Hosts = "paused-site.org", Enabled = false });
            RebuildGrid();
            checkInput.Text = "client-shop.co.th";
            foreach (var lang in new[] { Lang.Th, Lang.En })
            {
                L.Lang = lang;
                ApplyTexts();
                var tag = lang == Lang.Th ? "th" : "en";
                checkResult.Text = L.T("โดเมน: client-shop.co.th\nเครื่องนี้เห็น (hosts/แคช): 203.0.113.10\nDNS สาธารณะ (1.1.1.1): 198.51.100.7\n→ เครื่องนี้ชี้ไปคนละที่กับ DNS จริง (กำลังใช้ค่าจาก hosts)",
                                       "Domain: client-shop.co.th\nThis PC sees (hosts/cache): 203.0.113.10\nPublic DNS (1.1.1.1):        198.51.100.7\n→ This PC points somewhere different from real DNS (using hosts)");
                FitCheckCard();
                tabs.SelectedTab = tabList;
                Capture(this, Path.Combine(dir, $"1-main-{tag}.png"));
                tabs.SelectedTab = tabRaw;
                Capture(this, Path.Combine(dir, $"2-raw-{tag}.png"));
                tabs.SelectedTab = tabList;
                using (var a = new AddDialog("203.0.113.10"))
                {
                    a.Show(this);
                    Capture(a, Path.Combine(dir, $"3-add-{tag}.png"));
                    a.Close();
                }
                using (var i = new ImportDialog(doc.ImportCandidates()))
                {
                    i.Show(this);
                    Capture(i, Path.Combine(dir, $"4-import-{tag}.png"));
                    i.Close();
                }
            }
            Close();
        }

        static void Capture(Form f, string path)
        {
            f.Activate();
            f.BringToFront();
            for (int k = 0; k < 4; k++) { Application.DoEvents(); System.Threading.Thread.Sleep(150); }
            var b = f.Bounds;
            using (var bmp = new Bitmap(b.Width, b.Height))
            {
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(b.Location, Point.Empty, b.Size);
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
