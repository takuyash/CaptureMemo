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

            Shown += (s, e) => { TopMost = true; };
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
                if (img != null)
                {
                    InsertImage(new Bitmap(img));
                }
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
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void Editor_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var file in files)
            {
                if (!File.Exists(file)) continue;

                // ① 画像として読む（安全版）
                try
                {
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                    using (var img = Image.FromStream(fs))
                    {
                        InsertImage(new Bitmap(img));
                        continue;
                    }
                }
                catch
                {
                    // 画像じゃない
                }

                // ② テキストとして読む（サイズ制限あり）
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length < 1024 * 1024) // 1MB制限
                    {
                        string text = File.ReadAllText(file);
                        editor.AppendText(text + Environment.NewLine);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        // =========================
        // 画像挿入（安全版）
        // =========================
        private void InsertImage(Image img)
        {
            img = ResizeImage(img, 800);

            // クリップボード退避
            IDataObject backup = Clipboard.GetDataObject();

            try
            {
                Clipboard.SetImage(img);
                editor.Paste();
            }
            finally
            {
                if (backup != null)
                    Clipboard.SetDataObject(backup);
            }
        }

        // =========================
        // 画像リサイズ
        // =========================
        private Image ResizeImage(Image img, int maxWidth)
        {
            if (img.Width <= maxWidth)
                return img;

            int newHeight = img.Height * maxWidth / img.Width;

            var resized = new Bitmap(img, new Size(maxWidth, newHeight));
            img.Dispose();

            return resized;
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