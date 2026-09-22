using System.Drawing.Imaging;
using System.Text.Json;
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

            Color bgColor = isSelected
                ? Color.White
                : (isHovered
                    ? Color.FromArgb(235, 235, 235)
                    : Color.FromArgb(245, 245, 245));

            Color textColor = Color.Black;

            Color accentColor =
                Color.FromArgb(0, 120, 215);

            // =========================
            // タブ背景
            // =========================

            using (var b = new SolidBrush(bgColor))
            {
                g.FillRectangle(b, tabRect);
            }

            // =========================
            // ＋タブ
            // =========================

            if (tab.Text == "+")
            {
                using (var font = new Font(
                    Font.FontFamily,
                    12,
                    FontStyle.Bold))
                {
                    TextRenderer.DrawText(
                        g,
                        "＋",
                        font,
                        tabRect,
                        textColor,
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter
                    );
                }

                return;
            }

            // =========================
            // 選択状態
            // =========================

            if (isSelected)
            {
                using (var p = new Pen(
                    accentColor,
                    3))
                {
                    g.DrawLine(
                        p,
                        tabRect.Left,
                        tabRect.Top + 1,
                        tabRect.Right,
                        tabRect.Top + 1
                    );
                }
            }
            else
            {
                using (var p = new Pen(
                    Color.FromArgb(220, 220, 220),
                    1))
                {
                    g.DrawLine(
                        p,
                        tabRect.Right - 1,
                        tabRect.Top + 5,
                        tabRect.Right - 1,
                        tabRect.Bottom - 5
                    );
                }
            }

            // =========================
            // タイトル
            // =========================

            string title = tab.Text;

            TextRenderer.DrawText(
                g,
                title,
                Font,
                new Rectangle(
                    tabRect.X + 4,
                    tabRect.Y + 1,
                    tabRect.Width - 22,
                    tabRect.Height
                ),
                textColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis
            );

            // =========================
            // ×ボタン
            // =========================

            Rectangle closeRect = new Rectangle(
                tabRect.Right - 18,
                tabRect.Top + 5,
                14,
                14
            );

            if (hoverCloseIndex == e.Index)
            {
                using (var b = new SolidBrush(
                    Color.FromArgb(232, 17, 35)))
                {
                    g.FillEllipse(b, closeRect);
                }

                using (var font = new Font(
                    Font.FontFamily,
                    8,
                    FontStyle.Bold))
                {
                    TextRenderer.DrawText(
                        g,
                        "×",
                        font,
                        closeRect,
                        Color.White,
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding
                    );
                }
            }
            else
            {
                using (var font = new Font(
                    Font.FontFamily,
                    8,
                    FontStyle.Bold))
                {
                    TextRenderer.DrawText(
                        g,
                        "×",
                        font,
                        closeRect,
                        Color.FromArgb(100, 100, 100),
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding
                    );
                }
            }

            tab.Tag = closeRect;

            // =========================
            // ドラッグゴースト
            // =========================

            if (isDragging && dragGhostRect.HasValue)
            {
                using var b = new SolidBrush(
                    Color.FromArgb(60, accentColor));

                g.FillRectangle(
                    b,
                    dragGhostRect.Value
                );
            }
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