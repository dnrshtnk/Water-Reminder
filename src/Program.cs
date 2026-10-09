using System;
using System.Threading;
using System.Windows.Forms;

namespace Water
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\WaterReminder.Desktop.v1", out created))
            using (EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\WaterReminder.Show.v1"))
            {
                if (!created) { show.Set(); return; }
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm(show, Array.IndexOf(args, "--preview") >= 0));
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}
