using System;
using System.Threading;
using System.Windows.Forms;

namespace CaptureMemo
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // ==========================================
            // 二重起動防止
            // ==========================================
            bool createdNew;

            using Mutex mutex = new Mutex(
                true,
                "CaptureMemo_SingleInstance",
                out createdNew);

            // すでに起動している場合は何もせず終了
            if (!createdNew)
            {
                return;
            }

            Application.EnableVisualStyles();
            Application.Run(new CaptureMemoForm());
        }
    }
}