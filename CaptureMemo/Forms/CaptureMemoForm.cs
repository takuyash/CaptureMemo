using System.Drawing.Imaging;
using System.Text.Json;
using CaptureMemo.Models;
using Timer = System.Windows.Forms.Timer;

namespace CaptureMemo
{
    public partial class CaptureMemoForm : Form
    {
        private int hoverCloseIndex = -1;
        private int hoverTabIndex = -1;

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

        private string lastKeyword = "";
        private int currentTabIndex = 0;
        private int currentIndex = 0;

        public CaptureMemoForm()
        {
            InitializeComponent();

            // =========================
            // アイコン
            // =========================

            AppIcon = LoadIcon("icon.ico");
            Icon = AppIcon;

            // =========================
            // フォーム設定
            // =========================

            TopMost = true;

            Activated += (s, e) => TopMost = true;
            Deactivate += (s, e) => TopMost = true;

            // =========================
            // TabControlイベント
            // =========================

            tabControl.DrawItem += TabControl_DrawItem;
            tabControl.MouseDown += TabControl_MouseDown;
            tabControl.Selecting += TabControl_Selecting;
            tabControl.MouseMove += TabControl_MouseMove;
            tabControl.MouseUp += TabControl_MouseUp;
            tabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
            tabControl.MouseLeave += TabControl_MouseLeave;

            // =========================
            // キーボード
            // =========================

            KeyDown += MainForm_KeyDown;

            // =========================
            // 検索UIイベント
            // =========================

            searchBox.KeyDown += SearchBox_KeyDown;

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

            btnClose.Click += (s, e) =>
            {
                searchPanel.Visible = false;
            };

            // =========================
            // データ読み込み
            // =========================

            LoadFromJson();

            if (tabControl.TabCount == 0)
                AddNewTab();

            AddPlusTab();
            FixPlusTabPosition();

            // =========================
            // 5秒ごとに自動保存
            // =========================

            var timer = new Timer();
            timer.Interval = 5000;
            timer.Tick += (s, e) => SaveToJson();
            timer.Start();

            // =========================
            // 終了時保存
            // =========================

            FormClosing += (s, e) => SaveToJson();
        }

