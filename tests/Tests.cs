using System;
using System.IO;

namespace Water
{
    internal static class Tests
    {
        private static int count;
        private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        public static int Main()
        {
            string directory = Path.Combine(Path.GetDirectoryName(typeof(Tests).Assembly.Location), "test-settings");
            string path = Path.Combine(directory, "settings.xml");
            try
            {
                DateTime start = new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
                Reminder reminder = new Reminder(3600, start);
                Check(!reminder.Tick(start.AddMinutes(59)), "No early reminders");
                Check(reminder.Tick(start.AddHours(1)), "Reminder at deadline");
                Check(!reminder.Tick(start.AddHours(1)), "No duplicate reminder");
                Check(reminder.Tick(start.AddDays(2)), "One reminder after sleep");
                Check(reminder.NextUtc == start.AddDays(2).AddHours(1), "Missed intervals are skipped");
                reminder = new Reminder(3600, start);
                reminder.TogglePause(start.AddMinutes(20));
                Check(!reminder.Tick(start.AddHours(8)), "Pause suppresses reminders");
                Check(reminder.Remaining(start.AddHours(8)) == TimeSpan.FromMinutes(40), "Pause freezes countdown");
                reminder.TogglePause(start.AddHours(8));
                Check(reminder.NextUtc == start.AddHours(8).AddMinutes(40), "Resume preserves remaining time");
                reminder.SetInterval(900, start);
                Check(reminder.NextUtc == start.AddMinutes(15), "15 minute interval");
                reminder.SetInterval(1800, start);
                Check(reminder.NextUtc == start.AddMinutes(30), "30 minute interval");
                reminder.Snooze(start);
                Check(!reminder.Tick(start.AddMinutes(9)) && reminder.Tick(start.AddMinutes(10)), "Snooze fires after ten minutes");
                reminder.Restart(start);
                Check(reminder.NextUtc == start.AddMinutes(30), "Drinking restarts configured interval");
                reminder.TogglePause(start.AddMinutes(5));
                reminder.SetInterval(7200, start.AddMinutes(5));
                Check(reminder.Paused && reminder.Remaining(start.AddHours(5)) == TimeSpan.FromHours(2), "Changing paused interval stays paused");
                bool rejected = false;
                try { new Reminder(899, start); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "Invalid intervals rejected");
                Preferences settings = new Preferences { Seconds = 4500, Sound = false };
                Check(settings.Save(path), "Settings created");
                Preferences loaded = Preferences.Load(path);
                Check(loaded.Seconds == 4500 && !loaded.Sound, "Settings round trip");
                settings.Seconds = 86400;
                Check(settings.Save(path) && Preferences.Load(path).Seconds == 86400, "Existing settings atomically replaced");
                File.WriteAllText(path, "broken xml");
                Check(Preferences.Load(path).Seconds == 3600, "Corrupt settings fall back safely");
                File.WriteAllText(path, "<water seconds=\"15\" sound=\"true\"/>");
                Check(Preferences.Load(path).Seconds == 3600, "Out-of-range saved interval ignored");
                File.WriteAllText(path, "<water hours=\"0.25\" sound=\"true\"/>");
                Check(Preferences.Load(path).Seconds == 900, "Old hour settings migrate to seconds");
                Check(Environment.Is64BitProcess, "Executable runs as x64");
                Console.WriteLine("All " + count + " checks passed.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
            finally { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(directory)) Directory.Delete(directory); }
        }
    }
}
