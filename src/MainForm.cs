using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Water
{
    internal sealed class MainForm : Form
    {
        private readonly Preferences preferences;
        private readonly Reminder reminder;
        private readonly EventWaitHandle showEvent;
        private readonly bool preview;
        private readonly NotifyIcon tray;
        private readonly Icon trayIcon;
        private readonly ContextMenuStrip trayMenu;
        private readonly ToolStripMenuItem pauseMenu;
        private readonly ToolStripMenuItem soundMenu;
        private readonly System.Windows.Forms.Timer timer;
        private readonly System.Windows.Forms.Timer intervalCommitTimer;
        private readonly TextBox interval;
        private readonly SoftButton pauseButton;
        private readonly ToolTip tips;
        private ReminderForm popup;
        private bool exiting;
        private bool due;
        private bool saveFailed;
        private bool shownTrayHint;
        private float phase;
        private string status;

        public MainForm(EventWaitHandle showEvent, bool preview)
        {
            this.showEvent = showEvent;
            this.preview = preview;
            preferences = preview ? new Preferences() : Preferences.Load(Preferences.DefaultPath);
            reminder = new Reminder(preferences.Seconds, DateTime.UtcNow);
            Text = "Вода — напоминание";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(460, 460);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Design.Paper;
            Font = Design.Font(10, false);
            Icon = Design.CreateIcon();
            DoubleBuffered = true;
            AccessibleName = Text;
            tips = new ToolTip();

            SoftButton minimize = AddButton("—", new Rectangle(357, 22, 34, 30), false, delegate { HideToTray(); });
            minimize.AccessibleName = "Свернуть в трей";
            tips.SetToolTip(minimize, "Свернуть в системный трей");
            SoftButton close = AddButton("×", new Rectangle(399, 22, 34, 30), false, delegate { Close(); });
            close.AccessibleName = "Закрыть окно и оставить напоминания";
            tips.SetToolTip(close, "Скрыть окно. Для выхода: значок в трее → Выход");

            AddButton("−", new Rectangle(245, 323, 32, 32), false, delegate { ChangeIntervalSeconds(Math.Max(900, preferences.Seconds - 900)); }).AccessibleName = "Уменьшить интервал на 15 минут";
            interval = new TextBox
            {
                Location = new Point(281, 327), Size = new Size(73, 30), Text = (preferences.Seconds / 60).ToString(CultureInfo.InvariantCulture),
                BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = Design.Ink,
                Font = Design.Font(13, true), TextAlign = HorizontalAlignment.Center,
                AccessibleName = "Интервал напоминаний в минутах", TabIndex = 0
            };
            interval.KeyPress += delegate(object sender, KeyPressEventArgs e)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
            };
            intervalCommitTimer = new System.Windows.Forms.Timer { Interval = 300 };
            interval.TextChanged += delegate
            {
                intervalCommitTimer.Stop();
                intervalCommitTimer.Start();
            };
            intervalCommitTimer.Tick += delegate { intervalCommitTimer.Stop(); PersistInterval(); };
            interval.Validated += delegate { PersistInterval(); };
            Controls.Add(interval);
            tips.SetToolTip(interval, "Введите минуты (от 15 минут до 24 часов). Кнопки + и − меняют значение на 15 минут.");
            AddButton("+", new Rectangle(389, 323, 32, 32), false, delegate { ChangeIntervalSeconds(Math.Min(86400, preferences.Seconds + 900)); }).AccessibleName = "Увеличить интервал на 15 минут";
            AddButton("Я выпил(а) воду", new Rectangle(32, 383, 264, 43), true, delegate { Drink(); }).TabIndex = 1;
            pauseButton = AddButton("Пауза", new Rectangle(308, 383, 120, 43), false, delegate { TogglePause(); });
            pauseButton.TabIndex = 2;

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Открыть", null, delegate { ShowMain(); });
            trayMenu.Items.Add("Я выпил(а) воду", null, delegate { Drink(); });
            pauseMenu = new ToolStripMenuItem("Пауза", null, delegate { TogglePause(); });
            trayMenu.Items.Add(pauseMenu);
            soundMenu = new ToolStripMenuItem("Звук напоминаний") { Checked = preferences.Sound, CheckOnClick = true };
            soundMenu.Click += delegate { preferences.Sound = soundMenu.Checked; SavePreferences(); };
            trayMenu.Items.Add(soundMenu);
            trayMenu.Items.Add("Проверить напоминание", null, delegate { Notify(false); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Выход", null, delegate { exiting = true; Close(); });
            ContextMenuStrip = trayMenu;
            trayIcon = new Icon(Icon, SystemInformation.SmallIconSize);
            tray = new NotifyIcon { Icon = trayIcon, Text = "Вода — напоминания включены", ContextMenuStrip = trayMenu, Visible = true };
            tray.DoubleClick += delegate { ShowMain(); };
            tray.BalloonTipClicked += delegate { ShowMain(); };

            timer = new System.Windows.Forms.Timer { Interval = 100 };
            timer.Tick += delegate
            {
                if (showEvent.WaitOne(0)) ShowMain();
                if (reminder.Tick(DateTime.UtcNow)) Notify(true);
                UpdateStatus();
                if (Visible && WindowState != FormWindowState.Minimized)
                {
                    phase += 0.12f;
                    Invalidate(ScaleRect(new Rectangle(148, 80, 164, 165)));
                }
            };
            timer.Start();
            UpdateStatus();
            Shown += delegate { if (preview) Notify(false); };
        }
        private Rectangle ScaleRect(Rectangle rect)
        {
            float scale = ClientSize.Width / 460f;
            return new Rectangle((int)(rect.X * scale), (int)(rect.Y * scale), (int)(rect.Width * scale), (int)(rect.Height * scale));
        }
        private SoftButton AddButton(string text, Rectangle bounds, bool primary, EventHandler click)
        {
            SoftButton button = new SoftButton(text, bounds, primary) { BackColor = Design.Paper };
            button.Click += click;
            Controls.Add(button);
            return button;
        }
        private void SavePreferences()
        {
            saveFailed = !preview && !preferences.Save(Preferences.DefaultPath);
            Invalidate();
        }
        private void UpdateStatus()
        {
            TimeSpan remaining = reminder.Remaining(DateTime.UtcNow);
            int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
            string time = string.Format("{0:00}:{1:00}:{2:00}", seconds / 3600, seconds / 60 % 60, seconds % 60);
            string newStatus = reminder.Paused ? "Напоминания на паузе" : "Следующий стакан через " + time;
            if (status != newStatus)
            {
                status = newStatus;
                Invalidate(ScaleRect(new Rectangle(25, 250, 410, 60)));
                tray.Text = reminder.Paused ? "Вода — пауза" : "Вода — через " + time;
            }
        }
        private void Drink()
        {
            reminder.Restart(DateTime.UtcNow);
            due = false;
            DismissPopup();
            UpdateStatus();
            Invalidate();
        }
        private void TogglePause()
        {
            reminder.TogglePause(DateTime.UtcNow);
            pauseButton.Text = reminder.Paused ? "Продолжить" : "Пауза";
            pauseMenu.Text = reminder.Paused ? "Продолжить" : "Пауза";
            pauseMenu.Checked = reminder.Paused;
            if (reminder.Paused) { due = false; DismissPopup(); }
            UpdateStatus();
            Invalidate();
        }
        private void Notify(bool actual)
        {
            if (actual) due = true;
            if (popup == null || popup.IsDisposed)
            {
                popup = new ReminderForm(Icon, delegate { Drink(); }, delegate
                {
                    reminder.Snooze(DateTime.UtcNow);
                    due = false;
                    DismissPopup();
                    UpdateStatus();
                    Invalidate();
                });
                popup.Show();
            }
            if (preferences.Sound) SystemSounds.Asterisk.Play();
            Invalidate();
        }
        private void DismissPopup() { if (popup != null && !popup.IsDisposed) popup.Close(); popup = null; }
        private void ShowMain() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void HideToTray()
        {
            Hide();
            if (!shownTrayHint)
            {
                shownTrayHint = true;
                tray.ShowBalloonTip(4000, "Вода работает в фоне", "Чтобы открыть окно или выйти, нажмите значок стакана в системном трее.", ToolTipIcon.Info);
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            PersistInterval();
            if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideToTray(); }
            else { timer.Stop(); DismissPopup(); tray.Visible = false; }
            base.OnFormClosing(e);
        }
        private void PersistInterval()
        {
            if (intervalCommitTimer != null) intervalCommitTimer.Stop();
            int enteredMinutes;
            string text = interval.Text.Trim();
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out enteredMinutes) || enteredMinutes < 15 || enteredMinutes > 1440)
                enteredMinutes = preferences.Seconds / 60;
            ChangeIntervalSeconds(enteredMinutes * 60);
        }
        private void ChangeIntervalSeconds(int seconds)
        {
            if (seconds < 900 || seconds > 86400) return;
            intervalCommitTimer.Stop();
            string display = (seconds / 60).ToString(CultureInfo.InvariantCulture);
            if (interval.Text != display) interval.Text = display;
            if (preferences.Seconds != seconds)
            {
                reminder.SetInterval(seconds, DateTime.UtcNow);
                preferences.Seconds = seconds;
                due = false;
                DismissPopup();
                UpdateStatus();
            }
            SavePreferences();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); intervalCommitTimer.Dispose(); tray.Dispose(); trayIcon.Dispose(); trayMenu.Dispose(); tips.Dispose(); if (Icon != null) Icon.Dispose(); }
            base.Dispose(disposing);
        }
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y < 75 * ClientSize.Width / 460f) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            float scale = ClientSize.Width / 460f;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(scale, scale);
            using (Pen border = new Pen(Color.FromArgb(216, 228, 230))) g.DrawRectangle(border, 0, 0, 459, 459);
            using (Brush dot = new SolidBrush(Design.Blue)) g.FillEllipse(dot, 32, 31, 8, 8);
            DrawText(g, "ВОДА", 11, true, Design.Ink, new Rectangle(50, 23, 100, 26), false);
            DrawText(g, "Маленькая забота о себе", 9, false, Design.Muted, new Rectangle(32, 52, 300, 22), false);
            using (Brush halo = new SolidBrush(Design.Pale)) g.FillEllipse(halo, 143, 82, 174, 174);
            Design.Glass(g, new RectangleF(161, 88, 138, 158), phase);
            DrawText(g, reminder.Paused ? "Можно немного выдохнуть" : due ? "Пора выпить воды" : "Время для глотка воды", 17, true, Design.Ink, new Rectangle(20, 257, 420, 29), true);
            DrawText(g, status, 10, false, Design.Muted, new Rectangle(20, 286, 420, 22), true);
            Design.FillRound(g, new RectangleF(32, 317, 396, 49), 12, Color.White);
            DrawText(g, "Интервал в минутах", 10, false, Design.Ink, new Rectangle(48, 322, 192, 21), false);
            DrawText(g, "Шаг 15 минут", 8, false, Design.Muted, new Rectangle(48, 342, 185, 16), false);
            DrawText(g, "мин.", 9, false, Design.Muted, new Rectangle(355, 326, 35, 30), false);
            DrawText(g, saveFailed ? "Не удалось сохранить настройки" : "Закрытие окна сворачивает приложение в трей", 8, false, saveFailed ? Color.Firebrick : Design.Muted, new Rectangle(20, 433, 420, 17), true);
        }
        // GDI text does not follow Graphics transforms; convert logical bounds explicitly.
        private void DrawText(Graphics g, string text, float size, bool bold, Color color, Rectangle bounds, bool center)
        {
            GraphicsState state = g.Save();
            g.ResetTransform();
            Design.Text(g, text, size, bold, color, ScaleRect(bounds), center ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft);
            g.Restore(state);
        }
    }

    internal sealed class ReminderForm : Form
    {
        protected override bool ShowWithoutActivation { get { return true; } }
        public ReminderForm(Icon icon, Action drink, Action snooze)
        {
            Text = "Пора выпить воды";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(370, 192);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Design.Paper; TopMost = true; ShowInTaskbar = false; Icon = icon;
            Label title = new Label { Text = "Пора выпить воды", Font = Design.Font(17, true), ForeColor = Design.Ink, Bounds = new Rectangle(24, 21, 322, 35) };
            Label description = new Label { Text = "Сделайте паузу и выпейте стакан воды.", Font = Design.Font(10, false), ForeColor = Design.Muted, Bounds = new Rectangle(25, 66, 322, 42) };
            SoftButton done = new SoftButton("Выпил(а)", new Rectangle(24, 124, 155, 42), true);
            SoftButton later = new SoftButton("Через 10 мин", new Rectangle(191, 124, 155, 42), false);
            done.Click += delegate { drink(); Close(); };
            later.Click += delegate { snooze(); Close(); };
            Controls.AddRange(new Control[] { title, description, done, later });
            AcceptButton = done;
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(Math.Max(area.Left, area.Right - Width - 20), Math.Max(area.Top, area.Bottom - Height - 20));
        }
    }
}
