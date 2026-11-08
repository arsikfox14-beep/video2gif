using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Video2Gif
{
    internal sealed partial class MainForm : Form
    {
        private bool ffmpegReady;
        private bool busy;
        private string sourcePath;
        private VideoInfo info;
        private string resultPath;
        private string tempRoot;
        private readonly System.Collections.Generic.List<string> tempDirs =
            new System.Collections.Generic.List<string>();
        private Converter converter;
        private MemoryStream previewStream;
        private int lastReportTick;

        public MainForm()
        {
            Ui.SetDark(LoadDarkSetting());
            BuildUi();
            EnableDrop(this);
            FormClosing += delegate { Shutdown(); };
            UpdateActions();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTitleBarTheme();
        }

        /* --------------------------------------------------------------- Тема */

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Video2Gif", "theme.txt");
            }
        }

        private static bool LoadDarkSetting()
        {
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path)) return true;
                return File.ReadAllText(path).Trim() != "light";
            }
            catch { return true; }
        }

        private void SaveTheme()
        {
            try
            {
                string path = SettingsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Ui.Dark ? "dark" : "light");
            }
            catch { }
        }

        private void ToggleTheme()
        {
            Ui.SetDark(!Ui.Dark);
            themeButton.Text = Ui.Dark ? "Светлая" : "Тёмная";
            Ui.ApplyTheme(this);
            ApplyTitleBarTheme();
            SaveTheme();
            Invalidate(true);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private void ApplyTitleBarTheme()
        {
            if (!IsHandleCreated) return;
            int value = Ui.Dark ? 1 : 0;
            try { DwmSetWindowAttribute(Handle, 20, ref value, sizeof(int)); } catch { }
            try { DwmSetWindowAttribute(Handle, 19, ref value, sizeof(int)); } catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            string[] args = Environment.GetCommandLineArgs();
            PrepareFfmpeg();
            if (args.Length > 1 && File.Exists(args[1]))
            {
                LoadVideo(args[1]);
            }
        }

        /* ------------------------------------------------------------ FFmpeg */

        private void PrepareFfmpeg()
        {
            convertButton.Text = "Подготовка…";
            convertButton.Enabled = false;
            Task.Factory.StartNew(delegate { FfmpegBundle.Ensure(); })
                .ContinueWith(delegate(Task t)
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            convertButton.Text = "Сделать GIF";
                            if (t.IsFaulted)
                            {
                                string msg = t.Exception != null && t.Exception.InnerException != null
                                    ? t.Exception.InnerException.Message
                                    : "неизвестная ошибка";
                                MessageBox.Show(this, "Не удалось подготовить FFmpeg.\n\n" + msg,
                                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                            {
                                ffmpegReady = true;
                            }
                            UpdateActions();
                        });
                    }
                    catch { }
                });
        }

        /* -------------------------------------------------------------- Файл */

        private void PickFile()
        {
            if (busy) return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите видео или картинку";
                dialog.Filter = "Видео, картинки и GIF|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.wmv;*.flv;*.mpg;*.mpeg;*.ts;*.mts;*.m2ts;*.3gp;*.ogv;*.gif;*.png;*.jpg;*.jpeg;*.jfif;*.webp;*.bmp;*.tif;*.tiff;*.tga;*.avif|Все файлы|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadVideo(dialog.FileName);
                }
            }
        }

