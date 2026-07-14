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
            args = args ?? new string[0];

            // Headless tutorial-image generation (#150): render every offline tutorial plot to PNG and
            // exit, without showing the GUI. Usage: ESG-SignalCreator.exe --tutorial-images <dir>
            int tiIdx = Array.FindIndex(args, a => string.Equals(a, "--tutorial-images", StringComparison.OrdinalIgnoreCase));
            if (tiIdx >= 0)
            {
                string dir = tiIdx + 1 < args.Length ? args[tiIdx + 1] : "docs/images/tutorials";
                try { Ui.TutorialImages.TutorialImageHarness.Run(dir); Environment.Exit(0); }
                catch (Exception ex) { Console.Error.WriteLine("Tutorial-image generation failed: " + ex); Environment.Exit(2); }
            }

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
