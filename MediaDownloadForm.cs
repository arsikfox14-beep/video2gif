using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Video2Gif
{
    internal sealed class MediaDownloadForm : Form
    {
        private readonly bool spotify;
        private FlatInput urlInput;
        private FlatInput folderInput;
        private PillButton pasteButton;
        private PillButton chooseButton;
        private PillButton videoPill;
        private PillButton audioPill;
        private PillButton wavPill;
        private PillButton downloadButton;
        private PillButton cancelButton;
        private Label statusLabel;
        private SlimBar progressBar;

        private bool audio;
        private string audioFormat = "mp3";
        private bool working;
        private string selectedFolder = "";
        private int lastPercent = -1;
        private int lastTick;
        private MediaDownloader downloader;

        public readonly List<string> SavedFiles = new List<string>();
        public string SavedFolder = "";

        public MediaDownloadForm(bool spotify)
        {
            this.spotify = spotify;
            this.audio = spotify;
            BuildUi();
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = spotify ? "Скачать музыку из Spotify" : "Скачать видео или музыку";
            BackColor = Ui.Bg;
            Font = Ui.Base;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.P(560), Ui.P(412));
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

            Controls.Add(MakeLabel(spotify ? "Скачать музыку из Spotify" : "Скачать видео или музыку",
                Ui.H1, Ui.Text, 24, 20, 512, 30));
            Controls.Add(MakeLabel(
                spotify
                    ? "Трек, альбом или плейлист · находим на YouTube и сохраняем в MP3 или WAV"
                    : "YouTube, SoundCloud, VK и другие сайты · нужен интернет",
                Ui.Small, Ui.Muted, 24, 52, 512, 18));

            urlInput = new FlatInput();
            urlInput.SetBounds(Ui.P(24), Ui.P(88), Ui.P(400), Ui.P(40));
            urlInput.Placeholder = spotify
                ? "https://open.spotify.com/track/…"
                : "https://www.youtube.com/watch?v=…";
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
            pasteButton.SetBounds(Ui.P(432), Ui.P(88), Ui.P(104), Ui.P(40));
            pasteButton.Click += delegate { PasteLink(); };
            Controls.Add(pasteButton);

            statusLabel = MakeLabel(
                spotify
                    ? "Вставьте ссылку Spotify на трек, альбом или плейлист"
                    : "Вставьте ссылку на видео или музыку",
                Ui.Small, Ui.Muted, 24, 138, 512, 36);
            Controls.Add(statusLabel);

            Controls.Add(MakeLabel("Формат", Ui.Small, Ui.Muted, 24, 184, 200, 18));

            videoPill = new PillButton();
            videoPill.Text = "Видео MP4";
            videoPill.Font = Ui.Small;
            videoPill.SetBounds(Ui.P(24), Ui.P(204), Ui.P(248), Ui.P(34));
            videoPill.Click += delegate { audio = false; audioFormat = "mp4"; UpdateFormat(); };
            Controls.Add(videoPill);

            audioPill = new PillButton();
            audioPill.Text = "Музыка MP3";
            audioPill.Font = Ui.Small;
            audioPill.SetBounds(Ui.P(288), Ui.P(204), Ui.P(248), Ui.P(34));
            audioPill.Click += delegate { audio = true; audioFormat = "mp3"; UpdateFormat(); };
            Controls.Add(audioPill);

            wavPill = new PillButton();
            wavPill.Text = "WAV · 1411 кбит/с";
            wavPill.Font = Ui.Small;
            wavPill.SetBounds(Ui.P(288), Ui.P(204), Ui.P(248), Ui.P(34));
            wavPill.Visible = false;
            wavPill.Click += delegate { audio = true; audioFormat = "wav"; UpdateFormat(); };
            Controls.Add(wavPill);

            if (spotify)
            {
                videoPill.Visible = false;
                audioPill.Text = "MP3 · 320 кбит/с";
                audioPill.SetBounds(Ui.P(24), Ui.P(204), Ui.P(248), Ui.P(34));
                wavPill.SetBounds(Ui.P(288), Ui.P(204), Ui.P(248), Ui.P(34));
                wavPill.Visible = true;
            }

            Controls.Add(MakeLabel("Куда сохранить", Ui.Small, Ui.Muted, 24, 250, 300, 18));

            folderInput = new FlatInput();
            folderInput.SetBounds(Ui.P(24), Ui.P(270), Ui.P(400), Ui.P(40));
            folderInput.Box.ReadOnly = true;
            Controls.Add(folderInput);

            chooseButton = new PillButton();
            chooseButton.Ghost = true;
            chooseButton.Text = "Выбрать";
            chooseButton.Font = Ui.Small;
            chooseButton.SetBounds(Ui.P(432), Ui.P(270), Ui.P(104), Ui.P(40));
            chooseButton.Click += delegate { ChooseFolder(); };
            Controls.Add(chooseButton);

            progressBar = new SlimBar();
            progressBar.SetBounds(Ui.P(24), Ui.P(322), Ui.P(512), Ui.P(6));
            progressBar.Visible = false;
            Controls.Add(progressBar);

            downloadButton = new PillButton();
            downloadButton.Accent = true;
            downloadButton.Text = "Скачать";
            downloadButton.Font = Ui.Bold;
            downloadButton.SetBounds(Ui.P(24), Ui.P(344), Ui.P(248), Ui.P(44));
            downloadButton.Click += delegate { StartDownload(); };
            Controls.Add(downloadButton);

            cancelButton = new PillButton();
            cancelButton.Ghost = true;
            cancelButton.Text = "Закрыть";
            cancelButton.SetBounds(Ui.P(288), Ui.P(344), Ui.P(248), Ui.P(44));
            cancelButton.Click += delegate
            {
                if (working)
                {
                    if (downloader != null) downloader.Cancel();
                    SetStatus("Останавливаем…", false);
                }
                else
                {
                    Close();
                }
            };
            Controls.Add(cancelButton);

            selectedFolder = SavePaths.Desktop();
            folderInput.Value = selectedFolder;
            UpdateFormat();

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
            label.UseMnemonic = false;
            label.Tag = Ui.RoleOf(color);
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
                    bool isSpotify = SpotifyLinks.LooksLike(text);
                    if ((spotify && isSpotify) || (!spotify && !isSpotify && LooksLikeWeb(text)))
                    {
                        urlInput.Value = text;
                    }
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

        private static bool LooksLikeWeb(string text)
        {
            return !string.IsNullOrEmpty(text)
                && (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateFormat()
        {
            videoPill.Active = !audio && !spotify;
            audioPill.Active = audio && (!spotify || audioFormat == "mp3");
            wavPill.Active = spotify && audio && audioFormat == "wav";

            // Перерисовываем все кнопки, чтобы ни одна не осталась «нажатой».
            videoPill.Invalidate();
            audioPill.Invalidate();
            wavPill.Invalidate();
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

        private void ChooseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Куда сохранить скачанные файлы";
                dialog.ShowNewFolderButton = true;
                if (Directory.Exists(selectedFolder)) dialog.SelectedPath = selectedFolder;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                selectedFolder = dialog.SelectedPath;
                folderInput.Value = selectedFolder;
            }
        }

        private void StartDownload()
        {
            if (working) return;

            string url = urlInput.Value.Trim();
            if (url.Length == 0)
            {
                SetStatus(spotify ? "Вставьте ссылку Spotify." : "Вставьте ссылку на видео или музыку.", true);
                return;
            }
            if (!LooksLikeWeb(url) && !url.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Это не похоже на ссылку.", true);
                return;
            }
            if (spotify && !SpotifyLinks.LooksLike(url))
            {
                SetStatus("Нужна ссылка Spotify: трек, альбом или плейлист.", true);
                return;
            }
            if (!spotify && SpotifyLinks.LooksLike(url))
            {
                SetStatus("Это ссылка Spotify — закройте окно и выберите «Музыка из Spotify».", true);
                return;
            }

            if (string.IsNullOrEmpty(selectedFolder) || !Directory.Exists(selectedFolder))
            {
                SetStatus("Выберите папку для сохранения.", true);
                return;
            }

            working = true;
            SetBusy(true);
            lastPercent = -1;
            lastTick = 0;
            progressBar.Value = 0;
            progressBar.Visible = true;
            SetStatus(spotify ? "Получаем список треков…" : "Готовимся…", false);

            downloader = new MediaDownloader();
            MediaDownloader active = downloader;
            string captured = url;
            bool capturedAudio = audio;
            string capturedFormat = audioFormat;
            string folder = selectedFolder;

            Task.Factory.StartNew(delegate
            {
                return active.Download(captured, spotify, capturedAudio, capturedFormat,
                    folder, ReportProgress);
            }).ContinueWith(delegate(Task<List<string>> task)
            {
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate { OnDone(task, folder); });
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
                    SetStatus(stage, false);
                });
            }
            catch { }
        }

        private void OnDone(Task<List<string>> task, string folder)
        {
            if (task.IsFaulted)
            {
                Exception inner = task.Exception != null ? task.Exception.InnerException : null;
                if (inner is ConversionCancelledException)
                {
                    Reset("Отменено.", false);
                    return;
                }
                Reset(inner != null ? inner.Message : "Не удалось скачать файл.", true);
                return;
            }

            List<string> result = task.Result;
            if (result == null || result.Count == 0)
            {
                Reset("Ничего не скачалось.", true);
                return;
            }

            string saved = folder;
            if (downloader != null && !string.IsNullOrEmpty(downloader.LastFolder))
            {
                saved = downloader.LastFolder;
            }

            working = false;
            downloader = null;
            SavedFiles.Clear();
            SavedFiles.AddRange(result);
            SavedFolder = saved;
            progressBar.Value = 100;
            progressBar.Visible = false;
            SetBusy(false);
            SetStatus("Готово: " + result.Count + " файл(ов).", false);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void Reset(string message, bool error)
        {
            working = false;
            downloader = null;
            SetBusy(false);
            progressBar.Visible = false;
            SetStatus(message, error);
        }

        private void SetBusy(bool busy)
        {
            urlInput.Enabled = !busy;
            pasteButton.Enabled = !busy;
            chooseButton.Enabled = !busy;
            videoPill.Enabled = !busy && !spotify;
            audioPill.Enabled = !busy;
            wavPill.Enabled = !busy && spotify;
            downloadButton.Enabled = !busy;
            downloadButton.Text = busy ? "Скачиваем…" : "Скачать";
            cancelButton.Text = busy ? "Отменить" : "Закрыть";
        }

        private void SetStatus(string text, bool error)
        {
            statusLabel.ForeColor = error ? Ui.Bad : Ui.Muted;
            statusLabel.Tag = Ui.RoleOf(statusLabel.ForeColor);
            statusLabel.Text = text;
        }
    }
}
