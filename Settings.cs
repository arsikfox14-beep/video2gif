using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Video2Gif
{
    /* --------------------------------------------------------------- Плитка темы */

    internal sealed class ThemeTile : UiControl
    {
        public string ThemeKey = "dark";
        public string Title = "";
        public Color SwatchBg;
        public Color SwatchCard;
        public Color SwatchAccent;

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

        public ThemeTile()
        {
            TabStop = false;
        }

        public void Pick()
        {
            OnClick(EventArgs.Empty);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Active ? Ui.AccentSoft : (hover ? Ui.Track : Ui.Card);
            if (down) fill = Ui.Track;
            Color border = Active ? Ui.Accent : (hover ? Ui.BorderStrong : Ui.Border);
            Ui.FillRound(g, r, Ui.P(10), fill);
            Ui.StrokeRound(g, r, Ui.P(10), border, Active ? 1.5f : 1f);

            int d = Ui.P(16);
            int x = Ui.P(14);
            int y = Ui.P(13);
            DrawSwatch(g, new Rectangle(x, y, d, d), SwatchBg, Ui.Border);
            DrawSwatch(g, new Rectangle(x + Ui.P(19), y, d, d), SwatchCard, Ui.Border);
            DrawSwatch(g, new Rectangle(x + Ui.P(38), y, d, d), SwatchAccent, Ui.Border);

            Ui.Write(g, Title, Ui.Bold,
                new Rectangle(Ui.P(14), Ui.P(38), Width - Ui.P(28), Ui.P(18)),
                Active ? Ui.Text : Ui.Muted, Ui.LeftMid);

            if (Active)
            {
                using (Pen pen = new Pen(Ui.Accent, Math.Max(1.6f, Ui.P(1.6))))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    int cxp = Width - Ui.P(20);
                    int cyp = Ui.P(21);
                    g.DrawLine(pen, cxp - Ui.P(5), cyp, cxp - Ui.P(1), cyp + Ui.P(4));
                    g.DrawLine(pen, cxp - Ui.P(1), cyp + Ui.P(4), cxp + Ui.P(6), cyp - Ui.P(4));
                }
            }
        }

        private static void DrawSwatch(Graphics g, Rectangle r, Color color, Color border)
        {
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, r);
            }
            using (Pen pen = new Pen(border, 1f))
            {
                g.DrawEllipse(pen, r);
            }
        }
    }

    /* ------------------------------------------------------------------ Окно */

    internal sealed class SettingsForm : Form
    {
        private readonly Action<string> pick;
        private readonly ThemeTile autoTile;
        private readonly ThemeTile lightTile;
        private readonly ThemeTile darkTile;
        private readonly ThemeTile newYearTile;
        private readonly ThemeTile halloweenTile;

        public SettingsForm(Action<string> pickTheme)
        {
            pick = pickTheme;

            Text = "Настройки — " + Program.AppName;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            ClientSize = new Size(Ui.P(428), Ui.P(336));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Controls.Add(Lbl("Настройки", Ui.H2, Ui.Text, 24, 14, 300, 26));
            Controls.Add(Lbl("Тема оформления", Ui.Small, Ui.Muted, 24, 44, 300, 18));

            autoTile = MakeTile("auto", "Авто");
            autoTile.SwatchBg = Color.FromArgb(247, 247, 248);
            autoTile.SwatchCard = Color.FromArgb(14, 14, 15);
            ThemeKind seasonal = Ui.SeasonalTheme(DateTime.Now);
            autoTile.SwatchAccent = seasonal == ThemeKind.Halloween
                ? Color.FromArgb(255, 140, 66)
                : seasonal == ThemeKind.NewYear
                    ? Color.FromArgb(126, 200, 255)
                    : Color.FromArgb(126, 126, 132);
            autoTile.SetBounds(Ui.P(24), Ui.P(68), Ui.P(120), Ui.P(64));
            autoTile.Click += delegate { Choose("auto"); };
            Controls.Add(autoTile);

            lightTile = MakeTile("light", "Светлая");
            lightTile.SwatchBg = Color.FromArgb(247, 247, 248);
            lightTile.SwatchCard = Color.White;
            lightTile.SwatchAccent = Color.FromArgb(23, 23, 23);
            lightTile.SetBounds(Ui.P(154), Ui.P(68), Ui.P(120), Ui.P(64));
            lightTile.Click += delegate { Choose("light"); };
            Controls.Add(lightTile);

            darkTile = MakeTile("dark", "Тёмная");
            darkTile.SwatchBg = Color.FromArgb(14, 14, 15);
            darkTile.SwatchCard = Color.FromArgb(24, 24, 26);
            darkTile.SwatchAccent = Color.FromArgb(242, 242, 243);
            darkTile.SetBounds(Ui.P(284), Ui.P(68), Ui.P(120), Ui.P(64));
            darkTile.Click += delegate { Choose("dark"); };
            Controls.Add(darkTile);

            newYearTile = MakeTile("newyear", "Новый год");
            newYearTile.SwatchBg = Color.FromArgb(9, 21, 38);
            newYearTile.SwatchCard = Color.FromArgb(16, 34, 56);
            newYearTile.SwatchAccent = Color.FromArgb(126, 200, 255);
            newYearTile.SetBounds(Ui.P(24), Ui.P(140), Ui.P(184), Ui.P(64));
            newYearTile.Click += delegate { Choose("newyear"); };
            Controls.Add(newYearTile);

            halloweenTile = MakeTile("halloween", "Хэллоуин");
            halloweenTile.SwatchBg = Color.FromArgb(18, 12, 24);
            halloweenTile.SwatchCard = Color.FromArgb(29, 19, 38);
            halloweenTile.SwatchAccent = Color.FromArgb(255, 140, 66);
            halloweenTile.SetBounds(Ui.P(220), Ui.P(140), Ui.P(184), Ui.P(64));
            halloweenTile.Click += delegate { Choose("halloween"); };
            Controls.Add(halloweenTile);

            Controls.Add(Lbl("Авто по дате Windows: Хэллоуин — 1 октября–1 ноября,\n" +
                "Новый год — 1 декабря–1 января.", Ui.Tiny, Ui.Faint,
                24, 212, 380, 32));

            Controls.Add(Lbl("Тема применяется сразу и запоминается.", Ui.Tiny, Ui.Faint,
                24, 250, 380, 18));

            PillButton done = new PillButton();
            done.Accent = true;
            done.Text = "Готово";
            done.Font = Ui.Bold;
            done.SetBounds(Ui.P(300), Ui.P(284), Ui.P(104), Ui.P(36));
            done.Click += delegate { Close(); };
            Controls.Add(done);

            RefreshActive();
        }

        private static ThemeTile MakeTile(string key, string title)
        {
            ThemeTile tile = new ThemeTile();
            tile.ThemeKey = key;
            tile.Title = title;
            return tile;
        }

        private void Choose(string name)
        {
            if (pick != null) pick(name);
            RefreshActive();
            Ui.ApplyTheme(this);
            ApplyTitleBar();
            Invalidate(true);
        }

        private void RefreshActive()
        {
            string current = Ui.ThemeName;
            autoTile.Active = Ui.AutoTheme;
            lightTile.Active = !Ui.AutoTheme && current == "light";
            darkTile.Active = !Ui.AutoTheme && current == "dark";
            newYearTile.Active = !Ui.AutoTheme && current == "newyear";
            halloweenTile.Active = !Ui.AutoTheme && current == "halloween";
        }

        private void ApplyTitleBar()
        {
            if (!IsHandleCreated) return;
            Ui.ApplyTitleBar(Handle);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTitleBar();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        /* Локальная копия помощника, чтобы не дублировать разметку. */
        private static Label Lbl(string text, Font font, Color color, int x, int y, int w, int h)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            label.UseMnemonic = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Tag = Ui.RoleOf(color);
            label.SetBounds(Ui.P(x), Ui.P(y), Ui.P(w), Ui.P(h));
            return label;
        }
    }
}
