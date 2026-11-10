using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Video2Gif
{
    internal enum SnakeStep
    {
        Moved,
        Ate,
        HitWall,
        HitSelf
    }

    /* ------------------------------------------------------------------ Логика */

    internal sealed class SnakeGame
    {
        public readonly int Cols;
        public readonly int Rows;
        public readonly List<Point> Snake = new List<Point>();
        public readonly Random Rng = new Random();

        public Point Food;
        public Point Direction;
        public int Score;
        public bool Alive;

        private Point nextDirection;

        public SnakeGame(int cols, int rows)
        {
            Cols = Math.Max(8, cols);
            Rows = Math.Max(8, rows);
            Reset();
        }

        public void Reset()
        {
            Reset(new Point(1, 0));
        }

        public void Reset(Point direction)
        {
            if (Math.Abs(direction.X) + Math.Abs(direction.Y) != 1) direction = new Point(1, 0);

            Snake.Clear();
            int cx = Cols / 2;
            int cy = Rows / 2;
            Snake.Add(new Point(cx, cy));
            Snake.Add(new Point(cx - direction.X, cy - direction.Y));
            Snake.Add(new Point(cx - direction.X * 2, cy - direction.Y * 2));

            Direction = direction;
            nextDirection = direction;
            Score = 0;
            Alive = true;
            PlaceFood();
        }

        public void Turn(int dx, int dy)
        {
            if (!Alive) return;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return;
            if (dx == -Direction.X && dy == -Direction.Y) return;
            nextDirection = new Point(dx, dy);
        }

        public SnakeStep Step()
        {
            if (!Alive) return SnakeStep.HitWall;

            Direction = nextDirection;
            Point head = Snake[0];
            Point next = new Point(head.X + Direction.X, head.Y + Direction.Y);

            if (next.X < 0 || next.Y < 0 || next.X >= Cols || next.Y >= Rows)
            {
                Alive = false;
                return SnakeStep.HitWall;
            }

            bool eating = next.X == Food.X && next.Y == Food.Y;
            int limit = eating ? Snake.Count : Snake.Count - 1;
            for (int i = 0; i < limit; i++)
            {
                if (Snake[i] == next)
                {
                    Alive = false;
                    return SnakeStep.HitSelf;
                }
            }

            Snake.Insert(0, next);
            if (eating)
            {
                Score++;
                PlaceFood();
                return SnakeStep.Ate;
            }
            Snake.RemoveAt(Snake.Count - 1);
            return SnakeStep.Moved;
        }

        public void PlaceFood()
        {
            List<Point> free = new List<Point>();
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                {
                    Point p = new Point(x, y);
                    if (!Snake.Contains(p)) free.Add(p);
                }
            }
            Food = free.Count > 0 ? free[Rng.Next(free.Count)] : new Point(-1, -1);
        }
    }

    /* ------------------------------------------------------------------ Кнопка */

    internal sealed class GamepadButton : UiControl
    {
        private bool hover;
        private bool down;

        public GamepadButton()
        {
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

            if (down && Enabled) fill = Ui.Track;
            Ui.FillRound(g, r, Ui.P(9), fill);
            Ui.StrokeRound(g, r, Ui.P(9), border, 1f);

            Rectangle body = new Rectangle(Ui.P(8), Ui.P(9), Width - Ui.P(16), Height - Ui.P(18));
            using (Pen pen = new Pen(ink, 1.6f))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                using (GraphicsPath path = Ui.Round(body, body.Height / 2))
                {
                    g.DrawPath(pen, path);
                }

                int cy = body.Y + body.Height / 2;
                int cx = body.X + body.Width / 3;
                g.DrawLine(pen, cx - Ui.P(3), cy, cx + Ui.P(3), cy);
                g.DrawLine(pen, cx, cy - Ui.P(3), cx, cy + Ui.P(3));

                int bx = body.Right - Ui.P(12);
                g.DrawLine(pen, bx, cy - Ui.P(2), bx + Ui.P(3), cy + Ui.P(1));
                g.DrawLine(pen, bx + Ui.P(3), cy - Ui.P(2), bx, cy + Ui.P(1));
            }
        }
    }

    /* ------------------------------------------------------------------ Окно */

    internal sealed class SnakeForm : Form
    {
        private const int Cols = 24;
        private const int Rows = 18;

        private readonly SnakeGame game = new SnakeGame(Cols, Rows);
        private readonly Timer timer = new Timer();
        private readonly int cell;
        private readonly Rectangle board;
        private bool started;
        private bool paused;
        private int best;

        public SnakeForm()
        {
            cell = Ui.P(24);
            board = new Rectangle(Ui.P(24), Ui.P(76), Cols * cell, Rows * cell);

            Text = "Змейка — " + Program.AppName;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            ClientSize = new Size(board.Right + Ui.P(24), board.Bottom + Ui.P(24));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            best = LoadBest();

            timer.Interval = Speed;
            timer.Tick += delegate { Tick(); };
            Deactivate += delegate { if (started && !paused) Pause(); };
        }

        private int Speed
        {
            get { return Math.Max(70, 145 - game.Score * 3); }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int value = Ui.Dark ? 1 : 0;
            try { DwmSetWindowAttribute(Handle, 20, ref value, sizeof(int)); } catch { }
            try { DwmSetWindowAttribute(Handle, 19, ref value, sizeof(int)); } catch { }
        }

        private static string BestPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Video2Gif", "snake-best.txt");
            }
        }

        private static int LoadBest()
        {
            try
            {
                string path = BestPath;
                if (!File.Exists(path)) return 0;
                int value;
                return int.TryParse(File.ReadAllText(path).Trim(), out value) && value > 0 ? value : 0;
            }
            catch { return 0; }
        }

        private void SaveBest()
        {
            try
            {
                string path = BestPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, best.ToString());
            }
            catch { }
        }

        /* ------------------------------------------------------------ Управление */

        private void BeginRun()
        {
            started = true;
            paused = false;
            timer.Interval = Speed;
            timer.Start();
            Invalidate();
        }

        private void Pause()
        {
            if (!started || paused) return;
            paused = true;
            timer.Stop();
            Invalidate();
        }

        private void Resume()
        {
            if (!started || !paused) return;
            paused = false;
            timer.Interval = Speed;
            timer.Start();
            Invalidate();
        }

        private void Restart(int dx, int dy)
        {
            if (dx != 0 || dy != 0) game.Reset(new Point(dx, dy));
            else game.Reset();
            BeginRun();
        }

        private void Tick()
        {
            if (!started || paused) return;

            SnakeStep result = game.Step();
            if (result == SnakeStep.Ate)
            {
                if (game.Score > best)
                {
                    best = game.Score;
                    SaveBest();
                }
                timer.Interval = Speed;
            }
            else if (result == SnakeStep.HitWall || result == SnakeStep.HitSelf)
            {
                started = false;
                timer.Stop();
                if (game.Score > best) best = game.Score;
                SaveBest();
            }
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                if (!game.Alive) Restart(0, 0);
                else if (!started) BeginRun();
                else if (paused) Resume();
                else Pause();
                e.Handled = true;
                return;
            }

            int dx = 0;
            int dy = 0;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) dx = -1;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) dx = 1;
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.W) dy = -1;
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.S) dy = 1;

            if (dx != 0 || dy != 0)
            {
                if (!game.Alive) Restart(dx, dy);
                else
                {
                    if (!started) BeginRun();
                    if (!paused) game.Turn(dx, dy);
                }
                e.Handled = true;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (!game.Alive) Restart(0, 0);
            else if (!started) BeginRun();
            else if (paused) Resume();
            else Pause();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            timer.Dispose();
            base.OnFormClosed(e);
        }

        /* ---------------------------------------------------------------- Отрисовка */

        private static Color Blend(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * t),
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Ui.Bg);

            Ui.Write(g, "Змейка", Ui.H1,
                new Rectangle(Ui.P(24), Ui.P(12), Ui.P(220), Ui.P(30)), Ui.Text, Ui.LeftMid);
            Ui.Write(g, "Стрелки или WASD · пробел — пауза · Esc — выход", Ui.Tiny,
                new Rectangle(Ui.P(24), Ui.P(45), ClientSize.Width - Ui.P(48), Ui.P(18)),
                Ui.Faint, Ui.LeftMid);

            TextFormatFlags rightMid = TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            Ui.Write(g, "Счёт " + game.Score + "   ·   рекорд " + best, Ui.Small,
                new Rectangle(Ui.P(24), Ui.P(45), ClientSize.Width - Ui.P(48), Ui.P(18)),
                Ui.Muted, rightMid);

            using (GraphicsPath path = Ui.Round(board, Ui.P(12)))
            {
                using (SolidBrush brush = new SolidBrush(Ui.Card)) g.FillPath(brush, path);

                g.SetClip(path);
                for (int y = 0; y < Rows; y++)
                {
                    for (int x = 0; x < Cols; x++)
                    {
                        if ((x + y) % 2 != 0) continue;
                        Rectangle c = CellRect(x, y);
                        using (SolidBrush wash = new SolidBrush(Ui.AccentWash))
                        {
                            g.FillRectangle(wash, c);
                        }
                    }
                }

                DrawFood(g);
                DrawSnake(g);
                g.ResetClip();

                Ui.StrokeRound(g, board, Ui.P(12), Ui.Border, 1f);
            }

            DrawOverlay(g);
        }

        private Rectangle CellRect(int x, int y)
        {
            return new Rectangle(board.X + x * cell, board.Y + y * cell, cell, cell);
        }

        private void DrawFood(Graphics g)
        {
            if (game.Food.X < 0 || game.Food.Y < 0) return;
            Rectangle c = CellRect(game.Food.X, game.Food.Y);
            int size = (int)Math.Round(cell * 0.58f);
            Rectangle apple = new Rectangle(c.X + (cell - size) / 2, c.Y + (cell - size) / 2 + Ui.P(1), size, size);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            {
                g.FillEllipse(shadow, apple.X, apple.Y + Ui.P(2), apple.Width, apple.Height);
            }
            using (SolidBrush brush = new SolidBrush(Ui.Bad))
            {
                g.FillEllipse(brush, apple);
            }
            using (SolidBrush shine = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
            {
                int d = Math.Max(2, size / 4);
                g.FillEllipse(shine, apple.X + size / 5, apple.Y + size / 5, d, d);
            }
            using (Pen stem = new Pen(Ui.Good, Math.Max(1.4f, cell * 0.07f)))
            {
                stem.StartCap = LineCap.Round;
                stem.EndCap = LineCap.Round;
                g.DrawLine(stem,
                    apple.X + apple.Width / 2, apple.Y,
                    apple.X + apple.Width / 2 + Ui.P(2), apple.Y - Ui.P(3));
            }
        }

        private void DrawSnake(Graphics g)
        {
            int inset = Math.Max(1, cell / 12);
            for (int i = game.Snake.Count - 1; i >= 0; i--)
            {
                Rectangle c = CellRect(game.Snake[i].X, game.Snake[i].Y);
                Rectangle r = new Rectangle(c.X + inset, c.Y + inset,
                    c.Width - inset * 2, c.Height - inset * 2);

                Color color = i == 0
                    ? Ui.Accent
                    : Blend(Ui.Accent, Ui.Card, Math.Min(0.45, 0.08 + i * 0.025));
                Ui.FillRound(g, r, Math.Max(2, cell / 4), color);

                if (i == 0) DrawEyes(g, r);
            }
        }

        private void DrawEyes(Graphics g, Rectangle head)
        {
            int eye = Math.Max(2, cell / 6);
            Point d = game.Direction;
            int cx = head.X + head.Width / 2 + d.X * Math.Max(1, cell / 6);
            int cy = head.Y + head.Height / 2 + d.Y * Math.Max(1, cell / 6);

            int px = d.Y != 0 ? Math.Max(2, cell / 5) : 0;
            int py = d.X != 0 ? Math.Max(2, cell / 5) : 0;

            using (SolidBrush brush = new SolidBrush(Ui.OnAccent))
            {
                g.FillEllipse(brush, cx - eye / 2 - px, cy - eye / 2 - py, eye, eye);
                g.FillEllipse(brush, cx - eye / 2 + px, cy - eye / 2 + py, eye, eye);
            }
        }

        private void DrawOverlay(Graphics g)
        {
            if (game.Alive && started && !paused) return;

            string title;
            string subtitle;
            if (!game.Alive)
            {
                title = "Игра окончена";
                subtitle = "Счёт: " + game.Score + "   ·   пробел — заново";
            }
            else if (!started)
            {
                title = "Змейка";
                subtitle = "Стрелка или WASD — начать";
            }
            else
            {
                title = "Пауза";
                subtitle = "Пробел — продолжить";
            }

            using (GraphicsPath path = Ui.Round(board, Ui.P(12)))
            using (SolidBrush veil = new SolidBrush(Color.FromArgb(218, Ui.Card)))
            {
                g.FillPath(veil, path);
            }

            int top = board.Y + board.Height / 2 - Ui.P(46);
            Ui.Write(g, title, Ui.H2,
                new Rectangle(board.X, top, board.Width, Ui.P(34)), Ui.Text, Ui.CenterMid);
            Ui.Write(g, subtitle, Ui.Small,
                new Rectangle(board.X, top + Ui.P(34), board.Width, Ui.P(24)), Ui.Muted, Ui.CenterMid);
        }
    }
}
