using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Video2Gif
{
    internal sealed partial class MainForm
    {
        private DropCard drop;
        private FlatInput startInput;
        private FlatInput durationInput;
        private Label startLabel;
        private Label durationLabel;
        private Label widthLabel;
        private Label fpsLabel;
        private Label imageNote;
        private FlatInput widthInput;
        private FlatInput fpsInput;
        private PillButton ditherBayer;
        private PillButton ditherSierra;
        private PillButton ditherNone;
        private int ditherIndex;
        private ToggleRow loopToggle;
        private ToggleRow fastToggle;
        private PillButton convertButton;
        private PillButton cancelButton;
        private PillButton themeButton;
        private PillButton downloadsButton;

        private Panel progressView;
        private Label percentLabel;
        private Label stageLabel;
        private SlimBar progressBar;
        private Label progressSub;

        private Panel resultView;
        private Panel previewHost;
        private PictureBox preview;
        private Label resultMeta;
        private PillButton saveButton;
        private PillButton openButton;

        private EmptyState emptyState;

        private static Label Lbl(string text, Font font, Color color, int x, int y, int w, int h,
            ContentAlignment align)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            label.UseMnemonic = false;
            label.TextAlign = align;
            label.Tag = Ui.RoleOf(color);
            label.SetBounds(Ui.P(x), Ui.P(y), Ui.P(w), Ui.P(h));
            return label;
        }

        private static Label Lbl(string text, Font font, Color color, int x, int y, int w, int h)
        {
            return Lbl(text, font, color, x, y, w, h, ContentAlignment.MiddleLeft);
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = Program.AppName + " " + Program.Version + " — видео и картинки в GIF";
            BackColor = Ui.Bg;
            Font = Ui.Base;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.P(960), Ui.P(580));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            LogoBadge logo = new LogoBadge();
            logo.SetBounds(Ui.P(24), Ui.P(14), Ui.P(38), Ui.P(38));
            Controls.Add(logo);

            Controls.Add(Lbl(Program.AppName, Ui.Logo, Ui.Text, 70, 10, 240, 28));
            Controls.Add(Lbl("видео и картинки → GIF",
                Ui.Small, Ui.Muted, 71, 36, 300, 18));
            Controls.Add(Lbl("v" + Program.Version + " · локально", Ui.Tiny, Ui.Faint,
                460, 24, 240, 18, ContentAlignment.MiddleRight));

            downloadsButton = new PillButton();
            downloadsButton.Ghost = true;
            downloadsButton.Font = Ui.Small;
            downloadsButton.Text = "Скачать";
            downloadsButton.SetBounds(Ui.P(712), Ui.P(16), Ui.P(104), Ui.P(32));
            downloadsButton.Click += delegate { ShowDownloadMenu(); };
            Controls.Add(downloadsButton);

            themeButton = new PillButton();
            themeButton.Ghost = true;
            themeButton.Font = Ui.Small;
            themeButton.Text = Ui.Dark ? "Светлая" : "Тёмная";
            themeButton.SetBounds(Ui.P(832), Ui.P(16), Ui.P(104), Ui.P(32));
            themeButton.Click += delegate { ToggleTheme(); };
            Controls.Add(themeButton);

            Controls.Add(Lbl("by DeepSeek v4.1 flash & arsikfox14 & dismasdismavich", Ui.Tiny, Ui.Faint,
                24, 560, 912, 14, ContentAlignment.MiddleRight));

            drop = new DropCard();
            drop.SetBounds(Ui.P(24), Ui.P(68), Ui.P(912), Ui.P(92));
            drop.PickFile += delegate { PickFile(); };
            Controls.Add(drop);

            Card settings = new Card();
            settings.SetBounds(Ui.P(24), Ui.P(172), Ui.P(560), Ui.P(376));
            Controls.Add(settings);

            settings.Controls.Add(Lbl("Настройки", Ui.H2, Ui.Text, 18, 12, 300, 26));

            startLabel = Lbl("Начало, с", Ui.Small, Ui.Muted, 18, 54, 128, 18);
            settings.Controls.Add(startLabel);
            durationLabel = Lbl("Длительность, с", Ui.Small, Ui.Muted, 150, 54, 128, 18);
            settings.Controls.Add(durationLabel);
            widthLabel = Lbl("Ширина (0=ориг.)", Ui.Small, Ui.Muted, 282, 54, 128, 18);
            settings.Controls.Add(widthLabel);
            fpsLabel = Lbl("Кадры/с · 1–60", Ui.Small, Ui.Muted, 414, 54, 128, 18);
            settings.Controls.Add(fpsLabel);

            imageNote = Lbl("Для картинки время и кадры\nне нужны: получится GIF\nиз одного кадра.",
                Ui.Small, Ui.Muted, 18, 52, 252, 52);
            imageNote.Visible = false;
            settings.Controls.Add(imageNote);

            startInput = new FlatInput();
            startInput.SetBounds(Ui.P(18), Ui.P(72), Ui.P(128), Ui.P(34));
            startInput.Value = "0";
            settings.Controls.Add(startInput);

            durationInput = new FlatInput();
            durationInput.SetBounds(Ui.P(150), Ui.P(72), Ui.P(128), Ui.P(34));
            durationInput.Placeholder = "весь клип";
            settings.Controls.Add(durationInput);

            widthInput = new FlatInput();
            widthInput.SetBounds(Ui.P(282), Ui.P(72), Ui.P(128), Ui.P(34));
            widthInput.Value = "480";
            settings.Controls.Add(widthInput);

            fpsInput = new FlatInput();
            fpsInput.SetBounds(Ui.P(414), Ui.P(72), Ui.P(128), Ui.P(34));
            fpsInput.Value = "30";
            settings.Controls.Add(fpsInput);

            settings.Controls.Add(Lbl("Сглаживание переходов", Ui.Small, Ui.Muted, 18, 126, 300, 18));

            ditherBayer = new PillButton();
            ditherBayer.Text = "Байер — компактнее";
            ditherBayer.Font = Ui.Small;
            ditherBayer.SetBounds(Ui.P(18), Ui.P(146), Ui.P(169), Ui.P(34));
            settings.Controls.Add(ditherBayer);

            ditherSierra = new PillButton();
            ditherSierra.Text = "Sierra — плавнее";
            ditherSierra.Font = Ui.Small;
            ditherSierra.SetBounds(Ui.P(195), Ui.P(146), Ui.P(169), Ui.P(34));
            settings.Controls.Add(ditherSierra);

            ditherNone = new PillButton();
            ditherNone.Text = "Без сглаживания";
            ditherNone.Font = Ui.Small;
            ditherNone.SetBounds(Ui.P(372), Ui.P(146), Ui.P(170), Ui.P(34));
            settings.Controls.Add(ditherNone);

            ditherBayer.Click += delegate { SetDither(0); };
            ditherSierra.Click += delegate { SetDither(1); };
            ditherNone.Click += delegate { SetDither(2); };
            SetDither(1);

            loopToggle = new ToggleRow();
            loopToggle.SetBounds(Ui.P(18), Ui.P(208), Ui.P(524), Ui.P(38));
            loopToggle.Title = "Бесконечный цикл";
            loopToggle.Subtitle = "GIF будет повторяться без остановки";
            loopToggle.Checked = true;
            settings.Controls.Add(loopToggle);

            fastToggle = new ToggleRow();
            fastToggle.SetBounds(Ui.P(18), Ui.P(246), Ui.P(524), Ui.P(38));
            fastToggle.Title = "Быстрый режим";
            fastToggle.Subtitle = "Один проход вместо двух: быстрее, но палитра чуть беднее";
            settings.Controls.Add(fastToggle);

            convertButton = new PillButton();
            convertButton.Accent = true;
            convertButton.Text = "Сделать GIF";
            convertButton.Font = Ui.Bold;
            convertButton.SetBounds(Ui.P(18), Ui.P(304), Ui.P(254), Ui.P(44));
            convertButton.Click += delegate { StartConversion(); };
            settings.Controls.Add(convertButton);

            cancelButton = new PillButton();
            cancelButton.Ghost = true;
            cancelButton.Text = "Отменить";
            cancelButton.SetBounds(Ui.P(288), Ui.P(304), Ui.P(254), Ui.P(44));
            cancelButton.Visible = false;
            cancelButton.Click += delegate { CancelConversion(); };
            settings.Controls.Add(cancelButton);

            Card resultCard = new Card();
            resultCard.SetBounds(Ui.P(600), Ui.P(172), Ui.P(336), Ui.P(376));
            Controls.Add(resultCard);

            resultCard.Controls.Add(Lbl("Результат", Ui.H2, Ui.Text, 18, 12, 300, 26));

            emptyState = new EmptyState();
            emptyState.SetBounds(Ui.P(0), Ui.P(40), Ui.P(336), Ui.P(336));
            resultCard.Controls.Add(emptyState);

            progressView = new Panel();
            progressView.BackColor = Color.Transparent;
            progressView.SetBounds(Ui.P(0), Ui.P(40), Ui.P(336), Ui.P(336));
            progressView.Visible = false;
            resultCard.Controls.Add(progressView);

            percentLabel = Lbl("0%", new Font("Segoe UI", 30f, FontStyle.Bold), Ui.Accent,
                18, 102, 300, 60, ContentAlignment.MiddleCenter);
            progressView.Controls.Add(percentLabel);

            stageLabel = Lbl("Готовимся…", Ui.Base, Ui.Muted, 18, 166, 300, 22,
                ContentAlignment.MiddleCenter);
            progressView.Controls.Add(stageLabel);

            progressBar = new SlimBar();
            progressBar.SetBounds(Ui.P(18), Ui.P(200), Ui.P(300), Ui.P(8));
            progressView.Controls.Add(progressBar);

            progressSub = Lbl("", Ui.Tiny, Ui.Faint, 18, 216, 300, 20,
                ContentAlignment.MiddleCenter);
            progressView.Controls.Add(progressSub);

            resultView = new Panel();
            resultView.BackColor = Color.Transparent;
            resultView.SetBounds(Ui.P(0), Ui.P(40), Ui.P(336), Ui.P(336));
            resultView.Visible = false;
            resultCard.Controls.Add(resultView);

            previewHost = new Panel();
            previewHost.SetBounds(Ui.P(18), Ui.P(8), Ui.P(300), Ui.P(228));
            previewHost.BackColor = Ui.Card;
            previewHost.Tag = "surface";
            previewHost.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, previewHost.Width - 1, previewHost.Height - 1);
                Ui.StrokeRound(e.Graphics, r, Ui.P(10), Ui.Border, 1f);
            };
            resultView.Controls.Add(previewHost);

            preview = new PictureBox();
            preview.SetBounds(Ui.P(6), Ui.P(6), Ui.P(288), Ui.P(216));
            preview.SizeMode = PictureBoxSizeMode.Zoom;
            preview.BackColor = Ui.Card;
            preview.Tag = "surface";
            previewHost.Controls.Add(preview);

            resultMeta = Lbl("", Ui.Small, Ui.Muted, 18, 244, 300, 20,
                ContentAlignment.MiddleCenter);
            resultView.Controls.Add(resultMeta);

            saveButton = new PillButton();
            saveButton.Accent = true;
            saveButton.Text = "Сохранить GIF";
            saveButton.Font = Ui.Bold;
            saveButton.SetBounds(Ui.P(18), Ui.P(272), Ui.P(145), Ui.P(42));
            saveButton.Click += delegate { SaveResult(); };
            resultView.Controls.Add(saveButton);

            openButton = new PillButton();
            openButton.Ghost = true;
            openButton.Text = "Открыть";
            openButton.SetBounds(Ui.P(173), Ui.P(272), Ui.P(145), Ui.P(42));
            openButton.Click += delegate { OpenResult(); };
            resultView.Controls.Add(openButton);

            ResumeLayout(false);
        }

        private void ShowDownloadMenu()
        {
            if (busy) return;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Font = Ui.Base;
            menu.BackColor = Ui.Card;
            menu.ForeColor = Ui.Text;
            menu.Renderer = new MenuRenderer();

#if !NOTIKTOK
            AddMenuItem(menu, "TikTok", delegate { OpenTikTok(); });
#endif
            AddMenuItem(menu, "YouTube и другие сайты",
                delegate { OpenMediaDownload(false); });
            AddMenuItem(menu, "Музыка из Spotify",
                delegate { OpenMediaDownload(true); });

            menu.Show(downloadsButton, new Point(0, downloadsButton.Height + Ui.P(4)));
        }

        private static void AddMenuItem(ContextMenuStrip menu, string text, EventHandler click)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ForeColor = Ui.Text;
            item.BackColor = Ui.Card;
            item.Padding = new Padding(Ui.P(10), Ui.P(5), Ui.P(10), Ui.P(5));
            item.Click += click;
            menu.Items.Add(item);
        }

        private void OpenMediaDownload(bool spotify)
        {
            if (busy) return;
            using (MediaDownloadForm dialog = new MediaDownloadForm(spotify))
            {
                dialog.ShowDialog(this);
            }
        }

        private sealed class MenuRenderer : ToolStripProfessionalRenderer
        {
            public MenuRenderer() : base(new MenuColors()) { }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (SolidBrush brush = new SolidBrush(Ui.Card))
                {
                    e.Graphics.FillRectangle(brush, e.AffectedBounds);
                }
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Selected) return;
                using (SolidBrush brush = new SolidBrush(Ui.Track))
                {
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
                }
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Ui.Text : Ui.Faint;
                base.OnRenderItemText(e);
            }
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Ui.Card; } }
            public override Color MenuBorder { get { return Ui.Border; } }
            public override Color MenuItemBorder { get { return Ui.Border; } }
            public override Color MenuItemSelected { get { return Ui.Track; } }
            public override Color ImageMarginGradientBegin { get { return Ui.Card; } }
            public override Color ImageMarginGradientMiddle { get { return Ui.Card; } }
            public override Color ImageMarginGradientEnd { get { return Ui.Card; } }
            public override Color SeparatorDark { get { return Ui.Border; } }
            public override Color SeparatorLight { get { return Ui.Border; } }
        }

    }

    internal sealed class EmptyState : UiControl
    {
        public EmptyState()
        {
            Cursor = Cursors.Default;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = Width / 2;
            int top = Ui.P(74);
            int bw = Ui.P(64);
            int bh = Ui.P(50);
            Rectangle box = new Rectangle(cx - bw / 2, top, bw, bh);
            Ui.FillRound(g, box, Ui.P(14), Ui.AccentSoft);

            Rectangle frame = new Rectangle(box.X + Ui.P(15), box.Y + Ui.P(12),
                box.Width - Ui.P(30), box.Height - Ui.P(24));
            using (Pen pen = new Pen(Ui.Accent, 1.6f))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawRectangle(pen, frame);
                g.DrawEllipse(pen, frame.X + Ui.P(3), frame.Y + Ui.P(3), Ui.P(6), Ui.P(6));
                Point[] hills = new Point[]
                {
                    new Point(frame.X + Ui.P(2), frame.Bottom - Ui.P(3)),
                    new Point(frame.X + frame.Width / 2, frame.Y + frame.Height / 2 + Ui.P(2)),
                    new Point(frame.Right - Ui.P(3), frame.Bottom - Ui.P(3))
                };
                g.DrawLines(pen, hills);
            }

            int textTop = top + bh + Ui.P(24);
            Ui.Write(g, "Здесь появится GIF", Ui.Semi,
                new Rectangle(Ui.P(16), textTop, Width - Ui.P(32), Ui.P(24)),
                Ui.Text, Ui.CenterMid);
            Ui.Write(g, "Настройте параметры и нажмите «Сделать GIF»", Ui.Small,
                new Rectangle(Ui.P(16), textTop + Ui.P(26), Width - Ui.P(32), Ui.P(18)),
                Ui.Muted, Ui.CenterMid);
        }
    }
}
