using System;
using System.Drawing;
using System.Windows.Forms;
using EsgSignalCreator.Ui;

namespace EsgSignalCreator
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Surface unhandled errors instead of dying silently.
            Application.ThreadException += (s, e) => ShowFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowFatal(e.ExceptionObject as Exception);

            // The new Signal Studio shell is the default; pass --classic to launch the
            // original single-window MainForm.
            bool classic = Array.Exists(args ?? new string[0],
                a => string.Equals(a, "--classic", StringComparison.OrdinalIgnoreCase));
            Form form = classic ? (Form)new MainForm() : new StudioForm();
            form.Icon = AppIcon();
            Application.Run(form);
        }

        // The window/taskbar icon, taken from the icon embedded in the exe (ApplicationIcon).
        private static Icon AppIcon()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return null; }
        }

        private static void ShowFatal(Exception ex)
        {
            if (ex == null) return;
            MessageBox.Show(ex.Message, "ESG Signal Studio — error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
