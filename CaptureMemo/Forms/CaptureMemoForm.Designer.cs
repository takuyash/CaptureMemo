using CaptureMemo.Helpers;

namespace CaptureMemo
{
    partial class CaptureMemoForm
    {
        private TabControl tabControl;

        private Panel searchPanel;
        private TextBox searchBox;
        private Button btnNext;
        private Button btnPrev;
        private Button btnClose;

        private void InitializeComponent()
        {
            tabControl = new DoubleBufferedTabControl();

            searchPanel = new Panel();
            searchBox = new TextBox();
            btnPrev = new Button();
            btnNext = new Button();
            btnClose = new Button();

            SuspendLayout();

            // =========================
            // CaptureMemoForm
            // =========================

            Text = "CaptureMemo";
            Width = 420;
            Height = 650;
            BackColor = Color.White;
            TopMost = true;
            KeyPreview = true;

            // =========================
            // tabControl
            // =========================

            tabControl.Dock = DockStyle.Fill;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.ItemSize = new Size(60, 24);
            tabControl.Font = new Font("Meiryo", 9f);

            // =========================
            // searchPanel
            // =========================

            searchPanel.Height = 44;
            searchPanel.Dock = DockStyle.Top;
            searchPanel.Visible = false;
            searchPanel.BackColor = Color.FromArgb(245, 245, 245);

            // =========================
            // searchBox
            // =========================

            searchBox.Left = 12;
            searchBox.Top = 10;
            searchBox.Width = 200;
            searchBox.Font = new Font("Meiryo", 10);
            searchBox.BorderStyle = BorderStyle.FixedSingle;

            // =========================
            // btnPrev
            // =========================

            btnPrev.Text = "↑";
            btnPrev.Font = new Font("Meiryo", 10, FontStyle.Bold);
            btnPrev.SetBounds(220, 9, 32, 26);
            btnPrev.FlatStyle = FlatStyle.Flat;
            btnPrev.FlatAppearance.BorderSize = 0;
            btnPrev.BackColor = Color.FromArgb(230, 230, 230);
            btnPrev.Cursor = Cursors.Hand;

            btnPrev.MouseEnter += SearchButton_MouseEnter;
            btnPrev.MouseLeave += SearchButton_MouseLeave;

            // =========================
            // btnNext
            // =========================

            btnNext.Text = "↓";
            btnNext.Font = new Font("Meiryo", 10, FontStyle.Bold);
            btnNext.SetBounds(257, 9, 32, 26);
            btnNext.FlatStyle = FlatStyle.Flat;
            btnNext.FlatAppearance.BorderSize = 0;
            btnNext.BackColor = Color.FromArgb(230, 230, 230);
            btnNext.Cursor = Cursors.Hand;

            btnNext.MouseEnter += SearchButton_MouseEnter;
            btnNext.MouseLeave += SearchButton_MouseLeave;

            // =========================
            // btnClose
            // =========================

            btnClose.Text = "×";
            btnClose.Font = new Font("Meiryo", 10, FontStyle.Bold);
            btnClose.SetBounds(294, 9, 32, 26);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.BackColor = Color.FromArgb(230, 230, 230);
            btnClose.Cursor = Cursors.Hand;

            btnClose.MouseEnter += SearchButton_MouseEnter;
            btnClose.MouseLeave += SearchButton_MouseLeave;

            // =========================
            // searchPanel Controls
            // =========================

            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(btnPrev);
            searchPanel.Controls.Add(btnNext);
            searchPanel.Controls.Add(btnClose);

            // =========================
            // Form Controls
            // =========================

            Controls.Add(tabControl);
            Controls.Add(searchPanel);

            searchPanel.BringToFront();

            ResumeLayout(false);
        }

        // =========================
        // 検索ボタンのホバー
        // =========================

        private void SearchButton_MouseEnter(object sender, System.EventArgs e)
        {
            if (sender is Button btn)
            {
                btn.BackColor = Color.FromArgb(210, 210, 210);
            }
        }

        private void SearchButton_MouseLeave(object sender, System.EventArgs e)
        {
            if (sender is Button btn)
            {
                btn.BackColor = Color.FromArgb(230, 230, 230);
            }
        }
    }
}