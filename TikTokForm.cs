using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Video2Gif
{
    internal sealed class TikTokForm : Form
    {
        private FlatInput urlInput;
        private PillButton pasteButton;
        private PillButton downloadButton;
        private PillButton cancelButton;
        private Label statusLabel;
        private SlimBar progressBar;

        private bool working;
        private int lastPercent = -1;
        private int lastTick;
        private TikTokInfo info;
        private TikTokDownloader downloader;

        public string SavedPath;
        public int SavedCount = 1;

        public TikTokForm()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = "Скачать из TikTok";
            BackColor = Ui.Bg;
            Font = Ui.Base;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.P(520), Ui.P(282));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            Controls.Add(MakeLabel("Скачать из TikTok", Ui.H1, Ui.Text, 24, 20, 472, 30));
            Controls.Add(MakeLabel("Без водяного знака · интернет нужен только для загрузки",
                Ui.Small, Ui.Muted, 24, 52, 472, 18));

            urlInput = new FlatInput();
            urlInput.SetBounds(Ui.P(24), Ui.P(88), Ui.P(376), Ui.P(40));
            urlInput.Placeholder = "https://www.tiktok.com/@…/video/…";
            urlInput.Box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    StartDownload();
                }
            };
            Controls.Add(urlInput);

            pasteButton = new PillButton();
            pasteButton.Ghost = true;
            pasteButton.Text = "Вставить";
            pasteButton.Font = Ui.Small;
            pasteButton.SetBounds(Ui.P(412), Ui.P(88), Ui.P(84), Ui.P(40));
            pasteButton.Click += delegate { PasteLink(); };
            Controls.Add(pasteButton);

            statusLabel = MakeLabel("Вставьте ссылку на видео или фото-пост",
                Ui.Small, Ui.Muted, 24, 140, 472, 38);
            Controls.Add(statusLabel);

            progressBar = new SlimBar();
            progressBar.SetBounds(Ui.P(24), Ui.P(186), Ui.P(472), Ui.P(6));
            progressBar.Visible = false;
            Controls.Add(progressBar);

            downloadButton = new PillButton();
            downloadButton.Accent = true;
            downloadButton.Text = "Скачать";
            downloadButton.Font = Ui.Bold;
            downloadButton.SetBounds(Ui.P(24), Ui.P(216), Ui.P(220), Ui.P(42));
            downloadButton.Click += delegate { StartDownload(); };
            Controls.Add(downloadButton);

            cancelButton = new PillButton();
            cancelButton.Ghost = true;
            cancelButton.Text = "Отмена";
            cancelButton.SetBounds(Ui.P(276), Ui.P(216), Ui.P(220), Ui.P(42));
            cancelButton.Click += delegate { Close(); };
            Controls.Add(cancelButton);

            ResumeLayout(false);
        }

        private static Label MakeLabel(string text, Font font, Color color,
            int x, int y, int w, int h)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.SetBounds(Ui.P(x), Ui.P(y), Ui.P(w), Ui.P(h));
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!IsHandleCreated) return;
            int value = Ui.Dark ? 1 : 0;
            try { DwmSetWindowAttribute(Handle, 20, ref value, sizeof(int)); } catch { }
            try { DwmSetWindowAttribute(Handle, 19, ref value, sizeof(int)); } catch { }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText().Trim();
                    if (TikTok.LooksLikeUrl(text)) urlInput.Value = text;
                }
            }
            catch { }
            urlInput.Box.Focus();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (downloader != null) downloader.Cancel();
            base.OnFormClosing(e);
        }

        private void PasteLink()
        {
            try
            {
                if (Clipboard.ContainsText()) urlInput.Value = Clipboard.GetText().Trim();
            }
            catch { }
            urlInput.Box.Focus();
        }

        private void StartDownload()
        {
            if (working) return;
            string url = urlInput.Value.Trim();
            if (url.Length == 0)
            {
                SetStatus("Вставьте ссылку на видео TikTok.", true);
                return;
            }

            working = true;
            SetBusy(true);
            SetStatus("Получаем ссылку…", false);

            string captured = url;
            Task.Factory.StartNew(delegate { return TikTok.Resolve(captured); })
                .ContinueWith(delegate(Task<TikTokInfo> task)
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { OnResolved(task); });
                    }
                    catch { }
                });
        }

        private void OnResolved(Task<TikTokInfo> task)
        {
            if (task.IsFaulted)
            {
                Exception inner = task.Exception != null ? task.Exception.InnerException : null;
                Fail(inner != null ? inner.Message : "Не удалось получить ссылку.");
                return;
            }

            info = task.Result;
            if (info.IsPhotoPost) ChooseFolder();
            else ChooseFile();
        }

        private void ChooseFile()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Сохранить видео TikTok";
                dialog.Filter = "Видео MP4|*.mp4|Все файлы|*.*";
                dialog.DefaultExt = "mp4";
                dialog.AddExtension = true;
                dialog.OverwritePrompt = true;
                dialog.FileName = info.SuggestedName;
                dialog.InitialDirectory = DefaultFolder(Environment.SpecialFolder.MyVideos);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    Reset();
                    return;
                }
                StartDownloadTo(dialog.FileName);
            }
        }

        private void ChooseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Куда сохранить фото из TikTok";
                dialog.ShowNewFolderButton = true;
                dialog.SelectedPath = DefaultFolder(Environment.SpecialFolder.MyPictures);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    Reset();
                    return;
                }

                string target = Path.Combine(dialog.SelectedPath, info.SuggestedName);
                if (Directory.Exists(target))
                    target = Path.Combine(dialog.SelectedPath,
                        info.SuggestedName + "_" + DateTime.Now.ToString("HHmmss"));
                StartDownloadTo(target);
            }
        }

        private static string DefaultFolder(Environment.SpecialFolder folder)
        {
            string path = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(path)) path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(path)) path = Path.GetTempPath();
            return path;
        }

        private void StartDownloadTo(string path)
        {
            downloader = new TikTokDownloader();
            lastPercent = -1;
            lastTick = 0;
            downloadButton.Text = "Скачиваем…";
            SetStatus(info.IsPhotoPost ? "Скачиваем фото…" : "Скачиваем видео…", false);
            progressBar.Value = 0;
            progressBar.Visible = true;

            TikTokInfo captured = info;
            TikTokDownloader active = downloader;
            Task.Factory.StartNew(delegate
            {
                active.Download(captured, path, ReportProgress);
                return path;
            }).ContinueWith(delegate(Task<string> task)
            {
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate { OnDownloaded(task, captured); });
                }
                catch { }
            });
        }

        private void ReportProgress(int percent, string stage)
        {
            int now = Environment.TickCount;
            if (percent >= 0 && percent < 100 && percent == lastPercent && now - lastTick < 160) return;
            lastPercent = percent;
            lastTick = now;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed) return;
                    if (percent >= 0) progressBar.Value = Math.Max(0, Math.Min(100, percent));
                    statusLabel.ForeColor = Ui.Muted;
                    statusLabel.Text = stage;
                });
            }
            catch { }
        }

        private void OnDownloaded(Task<string> task, TikTokInfo captured)
        {
            if (task.IsFaulted)
            {
                Exception inner = task.Exception != null ? task.Exception.InnerException : null;
                Fail(inner != null ? inner.Message : "Не удалось скачать файл.");
                return;
            }

            string path = task.Result;
            SavedCount = captured.IsPhotoPost ? captured.PhotoUrls.Count : 1;
            SavedPath = captured.IsPhotoPost
                ? Path.Combine(path, "photo_01.jpg")
                : path;

            if (!File.Exists(SavedPath))
            {
                Fail("Файл скачался, но не найден на диске.");
                return;
            }

            working = false;
            downloader = null;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Fail(string message)
        {
            working = false;
            downloader = null;
            SetBusy(false);
            progressBar.Visible = false;
            SetStatus(message, true);
        }

        private void Reset()
        {
            working = false;
            info = null;
            SetBusy(false);
            progressBar.Visible = false;
            SetStatus("Вставьте ссылку на видео или фото-пост", false);
        }

        private void SetBusy(bool busy)
        {
            urlInput.Enabled = !busy;
            pasteButton.Enabled = !busy;
            downloadButton.Enabled = !busy;
            downloadButton.Text = busy ? "Подождите…" : "Скачать";
            progressBar.Visible = busy;
        }

        private void SetStatus(string text, bool error)
        {
            statusLabel.ForeColor = error ? Ui.Bad : Ui.Muted;
            statusLabel.Text = text;
        }
    }
}
