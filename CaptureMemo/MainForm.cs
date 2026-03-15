using System;
using System.Drawing;
using System.Drawing.Imaging;
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

            TopMost = true;

            editor = new RichTextBox();
            editor.Dock = DockStyle.Fill;
            editor.Font = new Font("Meiryo", 11);

            Controls.Add(editor);

            editor.KeyDown += Editor_KeyDown;
        }

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

        private void PasteClipboard()
        {
            if (Clipboard.ContainsImage())
            {
                Image? img = Clipboard.GetImage();

                if (img == null)
                    return;

                maxImageWidth = Math.Max(maxImageWidth, img.Width);
                maxImageHeight = Math.Max(maxImageHeight, img.Height);

                ResizeWindow();

                Clipboard.SetImage(img);
                editor.Paste();
            }
            else if (Clipboard.ContainsText())
            {
                editor.Paste();
            }
        }

        private void ResizeWindow()
        {
            int margin = 60;

            int newWidth = maxImageWidth + margin;
            int newHeight = maxImageHeight + margin;

            Width = Math.Max(Width, newWidth);
            Height = Math.Max(Height, newHeight);
        }

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

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }
}