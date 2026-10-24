using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CaptureMemo.Helpers
{
    public class DoubleBufferedTabControl : TabControl
    {
        // タブ帯の背景色
        public Color StripColor { get; set; } = Color.FromArgb(240, 241, 245);

        // ページ側の背景色
        public Color PageColor { get; set; } = Color.White;

        // 帯とページの境界線
        public Color BorderColor { get; set; } = Color.FromArgb(225, 227, 232);

        // スクロールボタンとタブの間の余白
        private const int ScrollButtonsGap = 6;

        private TabScrollButtons scrollButtons;

        internal int StripBottom => TabCount > 0
            ? GetTabRect(0).Bottom
            : ItemSize.Height + 2;

        // スクロールボタンの位置（表示されていなければEmpty）
        internal Rectangle ScrollButtonsBounds { get; private set; }

        internal bool CanScrollLeft =>
            TabCount > 0 && GetTabRect(0).Left < 0;

        internal bool CanScrollRight =>
            TabCount > 0 &&
            !ScrollButtonsBounds.IsEmpty &&
            GetTabRect(TabCount - 1).Right > ScrollButtonsBounds.Left;

        public DoubleBufferedTabControl()
        {
            // 標準の3D枠を描かせず、全て自前で描画する
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            scrollButtons = new TabScrollButtons(this);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            scrollButtons.ReleaseHandle();
            base.OnHandleDestroyed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            int stripBottom = StripBottom;

            UpdateScrollButtons();

            // =========================
            // 背景
            // =========================

            using (var b = new SolidBrush(PageColor))
            {
                g.FillRectangle(b, ClientRectangle);
            }

            using (var b = new SolidBrush(StripColor))
            {
                g.FillRectangle(b, 0, 0, Width, stripBottom);
            }

            // =========================
            // 境界線
            // =========================

            using (var p = new Pen(BorderColor))
            {
                g.DrawLine(p, 0, stripBottom - 1, Width, stripBottom - 1);
            }

            // =========================
            // スクロールボタンと被る部分にはタブを描かない
            // =========================

            int tabsRight = ScrollButtonsBounds.IsEmpty
                ? Width
                : ScrollButtonsBounds.Left - ScrollButtonsGap;

            var state = g.Save();

            g.SetClip(
                new Rectangle(tabsRight, 0, Width - tabsRight, stripBottom),
                CombineMode.Exclude);

            // 選択タブの下は境界線を途切れさせる
            if (SelectedIndex >= 0 && SelectedIndex < TabCount)
            {
                var sel = GetTabRect(SelectedIndex);

                using var b = new SolidBrush(PageColor);
                g.FillRectangle(b, sel.Left + 1, stripBottom - 1, sel.Width - 2, 1);
            }

            // =========================
            // 各タブ（描画はDrawItemに委譲）
            // =========================

            for (int i = 0; i < TabCount; i++)
            {
                var itemState = i == SelectedIndex
                    ? DrawItemState.Selected
                    : DrawItemState.None;

                OnDrawItem(new DrawItemEventArgs(g, Font, GetTabRect(i), i, itemState));
            }

            // 途中で切れるタブはフェードさせて自然に見せる
            if (CanScrollRight)
            {
                var fade = new Rectangle(tabsRight - 16, 0, 16, stripBottom - 1);

                using var b = new LinearGradientBrush(
                    new Rectangle(fade.X - 1, fade.Y, fade.Width + 1, fade.Height),
                    Color.FromArgb(0, StripColor),
                    StripColor,
                    LinearGradientMode.Horizontal);

                g.FillRectangle(b, fade);
            }

            g.Restore(state);

            scrollButtons.Invalidate();
        }

        // =========================
        // スクロールボタンの検出
        // =========================

        private void UpdateScrollButtons()
        {
            IntPtr hwnd = FindWindowEx(Handle, IntPtr.Zero, "msctls_updown32", null);

            if (hwnd != scrollButtons.Handle)
            {
                scrollButtons.ReleaseHandle();

                if (hwnd != IntPtr.Zero)
                    scrollButtons.AssignHandle(hwnd);
            }

            if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
            {
                GetWindowRect(hwnd, out var rc);

                ScrollButtonsBounds = RectangleToClient(
                    Rectangle.FromLTRB(rc.Left, rc.Top, rc.Right, rc.Bottom));
            }
            else
            {
                ScrollButtonsBounds = Rectangle.Empty;
            }
        }

        // =========================
        // Win32
        // =========================

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(
            IntPtr hwndParent,
            IntPtr hwndChildAfter,
            string lpszClass,
            string? lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    }
}
