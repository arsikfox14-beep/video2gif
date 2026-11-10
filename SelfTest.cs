using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Video2Gif
{
    internal static class SelfTest
    {
        private static readonly StringBuilder Log = new StringBuilder();
        private static int failures;

        public static int Run()
        {
            string logPath = Path.Combine(Path.GetTempPath(), "video2gif-selftest.log");
            string work = Path.Combine(Path.GetTempPath(), "video2gif-selftest");
            int code = 0;

            try
            {
                Line(Program.AppName + " selftest, version " + Program.Version);
                Line("os: " + Environment.OSVersion + ", 64-bit: " + Environment.Is64BitProcess);
                Line("temp: " + work);

                if (Directory.Exists(work)) Directory.Delete(work, true);
                Directory.CreateDirectory(work);

                string ffmpeg = FfmpegBundle.Path;
                Line("ffmpeg path: " + ffmpeg);
                Check("ffmpeg file", File.Exists(ffmpeg) && new FileInfo(ffmpeg).Length > 10L * 1024L * 1024L);

                string versionOut;
                int versionCode = RunProcess(ffmpeg, "-hide_banner -version", out versionOut);
                Check("ffmpeg -version", versionCode == 0 && versionOut.IndexOf("ffmpeg version", StringComparison.OrdinalIgnoreCase) >= 0);
                Line("  " + FirstLine(versionOut));

                string ytdlp = YtDlpBundle.Path;
                Line("yt-dlp path: " + ytdlp);
                Check("yt-dlp file", File.Exists(ytdlp) && new FileInfo(ytdlp).Length > 15L * 1024L * 1024L);
                string ytVersion;
                int ytCode = RunProcess(ytdlp, "--version", out ytVersion);
                Check("yt-dlp version", ytCode == 0 && ytVersion.Trim().Length > 0);
                Line("  " + FirstLine(ytVersion));

                string spotifyJson = "{\"props\":{\"pageProps\":{\"state\":{\"data\":{\"entity\":{\"type\":\"playlist\",\"trackList\":[" +
                    "{\"title\":\"Song One\",\"subtitle\":\"Artist A\",\"duration\":1000,\"isPlayable\":true}," +
                    "{\"title\":\"Song Two\",\"subtitle\":\"Artist B\",\"duration\":2000,\"isPlayable\":false}]}}}}}}";
                string spotifyHtml = "<script id=\"__NEXT_DATA__\" type=\"application/json\">" +
                    spotifyJson + "</script>";
                List<MediaTrack> spotifyTracks = SpotifyLinks.ParseHtml(spotifyHtml);
                Check("spotify link parse", spotifyTracks.Count == 1 &&
                    spotifyTracks[0].Title == "Song One" &&
                    spotifyTracks[0].Query == "Artist A Song One");

                MediaTrack timed = new MediaTrack();
                timed.DurationMs = 213000;
                Check("spotify duration filter",
                    MediaDownloader.DurationFilter(timed) == "duration >= 203 & duration <= 223");
                Check("spotify duration filter off",
                    MediaDownloader.DurationFilter(new MediaTrack()) == null);
                Check("default folder desktop", Directory.Exists(SavePaths.Desktop()));

                SnakeGame snake = new SnakeGame(20, 15);
                Check("snake initial",
                    snake.Snake.Count == 3 && snake.Alive &&
                    snake.Direction.X == 1 && snake.Direction.Y == 0);
                Point snakeHead = snake.Snake[0];
                Check("snake step",
                    snake.Step() == SnakeStep.Moved &&
                    snake.Snake[0].X == snakeHead.X + 1 && snake.Snake.Count == 3);
                snake.Turn(-1, 0);
                snake.Step();
                Check("snake no reverse", snake.Snake[0].X == snakeHead.X + 2);
                snake.Food = new Point(snake.Snake[0].X + 1, snake.Snake[0].Y);
                Check("snake eat",
                    snake.Step() == SnakeStep.Ate && snake.Score == 1 && snake.Snake.Count == 4);

                bool foodOk = true;
                for (int i = 0; i < 40; i++)
                {
                    snake.PlaceFood();
                    if (snake.Food.X < 0 || snake.Food.Y < 0 ||
                        snake.Food.X >= snake.Cols || snake.Food.Y >= snake.Rows ||
                        snake.Snake.Contains(snake.Food))
                    {
                        foodOk = false;
                        break;
                    }
                }
                Check("snake food free cell", foodOk);

                SnakeGame wall = new SnakeGame(20, 15);
                for (int i = 0; i < 20 && wall.Alive; i++) wall.Step();
                Check("snake wall", !wall.Alive);

                SnakeGame self = new SnakeGame(20, 15);
                self.Food = new Point(self.Snake[0].X + 1, self.Snake[0].Y);
                self.Step();
                self.Food = new Point(self.Snake[0].X + 1, self.Snake[0].Y);
                self.Step();
                self.Turn(0, -1);
                self.Step();
                self.Turn(-1, 0);
                self.Step();
                self.Turn(0, 1);
                Check("snake self collision", self.Step() == SnakeStep.HitSelf && !self.Alive);

                string probe = Path.Combine(work, "probe song.mp3");
                File.WriteAllText(probe, "x");
                Check("download path resolve",
                    MediaDownloader.ResolvePath(@"C:\NO-SUCH~1\probe song.mp3", work) == probe);

                string sample = Path.Combine(work, "sample.mp4");
                string genOut;
                int genCode = RunProcess(ffmpeg,
                    "-hide_banner -loglevel error -f lavfi -i testsrc2=size=320x240:rate=25 -t 2 " +
                    "-pix_fmt yuv420p -y " + Fmt.Quote(sample), out genOut);
                Check("generate sample", genCode == 0 && File.Exists(sample) && new FileInfo(sample).Length > 1000);

                VideoInfo info = Ffmpeg.Probe(sample);
                Line("probe: " + info.Describe() + ", codec " + info.Codec);
                Check("probe size", info.Width == 320 && info.Height == 240);
                Check("probe duration", info.Duration > 1.5 && info.Duration < 3.0);

                string fast = Path.Combine(work, "fast.gif");
                int fastMax = RunConversion(info, sample, fast, true);
                Check("fast gif", IsGif(fast) && new FileInfo(fast).Length > 1000);
                Check("fast progress", fastMax >= 90);
                Line("  fast.gif: " + Fmt.Size(new FileInfo(fast).Length) + ", progress max " + fastMax + "%");

                string slow = Path.Combine(work, "two-pass.gif");
                int slowMax = RunConversion(info, sample, slow, false);
                Check("two-pass gif", IsGif(slow) && new FileInfo(slow).Length > 1000);
                Check("two-pass progress", slowMax >= 90);
                Line("  two-pass.gif: " + Fmt.Size(new FileInfo(slow).Length) + ", progress max " + slowMax + "%");

                string still = Path.Combine(work, "still.png");
                string stillOut;
                int stillCode = RunProcess(ffmpeg,
                    "-hide_banner -loglevel error -f lavfi -i testsrc2=size=400x300 -frames:v 1 -y " +
                    Fmt.Quote(still), out stillOut);
                Check("generate image", stillCode == 0 && File.Exists(still));

                VideoInfo imageInfo = Ffmpeg.Probe(still);
                Line("image probe: " + imageInfo.Describe());
                Check("probe image", imageInfo.IsImage && imageInfo.Width == 400 && imageInfo.Height == 300);

                string imageFast = Path.Combine(work, "image-fast.gif");
                int imageFastMax = RunConversion(imageInfo, still, imageFast, true);
                Check("image fast gif", IsGif(imageFast) && new FileInfo(imageFast).Length > 1000);
                Check("image fast progress", imageFastMax >= 90);

                string imageSlow = Path.Combine(work, "image-two-pass.gif");
                int imageSlowMax = RunConversion(imageInfo, still, imageSlow, false);
                Check("image two-pass gif", IsGif(imageSlow) && new FileInfo(imageSlow).Length > 1000);
                Check("image two-pass progress", imageSlowMax >= 90);
                Line("  image gif: " + Fmt.Size(new FileInfo(imageSlow).Length) + ", progress max " + imageSlowMax + "%");

                string imageOrigFast = Path.Combine(work, "image-orig-fast.gif");
                int imageOrigFastMax = RunConversion(imageInfo, still, imageOrigFast, true, 0);
                Check("image original width fast gif", IsGif(imageOrigFast) && new FileInfo(imageOrigFast).Length > 1000);
                Check("image original width fast progress", imageOrigFastMax >= 90);

                string imageOrigSlow = Path.Combine(work, "image-orig-two-pass.gif");
                int imageOrigSlowMax = RunConversion(imageInfo, still, imageOrigSlow, false, 0);
                Check("image original width two-pass gif", IsGif(imageOrigSlow) && new FileInfo(imageOrigSlow).Length > 1000);
                Check("image original width two-pass progress", imageOrigSlowMax >= 90);

                string longInput = Path.Combine(work, "long.mp4");
                string longOut;
                int longCode = RunProcess(ffmpeg,
                    "-hide_banner -loglevel error -f lavfi -i testsrc2=size=640x480:rate=30 -t 6 " +
                    "-pix_fmt yuv420p -y " + Fmt.Quote(longInput), out longOut);
                Check("generate long sample", longCode == 0 && File.Exists(longInput));
                if (longCode == 0)
                {
                    ConversionOptions cancelOptions = new ConversionOptions();
                    cancelOptions.Input = longInput;
                    cancelOptions.Output = Path.Combine(work, "cancel.gif");
                    cancelOptions.Palette = Path.Combine(work, "cancel-palette.png");
                    cancelOptions.Width = 640;
                    cancelOptions.Fps = 15;
                    cancelOptions.Colors = 256;
                    cancelOptions.Dither = "bayer";
                    cancelOptions.Loop = true;
                    cancelOptions.Info = Ffmpeg.Probe(longInput);

                    Converter cancelConverter = new Converter();
                    System.Threading.Thread killer = new System.Threading.Thread(delegate()
                    {
                        System.Threading.Thread.Sleep(450);
                        cancelConverter.Cancel();
                    });
                    killer.Start();
                    bool cancelled = false;
                    try
                    {
                        cancelConverter.Run(cancelOptions, delegate(int p, string s) { });
                    }
                    catch (ConversionCancelledException)
                    {
                        cancelled = true;
                    }
                    catch (Exception ex)
                    {
                        Line("  cancel error: " + ex.Message);
                    }
                    killer.Join();
                    Check("cancel conversion", cancelled);
                }

                string ui = Path.Combine(work, "ui.png");
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (MainForm form = new MainForm())
                    {
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-4000, -4000);
                        form.Show();
                        if (Ui.Dark) Invoke(form, "ToggleTheme");
                        WaitUntil(delegate { return GetField(form, "ffmpegReady") is bool && (bool)GetField(form, "ffmpegReady"); }, 6000);
                        Invoke(form, "LoadVideo", sample);
                        WaitUntil(delegate { return GetField(form, "info") != null; }, 10000);
                        SaveShot(form, ui);
                        Check("ui loaded screenshot", File.Exists(ui) && new FileInfo(ui).Length > 2000);
                        PillButton uiConvert = GetField(form, "convertButton") as PillButton;
                        Check("ui convert enabled", uiConvert != null && uiConvert.Enabled);

                        Check("ui presets removed",
                            GetField(form, "presetCompact") == null &&
                            GetField(form, "presetBalanced") == null &&
                            GetField(form, "presetQuality") == null);
                        FlatInput fpsBox = GetField(form, "fpsInput") as FlatInput;
                        Check("ui fps default 30", fpsBox != null && fpsBox.Value == "30");
                        Check("ui colors slider removed", GetField(form, "colorsSlider") == null);
                        Check("ui gamepad button", GetField(form, "gameButton") is GamepadButton);

                        Invoke(form, "FinishSuccess", fast);
                        Application.DoEvents();
                        string resultShot = Path.Combine(work, "ui-result.png");
                        SaveShot(form, resultShot);
                        Check("ui result screenshot", File.Exists(resultShot) && new FileInfo(resultShot).Length > 2000);

                        Invoke(form, "ToggleTheme");
                        Application.DoEvents();
                        string darkShot = Path.Combine(work, "ui-dark.png");
                        SaveShot(form, darkShot);
                        Check("ui dark screenshot", File.Exists(darkShot) && new FileInfo(darkShot).Length > 2000);

                        Invoke(form, "ShowProgress", "Строим палитру", "2 с · 30 fps · 320 px");
                        SetField(form, "busy", true);
                        Invoke(form, "UpdateActions");
                        SlimBar bar = GetField(form, "progressBar") as SlimBar;
                        if (bar != null) bar.Value = 42;
                        Label pct = GetField(form, "percentLabel") as Label;
                        if (pct != null) pct.Text = "42%";
                        Application.DoEvents();
                        string progressShot = Path.Combine(work, "ui-progress.png");
                        SaveShot(form, progressShot);
                        Check("ui progress screenshot", File.Exists(progressShot) && new FileInfo(progressShot).Length > 2000);

                        SetField(form, "busy", false);
                        Invoke(form, "UpdateActions");
                        Invoke(form, "LoadVideo", still);
                        WaitUntil(delegate
                        {
                            VideoInfo vi = GetField(form, "info") as VideoInfo;
                            return vi != null && vi.IsImage;
                        }, 10000);
                        FlatInput uiDuration = GetField(form, "durationInput") as FlatInput;
                        FlatInput uiFps = GetField(form, "fpsInput") as FlatInput;
                        Check("ui image fields disabled",
                            uiDuration != null && !uiDuration.Enabled && uiFps != null && !uiFps.Enabled);
                        Application.DoEvents();
                        string imageShot = Path.Combine(work, "ui-image.png");
                        SaveShot(form, imageShot);
                        Check("ui image screenshot", File.Exists(imageShot) && new FileInfo(imageShot).Length > 2000);

                        string previousResult = GetField(form, "resultPath") as string;
                        Invoke(form, "StartConversion");
                        WaitUntil(delegate
                        {
                            string rp = GetField(form, "resultPath") as string;
                            return !(bool)GetField(form, "busy") && rp != null && rp != previousResult;
                        }, 30000);
                        string uiImageGif = GetField(form, "resultPath") as string;
                        Check("ui image conversion",
                            uiImageGif != null && uiImageGif != previousResult && IsGif(uiImageGif));

                        Invoke(form, "LoadVideo", sample);
                        WaitUntil(delegate
                        {
                            VideoInfo vi = GetField(form, "info") as VideoInfo;
                            return vi != null && !vi.IsImage;
                        }, 10000);
                        Check("ui video fields restored",
                            uiDuration != null && uiFps != null && uiDuration.Enabled && uiFps.Enabled);

                        if (!Ui.Dark) Invoke(form, "ToggleTheme");
                        form.Close();
                    }
                    Line("  ui.png: " + Fmt.Size(new FileInfo(ui).Length));
                }
                catch (Exception ex)
                {
                    Check("ui screenshot", false);
                    Line("  ui error: " + ex.Message);
                }

                Check("tiktok url detect",
                    TikTok.LooksLikeUrl("https://www.tiktok.com/@user/video/1234567890123456789") &&
                    TikTok.LooksLikeUrl("vt.tiktok.com/ZPabc123/") &&
                    !TikTok.LooksLikeUrl("https://example.com/video/1"));

                try
                {
                    Ui.SetDark(true);
                    using (SnakeForm game = new SnakeForm())
                    {
                        game.StartPosition = FormStartPosition.Manual;
                        game.Location = new Point(-4000, -4000);
                        game.Show();
                        Application.DoEvents();
                        string shot = Path.Combine(work, "ui-snake.png");
                        SaveShot(game, shot);
                        Check("ui snake screenshot", File.Exists(shot) && new FileInfo(shot).Length > 2000);
                        game.Close();
                    }
                }
                catch (Exception ex)
                {
                    Check("ui snake screenshot", false);
                    Line("  snake ui error: " + ex.Message);
                }

                try
                {
                    Ui.SetDark(true);
                    using (TikTokForm dialog = new TikTokForm())
                    {
                        dialog.StartPosition = FormStartPosition.Manual;
                        dialog.Location = new Point(-4000, -4000);
                        dialog.Show();
                        Application.DoEvents();
                        string shot = Path.Combine(work, "ui-tiktok.png");
                        SaveShot(dialog, shot);
                        Check("ui tiktok screenshot", File.Exists(shot) && new FileInfo(shot).Length > 2000);
                        dialog.Close();
                    }
                }
                catch (Exception ex)
                {
                    Check("ui tiktok screenshot", false);
                    Line("  tiktok ui error: " + ex.Message);
                }

                try
                {
                    Ui.SetDark(true);
                    using (MediaDownloadForm dialog = new MediaDownloadForm(false))
                    {
                        dialog.StartPosition = FormStartPosition.Manual;
                        dialog.Location = new Point(-4000, -4000);
                        dialog.Show();
                        Application.DoEvents();
                        string shot = Path.Combine(work, "ui-download.png");
                        SaveShot(dialog, shot);
                        Check("ui download screenshot", File.Exists(shot) && new FileInfo(shot).Length > 2000);

                        PillButton video = GetField(dialog, "videoPill") as PillButton;
                        PillButton audio = GetField(dialog, "audioPill") as PillButton;
                        if (audio != null) Click(audio);
                        Application.DoEvents();
                        Check("ui download mp3 selected", video != null && !video.Active
                            && audio != null && audio.Active);
                        if (video != null) Click(video);
                        Application.DoEvents();
                        Check("ui download video selected", video != null && video.Active
                            && audio != null && !audio.Active);
                        dialog.Close();
                    }

                    using (MediaDownloadForm dialog = new MediaDownloadForm(true))
                    {
                        dialog.StartPosition = FormStartPosition.Manual;
                        dialog.Location = new Point(-4000, -4000);
                        dialog.Show();
                        Application.DoEvents();
                        string shot = Path.Combine(work, "ui-spotify.png");
                        SaveShot(dialog, shot);
                        Check("ui spotify screenshot", File.Exists(shot) && new FileInfo(shot).Length > 2000);

                        PillButton wav = GetField(dialog, "wavPill") as PillButton;
                        PillButton mp3 = GetField(dialog, "audioPill") as PillButton;
                        if (wav != null) Click(wav);
                        Application.DoEvents();
                        string wavShot = Path.Combine(work, "ui-spotify-wav.png");
                        SaveShot(dialog, wavShot);
                        Check("ui spotify wav selected", wav != null && wav.Active
                            && mp3 != null && !mp3.Active
                            && File.Exists(wavShot) && new FileInfo(wavShot).Length > 2000);

                        if (mp3 != null) Click(mp3);
                        Application.DoEvents();
                        Check("ui spotify mp3 selected", wav != null && !wav.Active
                            && mp3 != null && mp3.Active);

                        dialog.Close();
                    }
                }
                catch (Exception ex)
                {
                    Check("ui download screenshot", false);
                    Line("  download ui error: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                failures++;
                Line("EXCEPTION: " + ex);
            }

            Line(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");
            try
            {
                File.WriteAllText(logPath, Log.ToString(), Encoding.UTF8);
            }
            catch { }
            code = failures == 0 ? 0 : 2;
            return code;
        }

        private static int RunConversion(VideoInfo info, string input, string output, bool fast, int width = 320)
        {
            ConversionOptions options = new ConversionOptions();
            options.Input = input;
            options.Output = output;
            options.Palette = Path.Combine(Path.GetDirectoryName(output), "palette-" + (fast ? "f" : "s") + ".png");
            options.TempDir = Path.GetDirectoryName(output);
            options.Width = width;
            options.Fps = 30;
            options.Colors = 256;
            options.Dither = "bayer";
            options.Loop = true;
            options.Fast = fast;
            options.Info = info;

            int max = 0;
            Converter converter = new Converter();
            converter.Run(options, delegate(int percent, string stage)
            {
                if (percent > max) max = percent;
            });
            return max;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(target);
        }

        private static void Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null) method.Invoke(target, arguments);
        }

        private static void Click(Control control)
        {
            MethodInfo method = typeof(Control).GetMethod("OnClick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null) method.Invoke(control, new object[] { EventArgs.Empty });
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(target, value);
        }

        private static void WaitUntil(Func<bool> predicate, int timeoutMs)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                Application.DoEvents();
                if (predicate()) return;
                System.Threading.Thread.Sleep(20);
            }
        }

        private static void SaveShot(Form form, string path)
        {
            using (Bitmap bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static bool IsGif(string path)        {
            if (!File.Exists(path)) return false;
            byte[] head = new byte[6];
            using (FileStream fs = File.OpenRead(path))
            {
                if (fs.Read(head, 0, 6) != 6) return false;
            }
            string sig = Encoding.ASCII.GetString(head);
            return sig == "GIF89a" || sig == "GIF87a";
        }

        private static int RunProcess(string exe, string args, out string output)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            StringBuilder sb = new StringBuilder();
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) sb.AppendLine(e.Data);
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) sb.AppendLine(e.Data);
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                output = sb.ToString();
                return p.ExitCode;
            }
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0 ? lines[0].Trim() : "";
        }

        private static void Check(string name, bool ok)
        {
            if (!ok) failures++;
            Line((ok ? "OK   " : "FAIL ") + name);
        }

        private static void Line(string text)
        {
            Log.AppendLine(text);
        }
    }
}
