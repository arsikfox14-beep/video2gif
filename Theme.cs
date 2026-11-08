using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Video2Gif
{
    internal static class Ui
    {
        public static readonly double S = DetectScale();

        public static bool Dark;
        public static Color Bg;
        public static Color Card;
        public static Color Border;
        public static Color BorderStrong;
        public static Color Text;
        public static Color Muted;
        public static Color Faint;
        public static Color Accent;
        public static Color AccentDark;
        public static Color AccentInk;
        public static Color AccentSoft;
        public static Color AccentLine;
        public static Color AccentWash;
        public static Color Track;
        public static Color Good;
        public static Color Bad;
        public static Color OnAccent;

        static Ui()
        {
            ApplyColors(false);
        }

        public static void SetDark(bool dark)
        {
            Dark = dark;
            ApplyColors(dark);
        }

        private static void ApplyColors(bool dark)
        {
            if (!dark)
            {
                Bg = Color.FromArgb(247, 247, 248);
                Card = Color.White;
                Border = Color.FromArgb(232, 232, 235);
                BorderStrong = Color.FromArgb(201, 201, 206);
                Text = Color.FromArgb(20, 20, 20);
                Muted = Color.FromArgb(111, 111, 118);
                Faint = Color.FromArgb(160, 160, 167);
                Accent = Color.FromArgb(23, 23, 23);
                AccentDark = Color.Black;
                AccentInk = Color.FromArgb(23, 23, 23);
                AccentSoft = Color.FromArgb(240, 240, 241);
                AccentLine = Color.FromArgb(214, 214, 217);
                AccentWash = Color.FromArgb(247, 247, 248);
                Track = Color.FromArgb(237, 237, 239);
                Good = Color.FromArgb(16, 148, 104);
                Bad = Color.FromArgb(214, 48, 49);
                OnAccent = Color.White;
            }
            else
            {
                Bg = Color.FromArgb(14, 14, 15);
                Card = Color.FromArgb(24, 24, 26);
                Border = Color.FromArgb(43, 43, 46);
                BorderStrong = Color.FromArgb(69, 69, 74);
                Text = Color.FromArgb(242, 242, 243);
                Muted = Color.FromArgb(160, 160, 168);
                Faint = Color.FromArgb(110, 110, 118);
                Accent = Color.FromArgb(242, 242, 243);
                AccentDark = Color.White;
                AccentInk = Color.FromArgb(242, 242, 243);
                AccentSoft = Color.FromArgb(42, 42, 43);
                AccentLine = Color.FromArgb(69, 69, 71);
                AccentWash = Color.FromArgb(32, 32, 33);
                Track = Color.FromArgb(38, 38, 41);
                Good = Color.FromArgb(52, 211, 153);
                Bad = Color.FromArgb(248, 113, 113);
                OnAccent = Color.FromArgb(17, 17, 17);
            }
        }

        public static void ApplyTheme(Control root)
        {
            Form form = root as Form;
            if (form != null) form.BackColor = Bg;

            Card card = root as Card;
            if (card != null) card.BackColor = Card;

            string tag = root.Tag as string;
            if (tag == "surface") root.BackColor = Card;

            FlatInput input = root as FlatInput;
            if (input != null) input.ApplyTheme();

            Label label = root as Label;
            if (label != null && label.Tag is string) label.ForeColor = RoleColor((string)label.Tag);

            root.Invalidate();
            foreach (Control child in root.Controls) ApplyTheme(child);
        }

        public static string RoleOf(Color color)
        {
            if (color.ToArgb() == Text.ToArgb()) return "text";
            if (color.ToArgb() == Muted.ToArgb()) return "muted";
            if (color.ToArgb() == Faint.ToArgb()) return "faint";
            if (color.ToArgb() == Accent.ToArgb()) return "accent";
            if (color.ToArgb() == AccentInk.ToArgb()) return "accent";
            if (color.ToArgb() == AccentDark.ToArgb()) return "accent";
            return "text";
        }

        private static Color RoleColor(string role)
        {
            if (role == "muted") return Muted;
            if (role == "faint") return Faint;
            if (role == "accent") return Accent;
            return Text;
        }

        public static readonly Font Base = new Font("Segoe UI", 9.5f);
        public static readonly Font Small = new Font("Segoe UI", 8.75f);
        public static readonly Font Tiny = new Font("Segoe UI", 8f);
        public static readonly Font Bold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font Semi = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        public static readonly Font H2 = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        public static readonly Font H1 = new Font("Segoe UI", 15f, FontStyle.Bold);
        public static readonly Font Logo = new Font("Segoe UI", 14f, FontStyle.Bold);
        public static readonly Font Glyph = new Font("Segoe UI", 13f, FontStyle.Bold);
        public static readonly Font Arrow = new Font("Segoe UI", 19f);

        private static double DetectScale()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return Math.Max(1.0, Math.Min(3.0, g.DpiX / 96.0));
                }
            }
            catch
            {
                return 1.0;
            }
        }

        public static int P(double value)
        {
            return (int)Math.Round(value * S);
        }

        public static GraphicsPath Round(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = Math.Max(2, radius * 2);
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle rect, int radius, Color color)
        {
            using (GraphicsPath path = Round(rect, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void StrokeRound(Graphics g, Rectangle rect, int radius, Color color, float width)
        {
            using (GraphicsPath path = Round(rect, radius))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        public static void Write(Graphics g, string text, Font font, Rectangle bounds, Color color,
            TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, bounds, color, flags);
        }

        public const TextFormatFlags LeftMid =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

        public const TextFormatFlags CenterMid =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
            TextFormatFlags.SingleLine;
    }

    internal abstract class UiControl : Control
    {
        protected UiControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Ui.Base;
            Cursor = Cursors.Hand;
        }
    }

    internal sealed class Card : Panel
    {
        public Card()
        {
            BackColor = Ui.Card;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Ui.Base;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 4 && Height > 4)
            {
                using (GraphicsPath path = Ui.Round(new Rectangle(0, 0, Width, Height), Ui.P(12)))
                {
                    Region = new Region(path);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.StrokeRound(e.Graphics, r, Ui.P(12), Ui.Border, 1f);
        }
    }

    internal sealed class PillButton : UiControl
    {
        public bool Accent;
        public bool Ghost;

        private bool active;
        private bool hover;
        private bool down;

        public bool Active
        {
            get { return active; }
            set
            {
                if (active == value) return;
                active = value;
                Invalidate();
            }
        }

        public PillButton()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            down = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            down = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Ui.P(9);

            Color fill;
            Color border;
            Color fore;

            if (!Enabled)
            {
                fill = Ui.Track;
                border = Ui.Border;
                fore = Ui.Faint;
            }
            else if (Accent)
            {
                fill = hover ? Ui.AccentDark : (down ? Ui.AccentDark : Ui.Accent);
                border = fill;
                fore = Ui.OnAccent;
            }
            else if (Ghost)
            {
                fill = hover ? Ui.Track : Ui.Card;
                border = hover ? Ui.BorderStrong : Ui.Border;
                fore = Ui.Text;
            }
            else
            {
                fill = Active ? Ui.AccentSoft : (hover ? Ui.Track : Ui.Card);
                border = Active ? Ui.AccentLine : (hover ? Ui.BorderStrong : Ui.Border);
                fore = Active ? Ui.AccentInk : Ui.Muted;
            }

            if (down && Enabled && !Accent) fill = Ui.Track;

            Ui.FillRound(g, r, radius, fill);
            Ui.StrokeRound(g, r, radius, border, 1f);
            Ui.Write(g, Text, Font, r, fore, Ui.CenterMid);
        }
    }

    internal sealed class DropCard : UiControl
    {
        public bool Selected;
        public bool Highlight;
        public string FileName = "";
        public string FileMeta = "";
        public string BusyText = "";

        public event EventHandler PickFile;

        private bool hover;

        public DropCard()
        {
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            EventHandler h = PickFile;
            if (h != null) h(this, EventArgs.Empty);
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Ui.P(12);

            Ui.FillRound(g, r, radius, Ui.Card);

            using (GraphicsPath path = Ui.Round(r, radius))
            using (Pen pen = new Pen(Highlight ? Ui.Accent : (hover ? Ui.BorderStrong : Ui.Border), 1.2f))
            {
                pen.DashStyle = DashStyle.Solid;
                g.DrawPath(pen, path);
            }

            int iconSize = Ui.P(42);
            int iconX = Ui.P(20);
            int iconY = (Height - iconSize) / 2;
            Rectangle iconRect = new Rectangle(iconX, iconY, iconSize, iconSize);
            Ui.FillRound(g, iconRect, Ui.P(12), Selected ? Ui.AccentSoft : Ui.AccentWash);

            TextRenderer.DrawText(g, Selected ? "▶" : "↑", Selected ? Ui.Glyph : Ui.Arrow, iconRect,
                Ui.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            int textX = iconX + iconSize + Ui.P(16);
            int rightPad = Selected ? Ui.P(150) : Ui.P(14);

            if (Selected)
            {
                string meta = BusyText.Length > 0 ? BusyText : FileMeta;
                Ui.Write(g, FileName, Ui.Semi,
                    new Rectangle(textX, Ui.P(22), Width - textX - rightPad, Ui.P(24)),
                    Ui.Text, Ui.LeftMid);
                Ui.Write(g, meta, Ui.Small,
                    new Rectangle(textX, Ui.P(48), Width - textX - rightPad, Ui.P(20)),
                    Ui.Muted, Ui.LeftMid);

                Rectangle chip = new Rectangle(Width - Ui.P(136), (Height - Ui.P(32)) / 2, Ui.P(116), Ui.P(32));
                Ui.FillRound(g, chip, Ui.P(8), Ui.Card);
                Ui.StrokeRound(g, chip, Ui.P(8), hover ? Ui.BorderStrong : Ui.Border, 1f);
                Ui.Write(g, "Другой файл", Ui.Small, chip, Ui.Text, Ui.CenterMid);
            }
            else
            {
                Ui.Write(g, "Перетащите файл сюда", Ui.Semi,
                    new Rectangle(textX, Ui.P(12), Width - textX - rightPad, Ui.P(24)),
                    Ui.Text, Ui.LeftMid);
                Ui.Write(g, "или нажмите, чтобы выбрать", Ui.Small,
                    new Rectangle(textX, Ui.P(36), Width - textX - rightPad, Ui.P(18)),
                    Ui.Muted, Ui.LeftMid);
                Ui.Write(g, "Видео: MP4, MKV, MOV, WEBM   ·   Картинки: PNG, JPG, WEBP, BMP, TIFF", Ui.Tiny,
                    new Rectangle(textX, Ui.P(56), Width - textX - rightPad, Ui.P(16)),
                    Ui.Faint, Ui.LeftMid);
            }
        }
    }

    internal sealed class LogoBadge : Control
    {
        public LogoBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Ui.Round(r, Ui.P(10)))
            using (SolidBrush brush = new SolidBrush(Ui.Accent))
            {
                g.FillPath(brush, path);
            }
            PointF[] triangle = new PointF[]
            {
                new PointF(Width * 0.40f, Height * 0.29f),
                new PointF(Width * 0.40f, Height * 0.71f),
                new PointF(Width * 0.72f, Height * 0.50f)
            };
            using (SolidBrush ink = new SolidBrush(Ui.OnAccent))
            {
                g.FillPolygon(ink, triangle);
            }
        }
    }
}
