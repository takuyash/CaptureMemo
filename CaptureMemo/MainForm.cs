using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace AlwaysOnTopMemo
{
    public class MainForm : Form
    {
        private RichTextBox editor;

        private int maxImageWidth = 0;
        private int maxImageHeight = 0;

        public MainForm()
        {
            Text = "CaptureMemo";
            Width = 400;
            Height = 500;

            editor = new RichTextBox();
            editor.Dock = DockStyle.Fill;
            editor.Font = new Font("Meiryo", 11);
            editor.AllowDrop = true;

            Controls.Add(editor);

            editor.KeyDown += Editor_KeyDown;
            editor.DragEnter += Editor_DragEnter;
            editor.DragDrop += Editor_DragDrop;

            this.Shown += (s, e) =>
            {
                TopMost = true;
            };
        }

        // =========================
        // Ctrl操作
        // =========================
        private void Editor_KeyDown(object? sender, KeyEventArgs e)
        {
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
            if (Clipboard.ContainsImage())
            {
                Image? img = Clipboard.GetImage();
                if (img == null) return;

                InsertImage(img);
            }
            else if (Clipboard.ContainsText())
            {
                editor.Paste();
            }
        }

        // =========================
        // Drag & Drop
        // =========================
        private void Editor_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data!.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void Editor_DragDrop(object? sender, DragEventArgs e)
        {
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var file in files)
            {
                if (!File.Exists(file)) continue;

                string ext = Path.GetExtension(file).ToLower();

                // 画像
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
                {
                    using (Image img = Image.FromFile(file))
                    {
                        InsertImage((Image)img.Clone());
                    }
                }
                // テキスト系
                else if (ext == ".txt" || ext == ".log" || ext == ".csv" || ext == ".json")
                {
                    string text = File.ReadAllText(file);
                    editor.AppendText(text + Environment.NewLine);
                }
            }
        }

        // =========================
        // 画像挿入（共通化）
        // =========================
        private void InsertImage(Image img)
        {
            // サイズ制限（でかすぎ防止）
            img = ResizeImage(img, 800);

            maxImageWidth = Math.Max(maxImageWidth, img.Width);
            maxImageHeight = Math.Max(maxImageHeight, img.Height);

            ResizeWindow();

            Clipboard.SetImage(img);
            editor.Paste();
        }

        // =========================
        // 画像リサイズ
        // =========================
        private Image ResizeImage(Image img, int maxWidth)
        {
            if (img.Width <= maxWidth)
                return img;

            int newHeight = img.Height * maxWidth / img.Width;
            return new Bitmap(img, new Size(maxWidth, newHeight));
        }

        // =========================
        // ウィンドウサイズ調整
        // =========================
        private void ResizeWindow()
        {
            int margin = 60;

            int newWidth = maxImageWidth + margin;
            int newHeight = maxImageHeight + margin;

            Width = Math.Max(Width, newWidth);
            Height = Math.Max(Height, newHeight);
        }

        // =========================
        // 画像として保存
        // =========================
        private void SaveAsImage()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "PNG Image|*.png";
                dialog.FileName = "memo.png";

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                Bitmap bmp = new Bitmap(editor.Width, editor.Height);

                editor.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));

                bmp.Save(dialog.FileName, ImageFormat.Png);
            }
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