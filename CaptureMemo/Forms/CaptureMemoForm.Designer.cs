using CaptureMemo.Helpers;

namespace CaptureMemo
{
    partial class CaptureMemoForm
    {
        private TabControl tabControl;

        private SearchBarPanel searchPanel;
        private TextBox searchBox;
        private IconButton btnNext;
        private IconButton btnPrev;
        private IconButton btnClose;
        private ToolTip searchToolTip;

        private void InitializeComponent()
        {
            tabControl = new DoubleBufferedTabControl();

            searchPanel = new SearchBarPanel();
            searchBox = new TextBox();
            btnPrev = new IconButton();
            btnNext = new IconButton();
            btnClose = new IconButton();
            searchToolTip = new ToolTip();

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
            tabControl.ItemSize = new Size(84, 32);
            tabControl.Font = new Font("Meiryo", 9f);

            // =========================
            // searchPanel
            // =========================

            searchPanel.Height = 44;
            searchPanel.Dock = DockStyle.Top;
            searchPanel.Visible = false;
            searchPanel.BackColor = Color.White;
            searchPanel.FieldBounds = new Rectangle(12, 8, 232, 28);
            searchPanel.DividerX = 315;
            searchPanel.Field = searchBox;

            // =========================
            // searchBox（角丸の入力欄の中に枠なしで配置）
            // =========================

            searchBox.Font = new Font("Meiryo", 10);
            searchBox.BorderStyle = BorderStyle.None;
            searchBox.BackColor = searchPanel.FieldColor;
            searchBox.PlaceholderText = "検索";
            searchBox.Left = searchPanel.FieldBounds.X + 30;
            searchBox.Width = searchPanel.FieldBounds.Width - 40;
            searchBox.Top = searchPanel.FieldBounds.Y +
                (searchPanel.FieldBounds.Height - searchBox.PreferredHeight) / 2;

            searchBox.Enter += (s, e) => searchBox.BackColor = Color.White;
            searchBox.Leave += (s, e) => searchBox.BackColor = searchPanel.FieldColor;

            // =========================
            // btnPrev
            // =========================

            btnPrev.Glyph = IconGlyph.ChevronUp;
            btnPrev.SetBounds(252, 8, 28, 28);
            searchToolTip.SetToolTip(btnPrev, "前を検索");

            // =========================
            // btnNext
            // =========================

            btnNext.Glyph = IconGlyph.ChevronDown;
            btnNext.SetBounds(282, 8, 28, 28);
            searchToolTip.SetToolTip(btnNext, "次を検索");

            // =========================
            // btnClose（ホバーで赤くしてタブの×と揃える）
            // =========================

            btnClose.Glyph = IconGlyph.Close;
            btnClose.SetBounds(321, 8, 28, 28);
            btnClose.HoverBackColor = Color.FromArgb(254, 226, 226);
            btnClose.PressedBackColor = Color.FromArgb(252, 202, 202);
            btnClose.HoverIconColor = Color.FromArgb(220, 38, 38);
            searchToolTip.SetToolTip(btnClose, "閉じる (Esc)");

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
    }
}
