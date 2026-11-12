using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Video2Gif
{
    internal enum IconKind
    {
        Mouse,
        Download,
        Settings
    }

    internal sealed class IconButton : UiControl
    {
        public readonly IconKind Kind;

        private bool hover;
        private bool down;

        public IconButton(IconKind kind)
        {
            Kind = kind;
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = !Enabled ? Ui.Track : (hover ? Ui.Track : Ui.Card);
            Color border = !Enabled ? Ui.Border : (hover ? Ui.BorderStrong : Ui.Border);
            Color ink = !Enabled ? Ui.Faint : (hover ? Ui.Text : Ui.Muted);
            if (down && Enabled) fill = Ui.AccentSoft;

            Ui.FillRound(g, r, Ui.P(9), fill);
            Ui.StrokeRound(g, r, Ui.P(9), border, 1f);

            float cx = Width / 2f;
            float cy = Height / 2f;
            float unit = Math.Min(Width, Height) * 0.30f;

            using (Pen pen = new Pen(ink, Math.Max(1.4f, Ui.P(1.5))))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                if (Kind == IconKind.Mouse)
                {
                    float w = unit * 1.02f;
                    float h = unit * 1.58f;
                    Rectangle body = Rectangle.Round(
                        new RectangleF(cx - w / 2f, cy - h / 2f, w, h));
                    using (GraphicsPath path = Ui.Round(body, (int)Math.Max(4, w / 2f)))
                    {
                        g.DrawPath(pen, path);
                    }

                    float top = cy - h / 2f;
                    g.DrawLine(pen, cx, top + 1, cx, cy - unit * 0.06f);
                    g.DrawLine(pen, cx - unit * 0.18f, top + h * 0.30f,
                        cx + unit * 0.18f, top + h * 0.30f);
                }
                else if (Kind == IconKind.Download)
                {
                    float top = cy - unit * 0.85f;
                    float bottom = cy + unit * 0.42f;
                    g.DrawLine(pen, cx, top, cx, bottom);
                    g.DrawLine(pen, cx - unit * 0.52f, bottom - unit * 0.44f, cx, bottom);
                    g.DrawLine(pen, cx + unit * 0.52f, bottom - unit * 0.44f, cx, bottom);

                    float tray = unit * 0.62f;
                    float trayY = cy + unit * 0.85f;
                    g.DrawLine(pen, cx - tray, trayY - unit * 0.28f, cx - tray, trayY);
                    g.DrawLine(pen, cx - tray, trayY, cx + tray, trayY);
                    g.DrawLine(pen, cx + tray, trayY - unit * 0.28f, cx + tray, trayY);
                }
                else
                {
                    float outer = unit * 0.98f;
                    float inner = unit * 0.70f;
                    PointF[] gear = new PointF[32];
                    for (int i = 0; i < 32; i++)
                    {
                        double a = -Math.PI / 2 + Math.PI * 2 / 32 * i;
                        float radius = (i % 4 < 2) ? outer : inner;
                        gear[i] = new PointF(
                            cx + (float)(Math.Cos(a) * radius),
                            cy + (float)(Math.Sin(a) * radius));
                    }
                    using (SolidBrush inkBrush = new SolidBrush(ink))
                    {
                        g.FillPolygon(inkBrush, gear);
                    }
                    float hole = unit * 0.30f;
                    using (SolidBrush holeBrush = new SolidBrush(fill))
                    {
                        g.FillEllipse(holeBrush, cx - hole, cy - hole, hole * 2, hole * 2);
                    }
                }
            }
        }
    }

    internal sealed class ToggleRow : UiControl
    {
        private bool isChecked;

        public string Title = "";
        public string Subtitle = "";

        public event EventHandler CheckedChanged;

        public ToggleRow()
        {
            Height = Ui.P(36);
        }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Invalidate();
                EventHandler h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int box = Ui.P(18);
            int y = Subtitle.Length > 0 ? Ui.P(2) : (Height - box) / 2;
            Rectangle r = new Rectangle(0, y, box, box);
            if (isChecked)
            {
                Ui.FillRound(g, r, Ui.P(5), Ui.Accent);
                Point[] check = new Point[]
                {
                    new Point(r.X + Ui.P(4), r.Y + Ui.P(9)),
                    new Point(r.X + Ui.P(8), r.Y + Ui.P(13)),
                    new Point(r.X + Ui.P(14), r.Y + Ui.P(5))
                };
                using (Pen pen = new Pen(Ui.OnAccent, Ui.P(2)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, check);
                }
            }
            else
            {
                Ui.FillRound(g, r, Ui.P(5), Ui.Card);
                Ui.StrokeRound(g, r, Ui.P(5), Ui.BorderStrong, 1.2f);
            }

            int tx = box + Ui.P(10);
            if (Subtitle.Length > 0)
            {
                Ui.Write(g, Title, Ui.Base, new Rectangle(tx, Ui.P(-1), Width - tx, Ui.P(20)), Ui.Text, Ui.LeftMid);
                Ui.Write(g, Subtitle, Ui.Tiny, new Rectangle(tx, Ui.P(18), Width - tx, Ui.P(16)), Ui.Faint, Ui.LeftMid);
            }
            else
            {
                Ui.Write(g, Title, Ui.Base, new Rectangle(tx, 0, Width - tx, Height), Ui.Text, Ui.LeftMid);
            }
        }
    }

    internal sealed class ColorSlider : UiControl
    {
        private int value = 256;
        private bool dragging;

        public int Minimum = 2;
        public int Maximum = 256;
        public int Step = 2;

        public event EventHandler ValueChanged;

        public ColorSlider()
        {
            Height = Ui.P(22);
        }

        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(Minimum, Math.Min(Maximum, value));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
                EventHandler h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        private int ThumbX
        {
            get
            {
                int pad = Ui.P(8);
                double ratio = (double)(value - Minimum) / Math.Max(1, Maximum - Minimum);
                return pad + (int)Math.Round(ratio * (Width - pad * 2));
            }
        }

        private void SetFromX(int x)
        {
            int pad = Ui.P(8);
            double ratio = (double)(x - pad) / Math.Max(1, Width - pad * 2);
            ratio = Math.Max(0, Math.Min(1, ratio));
            int raw = Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
            int steps = (int)Math.Round((double)(raw - Minimum) / Step);
            int v = Minimum + steps * Step;
            Value = v;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                SetFromX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) SetFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = Height / 2;
            int pad = Ui.P(8);
            Rectangle track = new Rectangle(pad, cy - Ui.P(2), Width - pad * 2, Ui.P(4));
            Ui.FillRound(g, track, Ui.P(2), Ui.Track);

            int tx = ThumbX;
            Rectangle fill = new Rectangle(track.X, track.Y, Math.Max(0, tx - track.X), track.Height);
            if (fill.Width > 2)
            {
                Ui.FillRound(g, fill, Ui.P(2), Ui.Accent);
            }

            int thumb = Ui.P(14);
            Rectangle knob = new Rectangle(tx - thumb / 2, cy - thumb / 2, thumb, thumb);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            {
                g.FillEllipse(shadow, knob.X, knob.Y + Ui.P(1), knob.Width, knob.Height);
            }
            using (SolidBrush knobBrush = new SolidBrush(Ui.Card))
            {
                g.FillEllipse(knobBrush, knob);
            }
            using (Pen pen = new Pen(Ui.Accent, Ui.P(2)))
            {
                g.DrawEllipse(pen, knob.X + 1, knob.Y + 1, knob.Width - 2, knob.Height - 2);
            }
        }
    }

    internal sealed class SlimBar : UiControl
    {
        private int value;

        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(0, Math.Min(100, value));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
            }
        }

        public SlimBar()
        {
            Height = Ui.P(8);
            Cursor = Cursors.Default;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Width, Height);
            Ui.FillRound(g, track, Height / 2, Ui.Track);
            int w = (int)Math.Round(Width * (value / 100.0));
            if (w > 0)
            {
                Rectangle fill = new Rectangle(0, 0, Math.Max(Height, w), Height);
                using (GraphicsPath path = Ui.Round(fill, Height / 2))
                using (SolidBrush brush = new SolidBrush(Ui.Accent))
                {
                    g.FillPath(brush, path);
                }
            }
        }
    }

    internal sealed class FlatInput : Panel
    {
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public readonly TextBox Box;
        private string placeholder = "";
        private bool focused;

        public FlatInput()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Card;

            Box = new TextBox();
            Box.BorderStyle = BorderStyle.None;
            Box.Font = Ui.Base;
            Box.BackColor = Ui.Card;
            Box.ForeColor = Ui.Text;
            Box.TextAlign = HorizontalAlignment.Left;
            Controls.Add(Box);

            Box.GotFocus += delegate { focused = true; Invalidate(); };
            Box.LostFocus += delegate { focused = false; Invalidate(); };
            Box.HandleCreated += delegate { ApplyPlaceholder(); };
            Cursor = Cursors.IBeam;
        }

        public string Value
        {
            get { return Box.Text; }
            set { Box.Text = value; }
        }

        public string Placeholder
        {
            get { return placeholder; }
            set
            {
                placeholder = value ?? "";
                ApplyPlaceholder();
            }
        }

        public void ApplyTheme()
        {
            BackColor = Ui.Card;
            Box.BackColor = Ui.Card;
            Box.ForeColor = Ui.Text;
            Invalidate();
        }

        private void ApplyPlaceholder()
        {
            if (Box.IsHandleCreated && placeholder.Length > 0)
            {
                SendMessage(Box.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 4 && Height > 4)
            {
                using (GraphicsPath path = Ui.Round(new Rectangle(0, 0, Width, Height), Ui.P(9)))
                {
                    Region = new Region(path);
                }
            }
            int pad = Ui.P(11);
            Box.SetBounds(pad, Math.Max(0, (Height - Box.Height) / 2), Math.Max(10, Width - pad * 2), Box.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.StrokeRound(e.Graphics, r, Ui.P(9), focused ? Ui.AccentLine : Ui.Border, focused ? 1.6f : 1f);
        }
    }
}