#if !NOTIKTOK
        private void OpenTikTok()
        {
            if (busy) return;
            using (TikTokForm dialog = new TikTokForm())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (string.IsNullOrEmpty(dialog.SavedPath) || !File.Exists(dialog.SavedPath)) return;

                LoadVideo(dialog.SavedPath);
                if (dialog.SavedCount > 1)
                {
                    MessageBox.Show(this,
                        "Скачано фото: " + dialog.SavedCount + ".\nПервое открыто в программе, остальные лежат в той же папке.",
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
#endif

        private void LoadVideo(string path)
        {
            if (busy) return;
            FileInfo fi = new FileInfo(path);
            if (!fi.Exists)
            {
                MessageBox.Show(this, "Файл не найден.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            sourcePath = path;
            info = null;
            drop.Selected = true;
            drop.FileName = fi.Name;
            drop.FileMeta = "Читаем файл…  " + Fmt.Size(fi.Length);
            drop.BusyText = "";
            drop.Invalidate();
            UpdateActions();

            string captured = path;
            Task.Factory.StartNew(delegate { return Ffmpeg.Probe(captured); })
                .ContinueWith(delegate(Task<VideoInfo> t)
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { FinishProbe(captured, t); });
                    }
                    catch { }
                });
        }

        private void FinishProbe(string path, Task<VideoInfo> task)
        {
            if (path != sourcePath) return;
            if (task.IsFaulted)
            {
                info = null;
                startInput.Enabled = true;
                durationInput.Enabled = true;
                fpsInput.Enabled = true;
                startInput.Visible = true;
                durationInput.Visible = true;
                fpsInput.Visible = true;
                startLabel.Visible = true;
                durationLabel.Visible = true;
                fpsLabel.Visible = true;
                imageNote.Visible = false;
                widthLabel.SetBounds(Ui.P(282), Ui.P(54), Ui.P(128), Ui.P(18));
                widthInput.SetBounds(Ui.P(282), Ui.P(72), Ui.P(128), Ui.P(34));
                durationInput.Placeholder = "весь клип";
                drop.FileMeta = "Не удалось прочитать файл";
                drop.Invalidate();
                UpdateActions();
                Exception inner = task.Exception != null ? task.Exception.InnerException : null;
                MessageBox.Show(this,
                    inner != null ? inner.Message : "Не удалось прочитать файл.",
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            info = task.Result;
            UpdateFileMeta();
            bool image = info.IsImage;
            startInput.Enabled = !image;
            durationInput.Enabled = !image;
            fpsInput.Enabled = !image;
            startInput.Visible = !image;
            durationInput.Visible = !image;
            fpsInput.Visible = !image;
            startLabel.Visible = !image;
            durationLabel.Visible = !image;
            fpsLabel.Visible = !image;
            imageNote.Visible = image;
            widthLabel.SetBounds(Ui.P(282), Ui.P(54), Ui.P(image ? 260 : 128), Ui.P(18));
            widthInput.SetBounds(Ui.P(282), Ui.P(72), Ui.P(image ? 260 : 128), Ui.P(34));
            startInput.Value = image ? "" : "0";
            durationInput.Placeholder = image ? "не используется" : "весь клип";
            durationInput.Value = image ? "" : InputNumber(Math.Min(8.0, info.Duration > 0 ? info.Duration : 8.0));
            UpdateActions();
        }

        private void UpdateFileMeta()
        {
            if (info == null || sourcePath == null) return;
            long bytes = 0;
            try { bytes = new FileInfo(sourcePath).Length; } catch { }
            if (info.IsImage)
            {
                drop.FileMeta = "КАРТИНКА · " + info.Width + "×" + info.Height +
                    (bytes > 0 ? " · " + Fmt.Size(bytes) : "");
            }
            else
            {
                drop.FileMeta = info.Codec.ToUpperInvariant() + " · " + info.Describe() +
                    (bytes > 0 ? " · " + Fmt.Size(bytes) : "");
            }
            drop.Invalidate();
        }

        private static string InputNumber(double value)
        {
            return value.ToString("0.#").Replace(',', '.');
        }

        private void UpdateActions()
        {
            convertButton.Enabled = ffmpegReady && info != null && !busy;
            cancelButton.Visible = busy;
            ditherBayer.Enabled = !busy;
            ditherSierra.Enabled = !busy;
            ditherNone.Enabled = !busy;
            downloadsButton.Enabled = !busy;
        }

        /* ------------------------------------------------------- Конвертация */

        private void StartConversion()
        {
            if (busy) return;
            if (!ffmpegReady)
            {
                MessageBox.Show(this, "FFmpeg ещё готовится, подождите пару секунд.",
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (info == null || sourcePath == null)
            {
                MessageBox.Show(this, "Сначала выберите видео или картинку.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool imageInput = info.IsImage;

            double start = 0;
            double duration = 0;
            double effective = 0;
            if (!imageInput)
            {
                string startText = startInput.Value.Trim();
                if (startText.Length == 0) start = 0;
                else if (!Fmt.TryNum(startText, out start) || start < 0)
                {
                    ShowFieldError("Начало должно быть числом от 0 секунд.", startInput);
                    return;
                }

                string durationText = durationInput.Value.Trim();
                if (durationText.Length > 0 && (!Fmt.TryNum(durationText, out duration) || duration <= 0))
                {
                    ShowFieldError("Длительность должна быть больше нуля (или оставьте поле пустым).",
                        durationInput);
                    return;
                }

                if (info.Duration > 0 && start >= info.Duration - 0.05)
                {
                    ShowFieldError("Начало совпадает с концом видео.", startInput);
                    return;
                }

                double available = info.Duration > 0 ? Math.Max(0, info.Duration - start) : -1;
                effective = duration > 0
                    ? (available > 0 ? Math.Min(duration, available) : duration)
                    : (available > 0 ? available : 0);
                if (effective > 0 && effective < 0.1)
                {
                    MessageBox.Show(this, "Слишком короткий фрагмент — выберите другую длительность.",
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            double widthValue;
            string widthText = widthInput.Value.Trim();
            if (widthText.Length == 0) widthValue = 0;
            else if (!Fmt.TryNum(widthText, out widthValue) || widthValue < 0 || widthValue > 1920)
            {
                ShowFieldError("Ширина должна быть от 0 до 1920 пикселей.", widthInput);
                return;
            }

            double fpsValue = 30;
            if (!imageInput)
            {
                if (!Fmt.TryNum(fpsInput.Value.Trim(), out fpsValue) || fpsValue < 1 || fpsValue > 60)
                {
                    ShowFieldError("Кадров в секунду — от 1 до 60.", fpsInput);
                    return;
                }
            }

            tempRoot = Path.Combine(Path.GetTempPath(),
                "video2gif-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(tempRoot);
                tempDirs.Add(tempRoot);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось создать временную папку.\n\n" + ex.Message,
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ConversionOptions options = new ConversionOptions();
            options.Input = sourcePath;
            options.Output = Path.Combine(tempRoot, "result.gif");
            options.Palette = Path.Combine(tempRoot, "palette.png");
            options.TempDir = tempRoot;
            options.Start = imageInput ? 0 : start;
            options.Duration = imageInput ? 0 : (duration > 0 ? effective : 0);
            options.Width = (int)Math.Round(widthValue);
            options.Fps = (int)Math.Round(fpsValue);
            options.Colors = 256;
            options.Dither = DitherName(ditherIndex);
            options.Loop = loopToggle.Checked;
            options.Fast = fastToggle.Checked;
            options.Info = info;

            busy = true;
            converter = new Converter();
            UpdateActions();
            string detail;
            if (imageInput)
            {
                int shownWidth = options.Width > 0 && info.Width > 0
                    ? Math.Min(options.Width, info.Width)
                    : (info.Width > 0 ? info.Width : options.Width);
                detail = "картинка · " + (shownWidth > 0 ? shownWidth + " px" : "оригинал");
            }
            else
            {
                detail = (effective > 0 ? Fmt.Time(effective) : "весь клип") + " · " +
                    options.Fps + " fps · " +
                    (options.Width > 0 ? options.Width.ToString() + " px" : "оригинал");
            }
            ShowProgress("Готовимся…", detail);

            Task.Factory.StartNew(delegate
            {
                try
                {
                    converter.Run(options, ReportProgress);
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { FinishSuccess(options.Output); });
                    }
                    catch { }
                }
                catch (ConversionCancelledException)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { FinishFailure("Конвертация отменена.", false); });
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    string message = ex.Message;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { FinishFailure(message, true); });
                    }
                    catch { }
                }
            });
        }

        private void ReportProgress(int percent, string stage)
        {
            int now = Environment.TickCount;
            if (percent < 100 && now - lastReportTick < 110) return;
            if (percent < 100) lastReportTick = now;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!busy) return;
                    progressBar.Value = percent;
                    percentLabel.Text = percent + "%";
                    stageLabel.Text = stage;
                });
            }
            catch { }
        }

        private void ShowProgress(string stage, string sub)
        {
            lastReportTick = 0;
            progressBar.Value = 0;
            percentLabel.Text = "0%";
            stageLabel.Text = stage;
            progressSub.Text = sub;
            emptyState.Visible = false;
            resultView.Visible = false;
            progressView.Visible = true;
            cancelButton.Enabled = true;
        }

        private void FinishSuccess(string output)
        {
            busy = false;
            converter = null;
            UpdateActions();

            Image image;
            try
            {
                preview.Image = null;
                if (previewStream != null) previewStream.Dispose();
                previewStream = new MemoryStream(File.ReadAllBytes(output));
                image = Image.FromStream(previewStream);
            }
            catch (Exception ex)
            {
                FinishFailure("Не удалось открыть готовый GIF: " + ex.Message, true);
                return;
            }

            preview.Image = image;
            resultPath = output;
            resultMeta.Text = Fmt.Size(new FileInfo(output).Length) + "   ·   " +
                image.Width + "×" + image.Height;
            progressView.Visible = false;
            emptyState.Visible = false;
            resultView.Visible = true;
            saveButton.Text = "Сохранить GIF";
        }

        private void FinishFailure(string message, bool showDialog)
        {
            busy = false;
            converter = null;
            UpdateActions();
            progressView.Visible = false;
            bool hasResult = resultPath != null && File.Exists(resultPath);
            resultView.Visible = hasResult;
            emptyState.Visible = !hasResult;
            DropCurrentTemp();

            if (showDialog)
            {
                MessageBox.Show(this, message, Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CancelConversion()
        {
            if (!busy || converter == null) return;
            cancelButton.Enabled = false;
            stageLabel.Text = "Останавливаем…";
            progressSub.Text = "";
            converter.Cancel();
        }

        private void ShowFieldError(string message, Control field)
        {
            MessageBox.Show(this, message, Program.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (field != null) field.Focus();
        }

        private static string DitherName(int index)
        {
            if (index == 1) return "sierra2_4a";
            if (index == 2) return "none";
            return "bayer";
        }

        private void SetDither(int index)
        {
            ditherIndex = index;
            ditherBayer.Active = index == 0;
            ditherSierra.Active = index == 1;
            ditherNone.Active = index == 2;
            ditherBayer.Invalidate();
            ditherSierra.Invalidate();
            ditherNone.Invalidate();
        }

        /* ---------------------------------------------------------- Результат */

        private void SaveResult()
        {
            if (resultPath == null || !File.Exists(resultPath)) return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Сохранить GIF";
                dialog.Filter = "GIF-файл|*.gif";
                dialog.DefaultExt = "gif";
                dialog.AddExtension = true;
                dialog.FileName = SafeName(sourcePath) + ".gif";
                try
                {
                    string dir = SavePaths.Desktop();
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dialog.InitialDirectory = dir;
                }
                catch { }

                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.Copy(resultPath, dialog.FileName, true);
                    saveButton.Text = "Сохранено";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось сохранить файл.\n\n" + ex.Message,
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string SafeName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "video";
            string name = Path.GetFileNameWithoutExtension(path);
            return string.IsNullOrEmpty(name) ? "video" : name;
        }

        private void OpenResult()
        {
            if (resultPath == null || !File.Exists(resultPath)) return;
            try
            {
                Process.Start(resultPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось открыть файл.\n\n" + ex.Message,
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /* ------------------------------------------------------- Перетаскивание */

        private void EnableDrop(Control root)
        {
            root.AllowDrop = true;
            root.DragEnter += OnDragEnter;
            root.DragOver += OnDragOver;
            root.DragLeave += OnDragLeave;
            root.DragDrop += OnDragDrop;

            foreach (Control child in root.Controls)
            {
                if (child is TextBoxBase || child is ComboBox) continue;
                EnableDrop(child);
            }
        }

        private static string FirstFile(IDataObject data)
        {
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop)) return null;
            string[] files = data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return null;
            return files[0];
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            string file = busy ? null : FirstFile(e.Data);
            if (file != null)
            {
                e.Effect = DragDropEffects.Copy;
                drop.Highlight = true;
                drop.Invalidate();
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effect = !busy && FirstFile(e.Data) != null
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnDragLeave(object sender, EventArgs e)
        {
            drop.Highlight = false;
            drop.Invalidate();
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            drop.Highlight = false;
            drop.Invalidate();
            if (busy) return;
            string file = FirstFile(e.Data);
            if (file != null) LoadVideo(file);
        }

        /* ------------------------------------------------------------- Очистка */

        private void Shutdown()
        {
            if (converter != null) converter.Cancel();
            if (preview != null)
            {
                preview.Image = null;
            }
            if (previewStream != null)
            {
                previewStream.Dispose();
                previewStream = null;
            }
            CleanupTemp();
        }

        private void CleanupTemp()
        {
            for (int i = 0; i < tempDirs.Count; i++)
            {
                try
                {
                    if (Directory.Exists(tempDirs[i])) Directory.Delete(tempDirs[i], true);
                }
                catch { }
            }
            tempDirs.Clear();
            tempRoot = null;
        }

        private void DropCurrentTemp()
        {
            string dir = tempRoot;
            tempRoot = null;
            if (string.IsNullOrEmpty(dir)) return;
            tempDirs.Remove(dir);
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch { }
        }
    }
}
