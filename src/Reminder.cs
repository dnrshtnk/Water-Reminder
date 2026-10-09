using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Water
{
    internal sealed class Reminder
    {
        public int Seconds { get; private set; }
        public DateTime NextUtc { get; private set; }
        public bool Paused { get; private set; }
        private TimeSpan remaining;

        public Reminder(int seconds, DateTime now) { SetInterval(seconds, now); }
        public void SetInterval(int seconds, DateTime now)
        {
            if (seconds < 900 || seconds > 86400) throw new ArgumentOutOfRangeException("seconds");
            Seconds = seconds;
            Restart(now);
        }
        public void Restart(DateTime now)
        {
            remaining = TimeSpan.FromSeconds(Seconds);
            NextUtc = now + remaining;
        }
        public TimeSpan Remaining(DateTime now)
        {
            TimeSpan value = Paused ? remaining : NextUtc - now;
            return value < TimeSpan.Zero ? TimeSpan.Zero : value;
        }
        public void TogglePause(DateTime now)
        {
            if (Paused) { NextUtc = now + remaining; Paused = false; }
            else { remaining = Remaining(now); Paused = true; }
        }
        public void Snooze(DateTime now)
        {
            remaining = TimeSpan.FromMinutes(10);
            NextUtc = now + remaining;
        }
        public bool Tick(DateTime now)
        {
            if (Paused || now < NextUtc) return false;
            // After sleep, show one reminder instead of replaying missed intervals.
            Restart(now);
            return true;
        }
    }

    internal sealed class Preferences
    {
        public int Seconds = 3600;
        public bool Sound = true;
        public static string DefaultPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WaterReminder", "settings.xml"); }
        }
        public static Preferences Load(string path)
        {
            Preferences result = new Preferences();
            try
            {
                XmlDocument document = new XmlDocument();
                document.XmlResolver = null;
                using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
                int seconds;
                bool sound;
                if (int.TryParse(document.DocumentElement.GetAttribute("seconds"), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) && seconds >= 900 && seconds <= 86400)
                    result.Seconds = seconds;
                else
                {
                    // Existing installations stored the interval as hours. Convert it once and
                    // preserve the same duration when the next preference save writes seconds.
                    decimal oldHours;
                    if (decimal.TryParse(document.DocumentElement.GetAttribute("hours"), NumberStyles.Number, CultureInfo.InvariantCulture, out oldHours) && oldHours >= 0.25m && oldHours <= 24m)
                        result.Seconds = Decimal.ToInt32(decimal.Round(oldHours * 3600m, 0, MidpointRounding.AwayFromZero));
                }
                if (bool.TryParse(document.DocumentElement.GetAttribute("sound"), out sound)) result.Sound = sound;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (XmlException) { }
            return result;
        }
        public bool Save(string path)
        {
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (XmlWriter writer = XmlWriter.Create(temporary, new XmlWriterSettings { Indent = true }))
                {
                    writer.WriteStartElement("water");
                    writer.WriteAttributeString("seconds", Seconds.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("sound", Sound.ToString());
                    writer.WriteEndElement();
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
