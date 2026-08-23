using System;
using System.Windows.Forms;

namespace Ceprkac
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            InjectedModuleCleaner.StartGlobal();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                try { MessageBox.Show(e.Exception.Message, "Ceprkac", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try { MessageBox.Show((e.ExceptionObject as Exception)?.Message ?? "Unhandled error", "Ceprkac", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                catch { }
            };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
