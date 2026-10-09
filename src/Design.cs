using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Water
{
    internal static class Design
    {
        public static readonly Color Paper = Color.FromArgb(247, 249, 248);
        public static readonly Color Ink = Color.FromArgb(31, 62, 76);
        public static readonly Color Muted = Color.FromArgb(105, 128, 138);
        public static readonly Color Blue = Color.FromArgb(44, 142, 182);
        public static readonly Color Pale = Color.FromArgb(230, 240, 242);
        public static Font Font(float size, bool bold) { return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point); }
        public static GraphicsPath Round(RectangleF bounds, float radius)
        {
            float d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            p.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            p.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            p.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        public static void FillRound(Graphics g, RectangleF bounds, float radius, Color color)
        {
            using (GraphicsPath p = Round(bounds, radius)) using (Brush brush = new SolidBrush(color)) g.FillPath(brush, p);
        }
        public static void Text(Graphics g, string text, float size, bool bold, Color color, Rectangle bounds, ContentAlignment alignment)
        {
            using (Font font = Font(size, bold))
            {
                TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
                if (alignment == ContentAlignment.MiddleCenter) flags |= TextFormatFlags.HorizontalCenter;
                else if (alignment == ContentAlignment.MiddleRight) flags |= TextFormatFlags.Right;
                TextRenderer.DrawText(g, text, font, bounds, color, flags);
            }
        }
        public static void Glass(Graphics g, RectangleF box, float phase)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(box.X, box.Y);
            g.ScaleTransform(box.Width / 150f, box.Height / 170f);
            using (Brush shadow = new SolidBrush(Color.FromArgb(18, 50, 100, 120))) g.FillEllipse(shadow, 24, 153, 103, 12);
            using (GraphicsPath glass = new GraphicsPath())
            {
                glass.AddLines(new PointF[] { new PointF(18, 12), new PointF(132, 12), new PointF(119, 145) });
                glass.AddBezier(119, 145, 118, 157, 32, 157, 31, 145);
                glass.CloseFigure();
                using (Brush white = new SolidBrush(Color.FromArgb(210, 255, 255, 255))) g.FillPath(white, glass);
                GraphicsState clip = g.Save();
                g.SetClip(glass);
                using (GraphicsPath water = new GraphicsPath())
                {
                    float wave = (float)Math.Sin(phase) * 4;
                    water.AddBezier(10, 66, 48, 52 + wave, 96, 77 - wave, 141, 61);
                    water.AddLine(141, 61, 141, 170);
                    water.AddLine(141, 170, 10, 170);
                    water.CloseFigure();
                    using (LinearGradientBrush fill = new LinearGradientBrush(new Point(0, 58), new Point(0, 160), Color.FromArgb(115, 203, 225), Color.FromArgb(49, 154, 192))) g.FillPath(fill, water);
                }
                using (Pen wavePen = new Pen(Color.FromArgb(150, 237, 255, 255), 2)) g.DrawBezier(wavePen, 10, 66, 48, 52 + (float)Math.Sin(phase) * 4, 96, 77 - (float)Math.Sin(phase) * 4, 141, 61);
                using (Pen reflection = new Pen(Color.FromArgb(165, 255, 255, 255), 5)) { reflection.StartCap = LineCap.Round; reflection.EndCap = LineCap.Round; g.DrawLine(reflection, 35, 31, 43, 128); }
                using (Brush bubbles = new SolidBrush(Color.FromArgb(100, 237, 255, 255))) { g.FillEllipse(bubbles, 91, 108, 7, 7); g.FillEllipse(bubbles, 81, 126, 4, 4); }
                g.Restore(clip);
                using (Pen edge = new Pen(Color.FromArgb(143, 190, 203), 2.5f)) { edge.LineJoin = LineJoin.Round; g.DrawPath(edge, glass); }
                using (Pen rim = new Pen(Color.FromArgb(197, 222, 229), 2)) g.DrawLine(rim, 20, 15, 130, 15);
            }
            g.Restore(state);
        }
        public static Icon CreateIcon()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Water.AppIcon.ico"))
            {
                if (stream == null) throw new InvalidOperationException("Embedded application icon is missing.");
                using (Icon icon = new Icon(stream, SystemInformation.IconSize)) return (Icon)icon.Clone();
            }
        }
    }

    internal sealed class SoftButton : Button
    {
        public bool Primary;
        private bool hover;
        public SoftButton(string text, Rectangle bounds, bool primary)
        {
            Text = text; Bounds = bounds; Primary = primary;
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand; Font = Design.Font(10, true);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);
            Color fill = Primary ? (hover ? Color.FromArgb(34, 122, 160) : Design.Blue) : (hover ? Color.FromArgb(214, 232, 236) : Design.Pale);
            Design.FillRound(e.Graphics, new RectangleF(0, 0, Width - 1, Height - 1), Math.Min(12, Height / 3), fill);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Primary ? Color.White : Design.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
        }
    }
}
