using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace AlwaysOnTopMemo
{
    public class MainForm : Form
    {
        private TabControl tabControl;
        private int hoverCloseIndex = -1;
        private int hoverTabIndex = -1; // タブ全体のホバー判定用
        private const int MAX_TABS = 10;
        public static Icon AppIcon;

        private string savePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "memo.json"
        );

        private int dragTabIndex = -1;
        private bool isDragging = false;

        // ゴースト用
        private Rectangle? dragGhostRect = null;

        // 右クリック対象
        private int rightClickTabIndex = -1;

        // =========================
        // 検索用
        // =========================
        private Panel searchPanel;
        private TextBox searchBox;
        private Button btnNext;
        private Button btnPrev;
        private Button btnClose;

        private string lastKeyword = "";
        private int currentTabIndex = 0;
        private int currentIndex = 0;

        public MainForm()
        {
            AppIcon = LoadIcon("icon.ico");
            this.Icon = AppIcon;

            Text = "CaptureMemo";
            Width = 420;
            Height = 650;
            this.BackColor = Color.White; // 全体の背景色をクリーンな白に

            this.TopMost = true;
            this.Activated += (s, e) => this.TopMost = true;
            this.Deactivate += (s, e) => this.TopMost = true;

            // --- TabControlの設定 ---
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;

            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.ItemSize = new Size(60, 24);
            tabControl.Font = new Font("Meiryo", 9f); // サイズに合わせてフォントを微小調整

            tabControl.DrawItem += TabControl_DrawItem;
            tabControl.MouseDown += TabControl_MouseDown;
            tabControl.Selecting += TabControl_Selecting;

            tabControl.MouseMove += TabControl_MouseMove;
            tabControl.MouseLeave += (s, e) =>
            {
                hoverCloseIndex = -1;
                hoverTabIndex = -1;
                tabControl.Invalidate();
            };

            tabControl.MouseUp += TabControl_MouseUp;
            tabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
            Controls.Add(tabControl);

            // =========================
            // 検索UI
            // =========================
            searchPanel = new Panel();
            searchPanel.Height = 44;
            searchPanel.Dock = DockStyle.Top;
            searchPanel.Visible = false;
            searchPanel.BackColor = Color.FromArgb(245, 245, 245);

            searchBox = new TextBox();
            searchBox.Left = 12;
            searchBox.Top = 10;
            searchBox.Width = 200;
            searchBox.Font = new Font("Meiryo", 10);
            searchBox.BorderStyle = BorderStyle.FixedSingle;

            btnPrev = CreateFlatButton("↑", 220, 9, 32);
            btnNext = CreateFlatButton("↓", 257, 9, 32);
            btnClose = CreateFlatButton("×", 294, 9, 32);

            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(btnPrev);
            searchPanel.Controls.Add(btnNext);
            searchPanel.Controls.Add(btnClose);

            Controls.Add(searchPanel);
            searchPanel.BringToFront();

            searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) searchPanel.Visible = false; };
            btnNext.Click += (s, e) => { if (lastKeyword != searchBox.Text) StartSearch(searchBox.Text); else SearchNext(); };
            btnPrev.Click += (s, e) => { if (lastKeyword != searchBox.Text) StartSearch(searchBox.Text); else SearchPrev(); };
            btnClose.Click += (s, e) => searchPanel.Visible = false;

            LoadFromJson();

            if (tabControl.TabCount == 0) AddNewTab();

            AddPlusTab();
            FixPlusTabPosition();

            // 5秒ごとに自動保存
            var timer = new Timer();
            timer.Interval = 5000;
            timer.Tick += (s, e) => SaveToJson();
            timer.Start();

            FormClosing += (s, e) => SaveToJson();
        }

        private Button CreateFlatButton(string text, int x, int y, int size)
        {
            var btn = new Button();
            btn.Text = text;
            btn.Font = new Font("Meiryo", 10, FontStyle.Bold);
            btn.SetBounds(x, y, size, size - 6);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.FromArgb(230, 230, 230);
            btn.Cursor = Cursors.Hand;

            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(210, 210, 210);
            btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(230, 230, 230);

            return btn;
        }

        // =========================
        // 検索処理
        // =========================
        private void StartSearch(string keyword) { if (string.IsNullOrEmpty(keyword)) return; lastKeyword = keyword; currentTabIndex = tabControl.SelectedIndex; currentIndex = 0; SearchNext(); }
        private void SearchNext() { Search(true); }
        private void SearchPrev() { Search(false); }
        private void Search(bool forward)
        {
            if (string.IsNullOrEmpty(lastKeyword)) return;

            int tabCount = tabControl.TabCount;

            for (int t = 0; t < tabCount; t++)
            {
                int index = forward ? (currentTabIndex + t) % tabCount : (currentTabIndex - t + tabCount) % tabCount;
                var tab = tabControl.TabPages[index];

                if (tab.Text == "+") continue;

                var editor = tab.Controls[0] as RichTextBox;
                if (editor == null) continue;

                int start = (index == currentTabIndex) ? currentIndex : (forward ? 0 : editor.TextLength);
                int found = -1;

                if (forward) { if (start <= editor.TextLength) found = editor.Text.IndexOf(lastKeyword, start, StringComparison.OrdinalIgnoreCase); }
                else { if (start > 0) found = editor.Text.LastIndexOf(lastKeyword, start - 1, StringComparison.OrdinalIgnoreCase); }

                if (found >= 0)
                {
                    tabControl.SelectedIndex = index;

                    editor.SelectionStart = found;
                    editor.SelectionLength = lastKeyword.Length;
                    editor.ScrollToCaret();
                    editor.Focus();

                    currentTabIndex = index;
                    currentIndex = forward ? found + lastKeyword.Length : found;
                    return;
                }
            }

            MessageBox.Show("見つかりません");
        }

        // =========================
        // タブ管理
        // =========================
        private void AddPlusTab() { tabControl.TabPages.Add(new TabPage("+")); }
        private bool IsPlusTab(int index) { return tabControl.TabPages[index].Text == "+"; }
        private void FixPlusTabPosition()
        {
            TabPage plus = null;
            foreach (TabPage tab in tabControl.TabPages) { if (tab.Text == "+") { plus = tab; break; } }
            if (plus == null) return;

            tabControl.TabPages.Remove(plus);
            tabControl.TabPages.Add(plus);
        }

        // =========================
        // タブ追加
        // =========================
        private void AddNewTab()
        {
            if (tabControl.TabCount - 1 >= MAX_TABS)
            {
                MessageBox.Show("最大10タブです");
                return;
            }

            int nextNo = GetNextTabNumber();
            if (nextNo == -1) return;

            var editor = CreateEditor();
            var tab = new TabPage($"Tab {nextNo}");
            tab.BackColor = Color.White;
            tab.Controls.Add(editor);

            tabControl.TabPages.Add(tab); // 一旦追加
            FixPlusTabPosition();         // ＋を右端へ
            tabControl.SelectedTab = tab;
        }

        private void CloseTab(int index)
        {
            if (tabControl.TabCount <= 2) return;// ＋含めて最低2

            tabControl.TabPages.RemoveAt(index);
            FixPlusTabPosition();
        }

        private void TabControl_MouseMove(object sender, MouseEventArgs e)
        {
            int newHoverCloseIndex = -1;
            int newHoverTabIndex = -1;

            for (int i = 0; i < tabControl.TabCount; i++)
            {
                var tabRect = tabControl.GetTabRect(i);
                if (tabRect.Contains(e.Location))
                {
                    newHoverTabIndex = i;
                }

                var tab = tabControl.TabPages[i];
                if (tab.Tag is Rectangle rect && rect.Contains(e.Location))
                {
                    newHoverCloseIndex = i;
                    break;
                }
            }

            bool needsRedraw = false;
            if (hoverCloseIndex != newHoverCloseIndex) { hoverCloseIndex = newHoverCloseIndex; needsRedraw = true; }
            if (hoverTabIndex != newHoverTabIndex) { hoverTabIndex = newHoverTabIndex; needsRedraw = true; }

            if (needsRedraw) tabControl.Invalidate(); // 再描画

            // ドラッグ中
            if (isDragging && dragTabIndex >= 0)
            {
                dragGhostRect = new Rectangle(e.X - 30, e.Y - 10, 60, 24);
                for (int i = 0; i < tabControl.TabCount; i++)
                {
                    if (i == dragTabIndex || IsPlusTab(i)) continue;

                    var rect = tabControl.GetTabRect(i);
                    if (rect.Contains(e.Location))
                    {
                        var dragged = tabControl.TabPages[dragTabIndex];

                        tabControl.TabPages.RemoveAt(dragTabIndex);
                        tabControl.TabPages.Insert(i, dragged);

                        dragTabIndex = i;
                        tabControl.SelectedTab = dragged;
                        break;
                    }
                }

                tabControl.Invalidate();
            }
        }

        // =========================
        // モダンな描画処理 (コンパクト版に合わせて座標調整)
        // =========================
        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var tabRect = tabControl.GetTabRect(e.Index);
            var tab = tabControl.TabPages[e.Index];
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool isHovered = (hoverTabIndex == e.Index);

            // --- 色の定義 ---
            Color bgColor = isSelected ? Color.White : (isHovered ? Color.FromArgb(235, 235, 235) : Color.FromArgb(245, 245, 245));
            Color textColor = isSelected ? Color.Black : Color.FromArgb(120, 120, 120);
            Color accentColor = Color.FromArgb(0, 120, 215);

            // 1. タブの背景
            using (var b = new SolidBrush(bgColor)) { g.FillRectangle(b, tabRect); }

            // 2. ＋タブの場合の特別描画
            if (tab.Text == "+")
            {
                using (var b = new SolidBrush(textColor))
                {
                    // サイズを 14 -> 12 に戻す
                    TextRenderer.DrawText(g, "＋", new Font(Font.FontFamily, 12, FontStyle.Bold), tabRect, textColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                return;
            }

            // 3. 選択状態の装飾
            if (isSelected)
            {
                // アクセントライン
                using (var p = new Pen(accentColor, 3))
                {
                    g.DrawLine(p, tabRect.Left, tabRect.Top + 1, tabRect.Right, tabRect.Top + 1);
                }
            }
            else
            {
                // 非選択タブの境界線 (高さ24に合わせて短く)
                using (var p = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    g.DrawLine(p, tabRect.Right - 1, tabRect.Top + 5, tabRect.Right - 1, tabRect.Bottom - 5);
                }
            }

            // 4. タイトル (60px幅に合わせて余白を削る)
            string title = tab.Text;
            TextRenderer.DrawText(g, title, Font,
                new Rectangle(tabRect.X + 4, tabRect.Y + 1, tabRect.Width - 22, tabRect.Height),
                textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 5. ×ボタンの描画 (高さ24に合わせて位置を再計算)
            Rectangle closeRect = new Rectangle(tabRect.Right - 18, tabRect.Top + 5, 14, 14);

            if (hoverCloseIndex == e.Index)
            {
                using (var b = new SolidBrush(Color.FromArgb(232, 17, 35))) { g.FillEllipse(b, closeRect); }
                TextRenderer.DrawText(g, "×", new Font(Font.FontFamily, 8, FontStyle.Bold), closeRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                Color crossColor = isSelected ? Color.FromArgb(150, 150, 150) : Color.FromArgb(200, 200, 200);
                TextRenderer.DrawText(g, "×", new Font(Font.FontFamily, 8, FontStyle.Bold), closeRect, crossColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            tab.Tag = closeRect;

            // 6. ドラッグ中のゴースト描画
            if (isDragging && dragGhostRect.HasValue)
            {
                using var b = new SolidBrush(Color.FromArgb(60, accentColor));
                g.FillRectangle(b, dragGhostRect.Value);
            }
        }

        // =========================
        // クリック処理
        // =========================
        private void TabControl_MouseDown(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl.TabCount; i++)
            {
                var tab = tabControl.TabPages[i];
                if (e.Button == MouseButtons.Right && tabControl.GetTabRect(i).Contains(e.Location))
                {
                    if (IsPlusTab(i)) return;
                    rightClickTabIndex = i;
                    ShowContextMenu(e.Location);
                    return;
                }

                if (IsPlusTab(i) && tabControl.GetTabRect(i).Contains(e.Location)) { AddNewTab(); return; }

                if (tab.Tag is Rectangle rect && rect.Contains(e.Location)) { CloseTab(i); return; }

                if (tabControl.GetTabRect(i).Contains(e.Location))
                {
                    dragTabIndex = i;
                    isDragging = true;
                }
            }
        }

        private void ShowContextMenu(Point location)
        {
            var menu = new ContextMenuStrip();
            menu.RenderMode = ToolStripRenderMode.Professional;
            menu.Items.Add("削除", null, (s, e) => CloseTab(rightClickTabIndex));
            menu.Items.Add("複製", null, (s, e) =>
            {
                var src = tabControl.TabPages[rightClickTabIndex];
                var editor = CreateEditor();
                editor.Rtf = ((RichTextBox)src.Controls[0]).Rtf;
                var tab = new TabPage(src.Text + "_copy");
                tab.Controls.Add(editor);
                tabControl.TabPages.Insert(rightClickTabIndex + 1, tab);
                FixPlusTabPosition();
            });
            menu.Items.Add("名前変更", null, (s, e) =>
            {
                var tab = tabControl.TabPages[rightClickTabIndex];
                string input = Microsoft.VisualBasic.Interaction.InputBox("タブ名を入力", "名前変更", tab.Text);
                if (!string.IsNullOrWhiteSpace(input)) tab.Text = input;
            });
            menu.Show(tabControl, location);
        }

        private void TabControl_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
            dragTabIndex = -1;
            dragGhostRect = null;
            tabControl.Invalidate();
        }

        private void TabControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl.TabCount; i++)
            {
                if (IsPlusTab(i)) continue;
                if (tabControl.GetTabRect(i).Contains(e.Location))
                {
                    string input = Microsoft.VisualBasic.Interaction.InputBox("タブ名を入力", "名前変更", tabControl.TabPages[i].Text);
                    if (!string.IsNullOrWhiteSpace(input)) tabControl.TabPages[i].Text = input;
                    break;
                }
            }
        }

        private void TabControl_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage != null && e.TabPage.Text == "+") e.Cancel = true;
        }

        // =========================
        // エディタ
        // =========================
        private RichTextBox CreateEditor()
        {
            var editor = new RichTextBox();
            editor.Dock = DockStyle.Fill;
            editor.Font = new Font("Meiryo", 11);
            editor.BorderStyle = BorderStyle.None;
            editor.BackColor = Color.White;
            editor.AllowDrop = true;

            editor.KeyDown += Editor_KeyDown;
            editor.DragEnter += Editor_DragEnter;
            editor.DragDrop += Editor_DragDrop;

            return editor;
        }

        private RichTextBox GetEditor() { return tabControl.SelectedTab?.Controls.Count > 0 ? tabControl.SelectedTab.Controls[0] as RichTextBox : null; }

        // =========================
        // キー・ファイル操作
        // =========================
        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.T) { AddNewTab(); e.SuppressKeyPress = true; }
            if (e.Control && e.KeyCode == Keys.W) { CloseTab(tabControl.SelectedIndex); e.SuppressKeyPress = true; }
            if (e.Control && e.KeyCode == Keys.V) { PasteClipboard(); e.SuppressKeyPress = true; }
            if (e.Control && e.KeyCode == Keys.S) { SaveAsImage(); e.SuppressKeyPress = true; }
        }

        private void PasteClipboard()
        {
            var editor = GetEditor();
            if (editor == null) return;
            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                if (img != null) InsertImage(editor, new Bitmap(img));
            }
            else if (Clipboard.ContainsText()) { editor.Paste(); }
        }

        private void Editor_DragEnter(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; }
        private void Editor_DragDrop(object sender, DragEventArgs e)
        {
            var editor = GetEditor();
            if (editor == null) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var file in files)
            {
                try { using var img = Image.FromFile(file); InsertImage(editor, new Bitmap(img)); continue; } catch { }
                try { editor.AppendText(File.ReadAllText(file)); } catch { }
            }
        }

        private void InsertImage(RichTextBox editor, Image img)
        {
            if (editor == null || img == null) return;
            var backup = Clipboard.GetDataObject();
            try { Clipboard.SetImage(img); editor.Paste(); }
            finally { try { if (backup != null) Clipboard.SetDataObject(backup); } catch { } }
        }

        private void SaveAsImage()
        {
            var editor = GetEditor();
            if (editor == null) return;
            using var dlg = new SaveFileDialog();
            dlg.Filter = "PNG|*.png";
            if (dlg.ShowDialog() != DialogResult.OK) return;

            int width = editor.Width;
            int height = editor.Height;
            int totalHeight = editor.GetPositionFromCharIndex(editor.TextLength).Y + height;

            Bitmap finalBmp = new Bitmap(width, totalHeight);
            using (Graphics g = Graphics.FromImage(finalBmp))
            {
                int offset = 0;
                while (offset < totalHeight)
                {
                    editor.AutoScrollOffset = new Point(0, offset);
                    Bitmap tmp = new Bitmap(width, height);
                    editor.DrawToBitmap(tmp, new Rectangle(0, 0, width, height));
                    g.DrawImage(tmp, 0, offset);
                    offset += height;
                }
            }
            finalBmp.Save(dlg.FileName, ImageFormat.Png);
        }

        // =========================
        // データ保存/読み込み
        // =========================
        private void SaveToJson()
        {
            var list = new List<TabData>();
            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Text == "+") continue;
                var editor = tab.Controls[0] as RichTextBox;
                list.Add(new TabData { Title = tab.Text, Rtf = editor.Rtf });
            }
            File.WriteAllText(savePath, JsonSerializer.Serialize(list));
        }

        private void LoadFromJson()
        {
            if (!File.Exists(savePath)) return;
            try
            {
                var list = JsonSerializer.Deserialize<List<TabData>>(File.ReadAllText(savePath));
                foreach (var item in list)
                {
                    var editor = CreateEditor();
                    editor.Rtf = item.Rtf;
                    var tab = new TabPage(item.Title);
                    tab.BackColor = Color.White;
                    tab.Controls.Add(editor);
                    tabControl.TabPages.Add(tab);
                }
            }
            catch { }
        }

        private static Icon LoadIcon(string resourceName)
        {
            var assembly = typeof(MainForm).Assembly;
            string fullName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
            if (fullName != null)
            {
                using Stream stream = assembly.GetManifestResourceStream(fullName);
                if (stream != null) return new Icon(stream);
            }
            return SystemIcons.Application;
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                searchPanel.Visible = true;
                searchBox.Focus();
                searchBox.SelectAll();
                e.SuppressKeyPress = true;
            }
        }

        private int GetNextTabNumber()
        {
            var used = new HashSet<int>();
            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Text.StartsWith("Tab ") && int.TryParse(tab.Text.Replace("Tab ", ""), out int num)) used.Add(num);
            }
            for (int i = 1; i <= MAX_TABS; i++) { if (!used.Contains(i)) return i; }
            return -1;
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }

    class TabData
    {
        public string Title { get; set; }
        public string Rtf { get; set; }
    }
}