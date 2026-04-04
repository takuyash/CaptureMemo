using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

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

        public MainForm()
        {
            AppIcon = LoadIcon("icon.ico");
            this.Icon = AppIcon;

            Text = "CaptureMemo";
            Width = 400;
            Height = 600;

            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;

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

            Controls.Add(tabControl);

            LoadFromJson();

            if (tabControl.TabCount == 0)
                AddNewTab();

            AddPlusTab();
            FixPlusTabPosition();

            FormClosing += (s, e) => SaveToJson();
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


            if (isDragging && dragTabIndex >= 0)
            {
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
            }
        }

        // =========================
        // 描画
        // =========================
        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var g = e.Graphics;
            var tabRect = tabControl.GetTabRect(e.Index);
            var tab = tabControl.TabPages[e.Index];

            // ＋タブ
            if (tab.Text == "+")
            {
                g.FillRectangle(Brushes.LightBlue, tabRect);
                TextRenderer.DrawText(g, "+", Font, tabRect, Color.Black,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // 通常タブ
            g.FillRectangle(Brushes.LightGray, tabRect);

            // タイトル
            TextRenderer.DrawText(g, tab.Text, Font,
                new Rectangle(tabRect.X + 5, tabRect.Y + 4, tabRect.Width - 20, tabRect.Height),
                Color.Black);

            // ×ボタン
            Rectangle closeRect = new Rectangle(
                tabRect.Right - 18,
                tabRect.Top + 4,
                14,
                14);

            // ホバー時の背景
            if (hoverCloseIndex == e.Index)
            {
                g.FillRectangle(Brushes.IndianRed, closeRect);
                TextRenderer.DrawText(g, "×", Font, closeRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                TextRenderer.DrawText(g, "×", Font, closeRect, Color.Black,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            tab.Tag = closeRect;
        }

        // =========================
        // クリック処理
        // =========================
        private void TabControl_MouseDown(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl.TabCount; i++)
            {
                var tab = tabControl.TabPages[i];

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

        // ドラッグ終了
        private void TabControl_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
            dragTabIndex = -1;
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

            Bitmap bmp = new Bitmap(editor.Width, editor.Height);
            editor.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(dlg.FileName, ImageFormat.Png);
        }

        // =========================
        // JSON保存
        // =========================
        private void SaveToJson()
        {
            var list = new List<string>();

            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Text == "+") continue;

                var editor = tab.Controls[0] as RichTextBox;
                list.Add(editor.Rtf);
            }

            File.WriteAllText(savePath, JsonSerializer.Serialize(list));
        }

        private void LoadFromJson()
        {
            if (!File.Exists(savePath)) return;

            var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(savePath));

            foreach (var rtf in list)
            {
                int nextNo = GetNextTabNumber();
                if (nextNo == -1) break;

                var editor = CreateEditor();
                editor.Rtf = rtf;

                var tab = new TabPage($"Tab {nextNo}");
                tab.Controls.Add(editor);

                tabControl.TabPages.Add(tab);
            }
        }

        private static Icon LoadIcon(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            if (File.Exists(path))
            {
                try { return new Icon(path); } catch { }
            }
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
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
}