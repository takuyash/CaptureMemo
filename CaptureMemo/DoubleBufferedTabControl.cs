using System.Reflection;

namespace CaptureMemo
{
    public class DoubleBufferedTabControl : TabControl
    {
        public DoubleBufferedTabControl()
        {
            typeof(TabControl)
                .GetProperty(
                    "DoubleBuffered",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                ?.SetValue(this, true, null);
        }
    }
}