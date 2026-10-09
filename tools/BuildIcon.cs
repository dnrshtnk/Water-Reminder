using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

// Native vector artwork, rendered independently at every Windows icon size.
internal static class BuildIcon
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };

    private static GraphicsPath Glass()
    {
        GraphicsPath p = new GraphicsPath();
        p.AddLine(64, 48, 192, 48);
        p.AddLine(192, 48, 177, 194);
        p.AddBezier(177, 194, 175, 215, 81, 215, 79, 194);
        p.CloseFigure();
        return p;
    }

    private static Bitmap Render(int size)
    {
        // Supersampling preserves clean diagonals and transparent rounded corners.
        using (Bitmap large = new Bitmap(size * 4, size * 4, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(large))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(large.Width / 256f, large.Height / 256f);
                using (GraphicsPath tile = new GraphicsPath())
                {
                    tile.AddArc(8, 8, 100, 100, 180, 90);
                    tile.AddArc(148, 8, 100, 100, 270, 90);
                    tile.AddArc(148, 148, 100, 100, 0, 90);
                    tile.AddArc(8, 148, 100, 100, 90, 90);
                    tile.CloseFigure();
                    using (Brush fill = new SolidBrush(Color.FromArgb(228, 242, 246))) g.FillPath(fill, tile);
                }
                using (GraphicsPath glass = Glass())
                {
                    using (Brush white = new SolidBrush(Color.FromArgb(251, 254, 255))) g.FillPath(white, glass);
                    GraphicsState state = g.Save();
                    g.SetClip(glass);
                    using (GraphicsPath water = new GraphicsPath())
                    {
                        water.AddBezier(58, 112, 100, 92, 149, 137, 198, 109);
                        water.AddLine(198, 109, 198, 220);
                        water.AddLine(198, 220, 58, 220);
                        water.CloseFigure();
                        using (Brush blue = new SolidBrush(Color.FromArgb(44, 157, 200))) g.FillPath(blue, water);
                    }
                    if (size >= 24)
                    {
                        using (Pen light = new Pen(Color.FromArgb(203, 240, 250), 9))
                        {
                            light.StartCap = LineCap.Round; light.EndCap = LineCap.Round;
                            g.DrawLine(light, 86, 72, 97, 175);
                        }
                    }
                    g.Restore(state);
                    using (Pen edge = new Pen(Color.FromArgb(35, 107, 139), size <= 20 ? 12 : 9))
                    {
                        edge.LineJoin = LineJoin.Round;
                        g.DrawPath(edge, glass);
                    }
                }
            }
            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(large, new Rectangle(0, 0, size, size), 0, 0, large.Width, large.Height, GraphicsUnit.Pixel);
            }
            return result;
        }
    }

    private static byte[] Frame(Bitmap bitmap)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            int size = bitmap.Width;
            // DIB frames also let .NET Framework select the 256 px image directly.
            int maskStride = ((size + 31) / 32) * 4;
            writer.Write(40); writer.Write(size); writer.Write(size * 2);
            writer.Write((short)1); writer.Write((short)32); writer.Write(0);
            writer.Write(size * size * 4 + maskStride * size);
            writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            for (int y = size - 1; y >= 0; y--)
                for (int x = 0; x < size; x++)
                {
                    Color color = bitmap.GetPixel(x, y);
                    writer.Write(color.B); writer.Write(color.G); writer.Write(color.R); writer.Write(color.A);
                }
            for (int y = size - 1; y >= 0; y--)
            {
                byte[] mask = new byte[maskStride];
                for (int x = 0; x < size; x++)
                    if (bitmap.GetPixel(x, y).A == 0) mask[x / 8] |= (byte)(128 >> (x % 8));
                writer.Write(mask);
            }
            return stream.ToArray();
        }
    }

    public static void Main(string[] args)
    {
        string directory = args[0];
        Directory.CreateDirectory(directory);
        byte[][] frames = new byte[Sizes.Length][];
        for (int i = 0; i < Sizes.Length; i++) using (Bitmap bitmap = Render(Sizes[i])) frames[i] = Frame(bitmap);
        using (BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(directory, "water.ico"))))
        {
            writer.Write((short)0); writer.Write((short)1); writer.Write((short)Sizes.Length);
            int offset = 6 + Sizes.Length * 16;
            for (int i = 0; i < Sizes.Length; i++)
            {
                writer.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                writer.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((short)1); writer.Write((short)32);
                writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length;
            }
            foreach (byte[] frame in frames) writer.Write(frame);
        }
        using (Bitmap hero = Render(256)) hero.Save(Path.Combine(directory, "water.png"), ImageFormat.Png);
        using (Bitmap preview = new Bitmap(640, 360))
        using (Graphics g = Graphics.FromImage(preview))
        using (Font font = new Font("Segoe UI", 10))
        {
            g.Clear(Color.FromArgb(247, 249, 248));
            using (Bitmap hero = Render(256)) g.DrawImageUnscaled(hero, 28, 50);
            using (Brush dark = new SolidBrush(Color.FromArgb(31, 62, 76))) g.FillRectangle(dark, 308, 180, 316, 150);
            g.DrawString("WINDOWS ICON  /  16 · 24 · 32 · 48 · 64", font, Brushes.DimGray, 310, 30);
            int x = 328;
            foreach (int size in new int[] { 16, 24, 32, 48, 64 })
            {
                using (Bitmap bitmap = Render(size))
                {
                    g.DrawImageUnscaled(bitmap, x, 112 - size / 2);
                    g.DrawImageUnscaled(bitmap, x, 252 - size / 2);
                }
                x += size + 18;
            }
            preview.Save(Path.Combine(directory, "icon-preview.png"), ImageFormat.Png);
        }
        Console.WriteLine("Icon: " + string.Join(", ", Array.ConvertAll(Sizes, n => n.ToString())) + " px");
    }
}