        // =========================
        // 検索
        // =========================

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                searchPanel.Visible = false;
            }
        }

        private void StartSearch(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return;

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
            if (string.IsNullOrEmpty(lastKeyword))
                return;

            int tabCount = tabControl.TabCount;

            for (int t = 0; t < tabCount; t++)
            {
                int index = forward
                    ? (currentTabIndex + t) % tabCount
                    : (currentTabIndex - t + tabCount) % tabCount;

                var tab = tabControl.TabPages[index];

                if (tab.Text == "+")
                    continue;

                var editor = tab.Controls[0] as RichTextBox;

                if (editor == null)
                    continue;

                int start = (index == currentTabIndex)
                    ? currentIndex
                    : (forward ? 0 : editor.TextLength);

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

                    currentIndex = forward
                        ? found + lastKeyword.Length
                        : found;

                    return;
                }
            }

            MessageBox.Show("見つかりません");
        }

        // =========================
        // タブ管理
        // =========================

        private void AddPlusTab()
        {
            tabControl.TabPages.Add(new TabPage("+"));
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

            if (plus == null)
                return;

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

            if (nextNo == -1)
                return;

            var editor = CreateEditor();

            var tab = new TabPage($"Tab {nextNo}");

            tab.BackColor = Color.White;
            tab.Controls.Add(editor);

            tabControl.TabPages.Add(tab);

            FixPlusTabPosition();

            tabControl.SelectedTab = tab;
        }

        private void CloseTab(int index)
        {
            if (tabControl.TabCount <= 2)
                return;

            tabControl.TabPages.RemoveAt(index);

            FixPlusTabPosition();
        }

        // =========================
        // タブマウス移動
        // =========================

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

            // =========================
            // タブホバー
            // =========================

            if (hoverTabIndex != newHoverTabIndex)
            {
                if (hoverTabIndex >= 0 &&
                    hoverTabIndex < tabControl.TabCount)
                {
                    tabControl.Invalidate(
                        tabControl.GetTabRect(hoverTabIndex)
                    );
                }

                if (newHoverTabIndex >= 0 &&
                    newHoverTabIndex < tabControl.TabCount)
                {
                    tabControl.Invalidate(
                        tabControl.GetTabRect(newHoverTabIndex)
                    );
                }

                hoverTabIndex = newHoverTabIndex;
            }

            // =========================
            // ×ボタンホバー
            // =========================

            if (hoverCloseIndex != newHoverCloseIndex)
            {
                if (hoverCloseIndex >= 0 &&
                    hoverCloseIndex < tabControl.TabCount)
                {
                    tabControl.Invalidate(
                        tabControl.GetTabRect(hoverCloseIndex)
                    );
                }

                if (newHoverCloseIndex >= 0 &&
                    newHoverCloseIndex < tabControl.TabCount)
                {
                    tabControl.Invalidate(
                        tabControl.GetTabRect(newHoverCloseIndex)
                    );
                }

                hoverCloseIndex = newHoverCloseIndex;
            }

            // =========================
            // ドラッグ中
            // =========================

            if (isDragging && dragTabIndex >= 0)
            {
                dragGhostRect = new Rectangle(
                    e.X - 30,
                    e.Y - 10,
                    60,
                    24
                );

                for (int i = 0; i < tabControl.TabCount; i++)
                {
                    if (i == dragTabIndex || IsPlusTab(i))
                        continue;

                    var rect = tabControl.GetTabRect(i);

                    if (rect.Contains(e.Location))
                    {
                        var dragged =
                            tabControl.TabPages[dragTabIndex];

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
        // タブマウス離脱
        // =========================

        private void TabControl_MouseLeave(object sender, EventArgs e)
        {
            if (hoverTabIndex >= 0 &&
                hoverTabIndex < tabControl.TabCount)
            {
                tabControl.Invalidate(
                    tabControl.GetTabRect(hoverTabIndex)
                );
            }

            if (hoverCloseIndex >= 0 &&
                hoverCloseIndex < tabControl.TabCount)
            {
                tabControl.Invalidate(
                    tabControl.GetTabRect(hoverCloseIndex)
                );
            }

            hoverCloseIndex = -1;
            hoverTabIndex = -1;
        }

        // =========================
        // タブ描画
        // =========================

        private void TabControl_DrawItem(
            object sender,
            DrawItemEventArgs e)
        {
            var g = e.Graphics;

            g.SmoothingMode =
                System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var tabRect = tabControl.GetTabRect(e.Index);
            var tab = tabControl.TabPages[e.Index];

            bool isSelected =
                (e.State & DrawItemState.Selected)
                == DrawItemState.Selected;

            bool isHovered =
                hoverTabIndex == e.Index;

            // =========================
            // 色
            // =========================

            Color accentColor =
                Color.FromArgb(37, 99, 235);

            Color hoverColor =
                Color.FromArgb(226, 228, 234);

            Color textColor = isSelected
                ? Color.FromArgb(28, 30, 36)
                : Color.FromArgb(105, 110, 122);

            // 少し内側に寄せてタブ同士に隙間を作る
            var card = new Rectangle(
                tabRect.X + 2,
                tabRect.Y + 3,
                tabRect.Width - 4,
                tabRect.Height - 3
            );

            // =========================
            // ＋タブ
            // =========================

            if (tab.Text == "+")
            {
                var plusRect = new Rectangle(
                    tabRect.X + (tabRect.Width - 22) / 2,
                    tabRect.Y + (tabRect.Height - 22) / 2 + 1,
                    22,
                    22
                );

                if (isHovered)
                {
                    using var path = RoundedRect(plusRect, 6);
                    using var b = new SolidBrush(hoverColor);
                    g.FillPath(b, path);
                }

                DrawPlus(
                    g,
                    plusRect,
                    isHovered
                        ? accentColor
                        : Color.FromArgb(105, 110, 122));

                return;
            }

            // =========================
            // タブ背景
            // =========================

            if (isSelected)
            {
                // 上だけ角丸のカードでページと一体化させる
                using (var path = RoundedTopRect(card, 7))
                using (var b = new SolidBrush(Color.White))
                {
                    g.FillPath(b, path);
                }

                using (var path = RoundedTopRect(card, 7))
                using (var p = new Pen(Color.FromArgb(225, 227, 232)))
                {
                    g.DrawPath(p, path);
                }

                // ページとの境目の線を消す
                using (var b = new SolidBrush(Color.White))
                {
                    g.FillRectangle(
                        b,
                        card.X + 1,
                        card.Bottom - 2,
                        card.Width - 1,
                        3);
                }

                // 上部のアクセントバー
                using (var path = RoundedRect(
                    new Rectangle(
                        card.X + 8,
                        card.Y,
                        card.Width - 16,
                        3),
                    1))
                using (var b = new SolidBrush(accentColor))
                {
                    g.FillPath(b, path);
                }
            }
            else if (isHovered)
            {
                var pill = new Rectangle(
                    card.X,
                    card.Y + 1,
                    card.Width,
                    card.Height - 5
                );

                using var path = RoundedRect(pill, 6);
                using var b = new SolidBrush(hoverColor);
                g.FillPath(b, path);
            }

            // =========================
            // タイトル
            // =========================

            bool showClose = isSelected || isHovered;

            using (var titleFont = new Font(
                tabControl.Font,
                isSelected ? FontStyle.Bold : FontStyle.Regular))
            {
                TextRenderer.DrawText(
                    g,
                    tab.Text,
                    titleFont,
                    new Rectangle(
                        card.X + 8,
                        card.Y + 1,
                        card.Width - (showClose ? 28 : 14),
                        card.Height - 2
                    ),
                    textColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix
                );
            }

            // =========================
            // ×ボタン
            // =========================

            Rectangle closeRect = new Rectangle(
                card.Right - 22,
                card.Y + (card.Height - 18) / 2 + 1,
                18,
                18
            );

            if (showClose)
            {
                bool isCloseHovered =
                    hoverCloseIndex == e.Index;

                if (isCloseHovered)
                {
                    using var path = RoundedRect(closeRect, 5);
                    using var b = new SolidBrush(
                        Color.FromArgb(254, 226, 226));
                    g.FillPath(b, path);
                }

                DrawCross(
                    g,
                    closeRect,
                    isCloseHovered
                        ? Color.FromArgb(220, 38, 38)
                        : Color.FromArgb(120, 125, 136));
            }

            tab.Tag = closeRect;

            // =========================
            // ドラッグゴースト（最後に1回だけ描く）
            // =========================

            if (isDragging &&
                dragGhostRect.HasValue &&
                e.Index == tabControl.TabCount - 1)
            {
                using var path = RoundedRect(dragGhostRect.Value, 6);

                using (var b = new SolidBrush(
                    Color.FromArgb(50, accentColor)))
                {
                    g.FillPath(b, path);
                }

                using (var p = new Pen(
                    Color.FromArgb(140, accentColor)))
                {
                    g.DrawPath(p, path);
                }
            }
        }

        // =========================
        // タブ描画ヘルパー
        // =========================

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(
            Rectangle r,
            int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedTopRect(
            Rectangle r,
            int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;

            path.AddLine(r.X, r.Bottom, r.X, r.Y + radius);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddLine(r.Right, r.Y + radius, r.Right, r.Bottom);

            return path;
        }

        private static void DrawCross(
            Graphics g,
            Rectangle r,
            Color color)
        {
            const int inset = 5;

            using var p = new Pen(color, 1.6f);
            p.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            p.EndCap = System.Drawing.Drawing2D.LineCap.Round;

            g.DrawLine(p, r.Left + inset, r.Top + inset, r.Right - inset, r.Bottom - inset);
            g.DrawLine(p, r.Right - inset, r.Top + inset, r.Left + inset, r.Bottom - inset);
        }

        private static void DrawPlus(
            Graphics g,
            Rectangle r,
            Color color)
        {
            const int inset = 6;

            using var p = new Pen(color, 1.8f);
            p.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            p.EndCap = System.Drawing.Drawing2D.LineCap.Round;

            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;

            g.DrawLine(p, cx, r.Top + inset, cx, r.Bottom - inset);
            g.DrawLine(p, r.Left + inset, cy, r.Right - inset, cy);
        }

        // =========================
        // タブクリック
        // =========================

        private void TabControl_MouseDown(
            object sender,
            MouseEventArgs e)
        {
            for (int i = 0;
                 i < tabControl.TabCount;
                 i++)
            {
                var tab = tabControl.TabPages[i];

                // =========================
                // 右クリック
                // =========================

                if (e.Button == MouseButtons.Right &&
                    tabControl.GetTabRect(i).Contains(e.Location))
                {
                    if (IsPlusTab(i))
                        return;

                    rightClickTabIndex = i;

                    ShowContextMenu(e.Location);

                    return;
                }

                // =========================
                // ＋タブ
                // =========================

                if (IsPlusTab(i) &&
                    tabControl.GetTabRect(i).Contains(e.Location))
                {
                    AddNewTab();
                    return;
                }

                // =========================
                // ×ボタン
                // =========================

                if (tab.Tag is Rectangle rect &&
                    rect.Contains(e.Location))
                {
                    CloseTab(i);
                    return;
                }

                // =========================
                // ドラッグ開始
                // =========================

                if (tabControl.GetTabRect(i).Contains(e.Location))
                {
                    dragTabIndex = i;
                    isDragging = true;
                }
            }
        }

        // =========================
        // コンテキストメニュー
        // =========================

        private void ShowContextMenu(Point location)
        {
            var menu = new ContextMenuStrip();

            menu.RenderMode =
                ToolStripRenderMode.Professional;

            // =========================
            // 削除
            // =========================

            menu.Items.Add(
                "削除",
                null,
                (s, e) =>
                {
                    CloseTab(rightClickTabIndex);
                });

            // =========================
            // 複製
            // =========================

            menu.Items.Add(
                "複製",
                null,
                (s, e) =>
                {
                    var src =
                        tabControl.TabPages[
                            rightClickTabIndex];

                    var editor = CreateEditor();

                    editor.Rtf =
                        ((RichTextBox)src.Controls[0]).Rtf;

                    var tab =
                        new TabPage(src.Text + "_copy");

                    tab.Controls.Add(editor);

                    tabControl.TabPages.Insert(
                        rightClickTabIndex + 1,
                        tab);

                    FixPlusTabPosition();
                });

            // =========================
            // 名前変更
            // =========================

            menu.Items.Add(
                "名前変更",
                null,
                (s, e) =>
                {
                    var tab =
                        tabControl.TabPages[
                            rightClickTabIndex];

                    string input =
                        Microsoft.VisualBasic.Interaction.InputBox(
                            "タブ名を入力",
                            "名前変更",
                            tab.Text);

                    if (!string.IsNullOrWhiteSpace(input))
                        tab.Text = input;
                });

            menu.Show(tabControl, location);
        }

        // =========================
        // タブマウスアップ
        // =========================

        private void TabControl_MouseUp(
            object sender,
            MouseEventArgs e)
        {
            isDragging = false;
            dragTabIndex = -1;
            dragGhostRect = null;

            tabControl.Invalidate();
        }

        // =========================
        // タブダブルクリック
        // =========================

        private void TabControl_MouseDoubleClick(
            object sender,
            MouseEventArgs e)
        {
            for (int i = 0;
                 i < tabControl.TabCount;
                 i++)
            {
                if (IsPlusTab(i))
                    continue;

                if (tabControl.GetTabRect(i).Contains(e.Location))
                {
                    string input =
                        Microsoft.VisualBasic.Interaction.InputBox(
                            "タブ名を入力",
                            "名前変更",
                            tabControl.TabPages[i].Text);

                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        tabControl.TabPages[i].Text = input;
                    }

                    break;
                }
            }
        }

        // =========================
        // タブ選択
        // =========================

        private void TabControl_Selecting(
            object sender,
            TabControlCancelEventArgs e)
        {
            if (e.TabPage != null &&
                e.TabPage.Text == "+")
            {
                e.Cancel = true;
            }
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

        private RichTextBox GetEditor()
        {
            return tabControl.SelectedTab?.Controls.Count > 0
                ? tabControl.SelectedTab.Controls[0]
                    as RichTextBox
                : null;
        }

        // =========================
        // エディタ キー操作
        // =========================

        private void Editor_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Control &&
                e.KeyCode == Keys.T)
            {
                AddNewTab();
                e.SuppressKeyPress = true;
            }

            if (e.Control &&
                e.KeyCode == Keys.W)
            {
                CloseTab(tabControl.SelectedIndex);
                e.SuppressKeyPress = true;
            }

            if (e.Control &&
                e.KeyCode == Keys.V)
            {
                PasteClipboard();
                e.SuppressKeyPress = true;
            }

            if (e.Control &&
                e.KeyCode == Keys.S)
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

            if (editor == null)
                return;

            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();

                if (img != null)
                {
                    InsertImage(
                        editor,
                        new Bitmap(img));
                }
            }
            else if (Clipboard.ContainsText())
            {
                editor.Paste();
            }
        }

        // =========================
        // ドラッグ開始
        // =========================

        private void Editor_DragEnter(
            object sender,
            DragEventArgs e)
        {
            if (e.Data.GetDataPresent(
                DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        // =========================
        // ドラッグ＆ドロップ
        // =========================

        private void Editor_DragDrop(
            object sender,
            DragEventArgs e)
        {
            var editor = GetEditor();

            if (editor == null)
                return;

            var files =
                (string[])e.Data.GetData(
                    DataFormats.FileDrop);

            foreach (var file in files)
            {
                try
                {
                    using var img =
                        Image.FromFile(file);

                    InsertImage(
                        editor,
                        new Bitmap(img));

                    continue;
                }
                catch
                {
                }

                try
                {
                    editor.AppendText(
                        File.ReadAllText(file));
                }
                catch
                {
                }
            }
        }

        // =========================
        // 画像挿入
        // =========================

        private void InsertImage(
            RichTextBox editor,
            Image img)
        {
            if (editor == null ||
                img == null)
            {
                return;
            }

            var backup =
                Clipboard.GetDataObject();

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
                    {
                        Clipboard.SetDataObject(
                            backup);
                    }
                }
                catch
                {
                }
            }
        }

        // =========================
        // 画像として保存
        // =========================

        private void SaveAsImage()
        {
            var editor = GetEditor();

            if (editor == null)
                return;

            using var dlg =
                new SaveFileDialog();

            dlg.Filter = "PNG|*.png";

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            int width = editor.Width;
            int height = editor.Height;

            int totalHeight =
                editor.GetPositionFromCharIndex(
                    editor.TextLength).Y + height;

            Bitmap finalBmp =
                new Bitmap(
                    width,
                    totalHeight);

            using (Graphics g =
                Graphics.FromImage(finalBmp))
            {
                int offset = 0;

                while (offset < totalHeight)
                {
                    editor.AutoScrollOffset =
                        new Point(0, offset);

                    Bitmap tmp =
                        new Bitmap(
                            width,
                            height);

                    editor.DrawToBitmap(
                        tmp,
                        new Rectangle(
                            0,
                            0,
                            width,
                            height));

                    g.DrawImage(
                        tmp,
                        0,
                        offset);

                    tmp.Dispose();

                    offset += height;
                }
            }

            finalBmp.Save(
                dlg.FileName,
                ImageFormat.Png);

            finalBmp.Dispose();
        }

        // =========================
        // JSON保存
        // =========================

        private void SaveToJson()
        {
            var list =
                new List<TabData>();

            foreach (TabPage tab
                in tabControl.TabPages)
            {
                if (tab.Text == "+")
                    continue;

                var editor =
                    tab.Controls[0]
                    as RichTextBox;

                list.Add(
                    new TabData
                    {
                        Title = tab.Text,
                        Rtf = editor.Rtf
                    });
            }

            File.WriteAllText(
                savePath,
                JsonSerializer.Serialize(list));
        }

        // =========================
        // JSON読み込み
        // =========================

        private void LoadFromJson()
        {
            if (!File.Exists(savePath))
                return;

            try
            {
                var list =
                    JsonSerializer.Deserialize<List<TabData>>(
                        File.ReadAllText(savePath));

                foreach (var item in list)
                {
                    var editor =
                        CreateEditor();

                    editor.Rtf =
                        item.Rtf;

                    var tab =
                        new TabPage(item.Title);

                    tab.BackColor =
                        Color.White;

                    tab.Controls.Add(editor);

                    tabControl.TabPages.Add(tab);
                }
            }
            catch
            {
            }
        }

        // =========================
        // アイコン読み込み
        // =========================

        private static Icon LoadIcon(
            string resourceName)
        {
            var assembly =
                typeof(CaptureMemoForm).Assembly;

            string fullName =
                assembly
                    .GetManifestResourceNames()
                    .FirstOrDefault(
                        n => n.EndsWith(
                            resourceName,
                            StringComparison.OrdinalIgnoreCase));

            if (fullName != null)
            {
                using Stream stream =
                    assembly.GetManifestResourceStream(
                        fullName);

                if (stream != null)
                {
                    return new Icon(stream);
                }
            }

            return SystemIcons.Application;
        }

        // =========================
        // フォーム キー操作
        // =========================

        private void MainForm_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Control &&
                e.KeyCode == Keys.F)
            {
                searchPanel.Visible = true;

                searchBox.Focus();
                searchBox.SelectAll();

                e.SuppressKeyPress = true;
            }
        }

        // =========================
        // タブ番号取得
        // =========================

        private int GetNextTabNumber()
        {
            var used =
                new HashSet<int>();

            foreach (TabPage tab
                in tabControl.TabPages)
            {
                if (tab.Text.StartsWith("Tab ") &&
                    int.TryParse(
                        tab.Text.Replace(
                            "Tab ",
                            ""),
                        out int num))
                {
                    used.Add(num);
                }
            }

            for (int i = 1;
                 i <= MAX_TABS;
                 i++)
            {
                if (!used.Contains(i))
                    return i;
            }

            return -1;
        }
    }
}