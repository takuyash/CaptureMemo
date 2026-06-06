using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace AlwaysOnTopMemo
{
    public class MainForm : Form
    {
        private TabControl tabControl;
        private int hoverCloseIndex = -1;
        private const int MAX_TABS = 10;
        public static Icon AppIcon;

        private int tabIndexCounter = 1;

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
            Width = 400;
            Height = 600;

            this.TopMost = true;
            this.Activated += (s, e) => this.TopMost = true;
            this.Deactivate += (s, e) => this.TopMost = true;

            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;

            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.ItemSize = new Size(60, 24);

            tabControl.DrawItem += TabControl_DrawItem;
            tabControl.MouseDown += TabControl_MouseDown;
            tabControl.Selecting += TabControl_Selecting;

            tabControl.MouseMove += TabControl_MouseMove;
            tabControl.MouseLeave += (s, e) =>
            {
                hoverCloseIndex = -1;
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
            searchPanel.Height = 30;
            searchPanel.Dock = DockStyle.Top;
            searchPanel.Visible = false;

            searchBox = new TextBox();
            searchBox.Left = 5;
            searchBox.Width = 180;

            btnPrev = new Button();
            btnPrev.Text = "↑";
            btnPrev.Left = 190;
            btnPrev.Width = 30;

            btnNext = new Button();
            btnNext.Text = "↓";
            btnNext.Left = 225;
            btnNext.Width = 30;

            btnClose = new Button();
            btnClose.Text = "×";
            btnClose.Left = 260;
            btnClose.Width = 30;

            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(btnPrev);
            searchPanel.Controls.Add(btnNext);
            searchPanel.Controls.Add(btnClose);

            Controls.Add(searchPanel);
            searchPanel.BringToFront();

            searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    searchPanel.Visible = false;
                }
            };

            btnNext.Click += (s, e) =>
            {
                if (lastKeyword != searchBox.Text)
                    StartSearch(searchBox.Text);
                else
                    SearchNext();
            };

            btnPrev.Click += (s, e) =>
            {
                if (lastKeyword != searchBox.Text)
                    StartSearch(searchBox.Text);
                else
                    SearchPrev();
            };
            btnClose.Click += (s, e) => searchPanel.Visible = false;

            LoadFromJson();

            if (tabControl.TabCount == 0)
                AddNewTab();

            AddPlusTab();
            FixPlusTabPosition();

            // 5秒ごとに自動保存
            var timer = new Timer();
            timer.Interval = 5000;
            timer.Tick += (s, e) => SaveToJson();
            timer.Start();

            FormClosing += (s, e) => SaveToJson();
        }

        // =========================
        // 検索処理
        // =========================
        private void StartSearch(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return;

            lastKeyword = keyword;
            currentTabIndex = tabControl.SelectedIndex;
            currentIndex = 0;

            SearchNext();
        }

        private void SearchNext()
        {
            Search(true);
        }

        private void SearchPrev()
        {
            Search(false);
        }

        private void Search(bool forward)
        {
            if (string.IsNullOrEmpty(lastKeyword)) return;

            int tabCount = tabControl.TabCount;

            for (int t = 0; t < tabCount; t++)
            {
                int index;

                if (forward)
                    index = (currentTabIndex + t) % tabCount;
                else
                    index = (currentTabIndex - t + tabCount) % tabCount;

                var tab = tabControl.TabPages[index];

                if (tab.Text == "+") continue;

                var editor = tab.Controls[0] as RichTextBox;
                if (editor == null) continue;

                int start;

                if (index == currentTabIndex)
                {
                    start = currentIndex;
                }
                else
                {
                    start = forward ? 0 : editor.TextLength;
                }

                int found = -1;

                if (forward)
                {
                    if (start <= editor.TextLength)
                    {
                        found = editor.Text.IndexOf(
                            lastKeyword,
                            start,
                            StringComparison.OrdinalIgnoreCase
                        );
                    }
                }
                else
                {
                    if (start > 0)
                    {
                        found = editor.Text.LastIndexOf(
                            lastKeyword,
                            start - 1,
                            StringComparison.OrdinalIgnoreCase
                        );
                    }
                }

                if (found >= 0)
                {
                    tabControl.SelectedIndex = index;

                    editor.SelectionStart = found;
                    editor.SelectionLength = lastKeyword.Length;
                    editor.ScrollToCaret();
                    editor.Focus();

                    currentTabIndex = index;

                    if (forward)
                        currentIndex = found + lastKeyword.Length;
                    else
                        currentIndex = found;

                    return;
                }
            }

            MessageBox.Show("見つかりません");
        }

        // =========================
        // ＋タブ
        // =========================
        private void AddPlusTab()
        {
            var plus = new TabPage("+");
            tabControl.TabPages.Add(plus);
        }

        private bool IsPlusTab(int index)
        {
            return tabControl.TabPages[index].Text == "+";
        }

        private void FixPlusTabPosition()
        {
            TabPage plus = null;

            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Text == "+")
                {
                    plus = tab;
                    break;
                }
            }

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
            int newHoverIndex = -1;

            for (int i = 0; i < tabControl.TabCount; i++)
            {
                var tab = tabControl.TabPages[i];

                if (tab.Tag is Rectangle rect)
                {
                    if (rect.Contains(e.Location))
                    {
                        newHoverIndex = i;
                        break;
                    }
                }
            }

            if (hoverCloseIndex != newHoverIndex)
            {
                hoverCloseIndex = newHoverIndex;
                tabControl.Invalidate(); // 再描画
            }

            // ドラッグ中
            if (isDragging && dragTabIndex >= 0)
            {
                dragGhostRect = new Rectangle(e.X - 30, e.Y - 10, 60, 20);

                for (int i = 0; i < tabControl.TabCount; i++)
                {
                    if (i == dragTabIndex) continue;
                    if (IsPlusTab(i)) continue;

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
        // 描画
        // =========================
        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; // 描画を滑らかに

            var tabRect = tabControl.GetTabRect(e.Index);
            var tab = tabControl.TabPages[e.Index];
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            // --- 色の定義 ---
            Color bgColor = isSelected ? Color.White : Color.FromArgb(240, 240, 240);
            Color textColor = isSelected ? Color.Black : Color.FromArgb(100, 100, 100);
            Color accentColor = Color.FromArgb(0, 120, 215); // 選択時のアクセントライン(青)

            // 1. タブの背景
            using (var b = new SolidBrush(bgColor))
            {
                g.FillRectangle(b, tabRect);
            }

            // 2. ＋タブの場合の特別描画
            if (tab.Text == "+")
            {
                using (var b = new SolidBrush(textColor))
                {
                    TextRenderer.DrawText(g, "＋", new Font(Font.FontFamily, 12, FontStyle.Bold), tabRect, textColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                return;
            }

            // 3. 選択状態の装飾
            if (isSelected)
            {
                // 選択タブの上部にアクセントラインを引く
                using (var p = new Pen(accentColor, 3))
                {
                    g.DrawLine(p, tabRect.Left, tabRect.Top + 1, tabRect.Right, tabRect.Top + 1);
                }
            }
            else
            {
                // 非選択タブの右側に薄い区切り線を引く
                using (var p = new Pen(Color.LightGray, 1))
                {
                    g.DrawLine(p, tabRect.Right - 1, tabRect.Top + 6, tabRect.Right - 1, tabRect.Bottom - 6);
                }
            }

            // タイトル 
            string title = tab.Text + "　";

            TextRenderer.DrawText(g, title, Font,
                new Rectangle(tabRect.X + 6, tabRect.Y + 4, tabRect.Width - 30, tabRect.Height),
                Color.Black,
                TextFormatFlags.NoPadding);

            // 5. ×ボタンの描画
            Rectangle closeRect = new Rectangle(tabRect.Right - 22, tabRect.Top + 4, 14, 14);

            if (hoverCloseIndex == e.Index)
            {
                // ホバー時は丸い赤背景に白文字
                using (var b = new SolidBrush(Color.FromArgb(232, 17, 35)))
                {
                    g.FillEllipse(b, closeRect);
                }
                TextRenderer.DrawText(g, "×", new Font(Font.FontFamily, 8, FontStyle.Bold), closeRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                // 通常時は目立たないグレー
                TextRenderer.DrawText(g, "×", new Font(Font.FontFamily, 8, FontStyle.Bold), closeRect, Color.FromArgb(170, 170, 170),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            // クリック判定用に領域を保存
            tab.Tag = closeRect;

            // 6. ドラッグ中のゴースト描画
            if (isDragging && dragGhostRect.HasValue)
            {
                using var b = new SolidBrush(Color.FromArgb(80, accentColor)); // 半透明の青
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

                // 右クリック
                if (e.Button == MouseButtons.Right &&
                    tabControl.GetTabRect(i).Contains(e.Location))
                {
                    if (IsPlusTab(i)) return;

                    rightClickTabIndex = i;
                    ShowContextMenu(e.Location);
                    return;
                }

                // ＋クリック
                if (IsPlusTab(i))
                {
                    if (tabControl.GetTabRect(i).Contains(e.Location))
                    {
                        AddNewTab();
                        return;
                    }
                }

                // ×クリック
                if (tab.Tag is Rectangle rect)
                {
                    if (rect.Contains(e.Location))
                    {
                        CloseTab(i);
                        return;
                    }
                }

                // ドラッグ開始
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
                string input = Microsoft.VisualBasic.Interaction.InputBox(
                    "タブ名を入力", "名前変更", tab.Text);

                if (!string.IsNullOrWhiteSpace(input))
                    tab.Text = input;
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

        // ダブルクリックで名前変更
        private void TabControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl.TabCount; i++)
            {
                if (IsPlusTab(i)) continue;

                if (tabControl.GetTabRect(i).Contains(e.Location))
                {
                    string current = tabControl.TabPages[i].Text;

                    string input = Microsoft.VisualBasic.Interaction.InputBox(
                        "タブ名を入力",
                        "名前変更",
                        current);

                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        tabControl.TabPages[i].Text = input;
                    }
                    break;
                }
            }
        }

        // ＋タブ選択防止
        private void TabControl_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage.Text == "+")
                e.Cancel = true;
        }

        // =========================
        // エディタ
        // =========================
        private RichTextBox CreateEditor()
        {
            var editor = new RichTextBox();
            editor.Dock = DockStyle.Fill;
            editor.Font = new Font("Meiryo", 11);
            editor.AllowDrop = true;

            editor.KeyDown += Editor_KeyDown;
            editor.DragEnter += Editor_DragEnter;
            editor.DragDrop += Editor_DragDrop;

            return editor;
        }

        private RichTextBox GetEditor()
        {
            return tabControl.SelectedTab?.Controls.Count > 0
                ? tabControl.SelectedTab.Controls[0] as RichTextBox
                : null;
        }

        // =========================
        // キー操作
        // =========================
        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.T)
            {
                AddNewTab();
                e.SuppressKeyPress = true;
            }

            if (e.Control && e.KeyCode == Keys.W)
            {
                CloseTab(tabControl.SelectedIndex);
                e.SuppressKeyPress = true;
            }

            if (e.Control && e.KeyCode == Keys.V)
            {
                PasteClipboard();
                e.SuppressKeyPress = true;
            }

            if (e.Control && e.KeyCode == Keys.S)
            {
                SaveAsImage();
                e.SuppressKeyPress = true;
            }
        }

        // =========================
        // クリップボード貼り付け
        // =========================
        private void PasteClipboard()
        {
            var editor = GetEditor();
            if (editor == null) return;

            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                if (img == null) return;

                InsertImage(editor, new Bitmap(img));
            }
            else if (Clipboard.ContainsText())
            {
                editor.Paste();
            }
        }

        // =========================
        // Drag & Drop
        // =========================
        private void Editor_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void Editor_DragDrop(object sender, DragEventArgs e)
        {
            var editor = GetEditor();
            if (editor == null) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var file in files)
            {
                try
                {
                    using var img = Image.FromFile(file);
                    InsertImage(editor, new Bitmap(img));
                    continue;
                }
                catch { }

                try
                {
                    editor.AppendText(File.ReadAllText(file));
                }
                catch { }
            }
        }

        // =========================
        // 画像挿入
        // =========================
        private void InsertImage(RichTextBox editor, Image img)
        {
            if (editor == null || img == null) return;
            // クリップボード退避
            var backup = Clipboard.GetDataObject();

            try
            {
                Clipboard.SetImage(img);
                editor.Paste();
            }
            finally
            {
                try
                {
                    if (backup != null)
                        Clipboard.SetDataObject(backup);
                }
                catch { }
            }
        }

        // =========================
        // 画像として保存
        // =========================
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
        // JSON保存
        // =========================
        private void SaveToJson()
        {
            var list = new List<TabData>();

            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Text == "+") continue;

                var editor = tab.Controls[0] as RichTextBox;

                list.Add(new TabData
                {
                    Title = tab.Text,
                    Rtf = editor.Rtf
                });
            }

            File.WriteAllText(savePath, JsonSerializer.Serialize(list));
        }

        private void LoadFromJson()
        {
            if (!File.Exists(savePath)) return;

            var list = JsonSerializer.Deserialize<List<TabData>>(File.ReadAllText(savePath));

            foreach (var item in list)
            {
                var editor = CreateEditor();
                editor.Rtf = item.Rtf;

                var tab = new TabPage(item.Title);
                tab.Controls.Add(editor);

                tabControl.TabPages.Add(tab);
            }
        }

        private static Icon LoadIcon(string resourceName)
        {
            var assembly = typeof(MainForm).Assembly;

            // リソース名の確認
            string fullName = assembly.GetManifestResourceNames()
                                      .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
            if (fullName != null)
            {
                using Stream stream = assembly.GetManifestResourceStream(fullName);
                if (stream != null)
                    return new Icon(stream);
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

        // 追加：空き番号取得
        private int GetNextTabNumber()
        {
            var used = new HashSet<int>();

            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Text.StartsWith("Tab "))
                {
                    if (int.TryParse(tab.Text.Replace("Tab ", ""), out int num))
                        used.Add(num);
                }
            }

            for (int i = 1; i <= MAX_TABS; i++)
            {
                if (!used.Contains(i))
                    return i;
            }

            return -1;
        }

        // =========================
        // 起動
        // =========================
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