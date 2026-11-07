using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Video2Gif
{
    internal static class Fmt
    {
        public static string Num(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static bool TryNum(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Trim().Replace(',', '.');
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static string Size(long bytes)
        {
            if (bytes < 1024) return bytes + " Б";
            if (bytes < 1024L * 1024L) return (bytes / 1024.0).ToString("0.0") + " КБ";
            if (bytes < 1024L * 1024L * 1024L) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " МБ";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.00") + " ГБ";
        }

        public static string Time(double seconds)
        {
            if (seconds < 1) return "0," + ((int)Math.Round(seconds * 10)).ToString(CultureInfo.InvariantCulture) + " с";
            if (seconds < 60) return seconds.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + " с";
            int m = (int)(seconds / 60);
            int s = (int)Math.Round(seconds - m * 60);
            if (s == 60) { m++; s = 0; }
            return m + " мин " + s.ToString("00", CultureInfo.InvariantCulture) + " с";
        }

        public static string Quote(string path)
        {
            return "\"" + path + "\"";
        }
    }

    internal sealed class VideoInfo
    {
        public double Duration;
        public int Width;
        public int Height;
        public double Fps;
        public string Codec = "?";
        public bool Audio;
        public bool IsImage;

        public string Describe()
        {
            string size = Width > 0 && Height > 0 ? Width + "×" + Height : "размер ?";
            if (IsImage) return size + " · картинка";
            string dur = Duration > 0 ? Fmt.Time(Duration) : "длительность ?";
            string fps = Fps > 0 ? Fmt.Num(Math.Round(Fps)) + " fps" : "";
            string parts = size + " · " + dur;
            if (fps.Length > 0) parts += " · " + fps;
            return parts;
        }
    }

    internal sealed class ConversionException : Exception
    {
        public ConversionException(string message) : base(message) { }
    }

    internal sealed class ConversionCancelledException : Exception
    {
        public ConversionCancelledException() : base("Конвертация отменена.") { }
    }

    internal sealed class ConversionOptions
    {
        public string Input;
        public string Output;
        public string Palette;
        public string TempDir;
        public double Start;
        public double Duration; // 0 = до конца
        public int Width;
        public int Fps = 30;
        public int Colors = 256;
        public string Dither = "bayer";
        public bool Loop = true;
        public bool Fast;
        public VideoInfo Info;
    }

    internal static class FfmpegBundle
    {
        private static readonly object Gate = new object();
        private static string cached;

        public static string Path
        {
            get
            {
                lock (Gate)
                {
                    if (cached != null) return cached;
                    cached = Prepare();
                    return cached;
                }
            }
        }

        public static void Ensure()
        {
            string unused = Path;
        }

        private static string Prepare()
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Video2Gif");
            string exe = System.IO.Path.Combine(dir, "ffmpeg.exe");
            string stamp = System.IO.Path.Combine(dir, "version.txt");
            string wanted = "ffmpeg-9.0.2 ess";

            if (File.Exists(exe) && new FileInfo(exe).Length > 20L * 1024L * 1024L &&
                File.Exists(stamp) && File.ReadAllText(stamp).Trim() == wanted)
            {
                return exe;
            }

            Directory.CreateDirectory(dir);

            Assembly asm = Assembly.GetExecutingAssembly();
            Stream res = asm.GetManifestResourceStream("ffmpeg.gz");
            if (res == null)
            {
                throw new ConversionException(
                    "В сборке отсутствует FFmpeg. Пересоберите приложение по инструкции в README.");
            }

            string tmp = exe + ".tmp";
            try
            {
                using (res)
                using (GZipStream gz = new GZipStream(res, CompressionMode.Decompress))
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    gz.CopyTo(fs, 1 << 20);
                }

                if (File.Exists(exe)) File.Delete(exe);
                File.Move(tmp, exe);
                File.WriteAllText(stamp, wanted);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw new ConversionException("Не удалось распаковать FFmpeg: " + ex.Message);
            }

            return exe;
        }
    }

    internal static class Ffmpeg
    {
        private static readonly string[] ImageExt =
        {
            ".png", ".jpg", ".jpeg", ".jfif", ".webp", ".bmp", ".tif", ".tiff", ".tga", ".avif"
        };

        public static bool IsImagePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;
            ext = ext.ToLowerInvariant();
            for (int i = 0; i < ImageExt.Length; i++)
            {
                if (ImageExt[i] == ext) return true;
            }
            return false;
        }

        public static VideoInfo Probe(string input)
        {
            string stderr;
            int code;
            Capture("-hide_banner -nostdin -i " + Fmt.Quote(input), 30000, out code, out stderr);
            VideoInfo info = ParseProbe(stderr);
            if (info == null)
            {
                throw new ConversionException("Не удалось прочитать файл: формат не поддерживается или файл повреждён.");
            }
            info.IsImage = IsImagePath(input);
            return info;
        }

        private static void Capture(string args, int timeoutMs, out int code, out string stderr)
        {
            ProcessStartInfo psi = new ProcessStartInfo(FfmpegBundle.Path, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.StandardOutputEncoding = Encoding.UTF8;

            StringBuilder sb = new StringBuilder();
            code = -1;
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) sb.AppendLine(e.Data);
                };
                p.Start();
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                }
                p.WaitForExit();
                code = p.ExitCode;
            }
            stderr = sb.ToString();
        }

        private static VideoInfo ParseProbe(string output)
        {
            double duration = -1;
            Match dm = Regex.Match(output, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (dm.Success)
            {
                duration = int.Parse(dm.Groups[1].Value, CultureInfo.InvariantCulture) * 3600 +
                           int.Parse(dm.Groups[2].Value, CultureInfo.InvariantCulture) * 60 +
                           double.Parse(dm.Groups[3].Value, CultureInfo.InvariantCulture);
            }

            VideoInfo info = null;
            bool audio = false;
            string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (Regex.IsMatch(line, @"Stream #\d+:\d+.*: Audio:")) audio = true;
                if (info != null) continue;
                if (!Regex.IsMatch(line, @"Stream #\d+:\d+.*: Video:")) continue;
                if (Regex.IsMatch(line, @"attached pic", RegexOptions.IgnoreCase)) continue;
                Match codec = Regex.Match(line, @"Video:\s*([A-Za-z0-9_]+)");
                Match size = Regex.Match(line, @"(?:^|[, ])(\d{2,5})x(\d{2,5})(?:[ ,\[]|$)");
                if (!size.Success) continue;
                Match fm = Regex.Match(line, @"([\d.]+)\s+fps");
                double fps = fm.Success ? double.Parse(fm.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
                info = new VideoInfo();
                info.Codec = codec.Success ? codec.Groups[1].Value : "unknown";
                info.Width = int.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture);
                info.Height = int.Parse(size.Groups[2].Value, CultureInfo.InvariantCulture);
                info.Fps = fps > 0 ? fps : 25;
            }

            if (info == null) return null;

            Match rot = Regex.Match(output, @"rotation of (-?\d+(?:\.\d+)?) degrees");
            if (rot.Success)
            {
                double degrees = double.Parse(rot.Groups[1].Value, CultureInfo.InvariantCulture);
                if (Math.Round(Math.Abs(degrees) % 180) == 90)
                {
                    int tmp = info.Width;
                    info.Width = info.Height;
                    info.Height = tmp;
                }
            }

            info.Duration = duration > 0 ? duration : 0;
            info.Audio = audio;
            return info;
        }

        public static string ReadableError(string stderr)
        {
            string text = (stderr ?? "").Trim();
            if (Regex.IsMatch(text, "Invalid data found", RegexOptions.IgnoreCase))
                return "Файл повреждён или этот формат не поддерживается.";
            if (Regex.IsMatch(text, "No such file or directory", RegexOptions.IgnoreCase))
                return "Файл не найден.";
            if (Regex.IsMatch(text, "Permission denied", RegexOptions.IgnoreCase))
                return "Нет доступа к файлу.";
            if (Regex.IsMatch(text, "No space left", RegexOptions.IgnoreCase))
                return "На диске закончилось место.";
            if (Regex.IsMatch(text, "Error while opening encoder|encoder .* not found", RegexOptions.IgnoreCase))
                return "В сборке FFmpeg нет нужного кодировщика.";
            if (Regex.IsMatch(text, "Output file is empty", RegexOptions.IgnoreCase))
                return "Получился пустой файл — проверьте настройки.";

            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string tail = "";
            for (int i = Math.Max(0, lines.Length - 2); i < lines.Length; i++)
            {
                if (tail.Length > 0) tail += " ";
                tail += lines[i].Trim();
            }
            tail = Regex.Replace(tail, @"\s+", " ");
            return tail.Length > 0 ? "FFmpeg: " + tail : "Не удалось конвертировать файл.";
        }
    }

    internal sealed class Converter
    {
        private Process process;
        private volatile bool cancelRequested;

        public void Cancel()
        {
            cancelRequested = true;
            try
            {
                if (process != null && !process.HasExited) process.Kill();
            }
            catch { }
        }

        public void Run(ConversionOptions o, Action<int, string> report)
        {
            string ffmpeg = FfmpegBundle.Path;
            double total = ClipLength(o);
            bool image = o.Info != null && o.Info.IsImage;
            List<string> filters = new List<string>();
            if (!image) filters.Add("fps=" + o.Fps.ToString(CultureInfo.InvariantCulture));
            if (o.Width > 0 && o.Info != null && o.Info.Width > 0)
            {
                int target = Math.Min(o.Width, o.Info.Width);
                int even = Math.Max(2, target - (target % 2));
                filters.Add("scale=" + even.ToString(CultureInfo.InvariantCulture) + ":-1:flags=lanczos");
            }
            string chain = string.Join(",", filters.ToArray());
            string dither = DitherFilter(o.Dither);

            string common = "-hide_banner -loglevel error -progress pipe:1 -nostats";
            string startArg = o.Start > 0 ? " -ss " + Fmt.Num(o.Start) : "";
            string durationArg = o.Duration > 0 ? " -t " + Fmt.Num(o.Duration) : "";
            string rateArg = image ? " -r 10" : "";

            if (o.Fast)
            {
                string graph = chain + ",split[a][b];[a]palettegen=max_colors=" +
                    o.Colors.ToString(CultureInfo.InvariantCulture) + ":stats_mode=diff[p];" +
                    "[b][p]paletteuse=dither=" + dither + ":diff_mode=rectangle";
                string args = common + startArg + " -i " + Fmt.Quote(o.Input) + durationArg +
                    " -lavfi " + Fmt.Quote(graph) + " -an -loop " + (o.Loop ? "0" : "-1") +
                    rateArg + " -y " + Fmt.Quote(o.Output);
                RunPass(ffmpeg, args, total, 0, 100, "Собираем GIF", report);
            }
            else
            {
                string paletteFilter = chain + ",palettegen=max_colors=" +
                    o.Colors.ToString(CultureInfo.InvariantCulture) + ":stats_mode=diff";
                string args1 = common + startArg + " -i " + Fmt.Quote(o.Input) + durationArg +
                    " -vf " + Fmt.Quote(paletteFilter) + " -an -y " + Fmt.Quote(o.Palette);
                RunPass(ffmpeg, args1, total, 0, 30, "Строим палитру", report);

                string graph = chain + "[x];[x][1:v]paletteuse=dither=" + dither + ":diff_mode=rectangle";
                string args2 = common + startArg + " -i " + Fmt.Quote(o.Input) + " -i " + Fmt.Quote(o.Palette) +
                    durationArg + " -lavfi " + Fmt.Quote(graph) + " -an -loop " + (o.Loop ? "0" : "-1") +
                    rateArg + " -y " + Fmt.Quote(o.Output);
                RunPass(ffmpeg, args2, total, 30, 100, "Собираем GIF", report);
            }

            if (!File.Exists(o.Output) || new FileInfo(o.Output).Length < 32)
            {
                throw new ConversionException("Получился пустой файл — проверьте настройки.");
            }
        }

        private double ClipLength(ConversionOptions o)
        {
            double available = -1;
            if (o.Info != null && o.Info.Duration > 0)
            {
                available = Math.Max(0, o.Info.Duration - o.Start);
            }
            if (o.Duration > 0)
            {
                if (available > 0) return Math.Min(o.Duration, available);
                return o.Duration;
            }
            return available > 0 ? available : 0;
        }

        private static string DitherFilter(string name)
        {
            if (name == "none") return "none";
            if (name == "sierra2_4a") return "sierra2_4a";
            return "bayer:bayer_scale=4";
        }

        private void RunPass(string ffmpeg, string args, double duration, int from, int to,
            string stage, Action<int, string> report)
        {
            if (cancelRequested) throw new ConversionCancelledException();
            report(from, stage);

            ProcessStartInfo psi = new ProcessStartInfo(ffmpeg, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            StringBuilder err = new StringBuilder();
            int lastPercent = from;

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    string line = e.Data;
                    int split = line.IndexOf('=');
                    if (split <= 0) return;
                    string key = line.Substring(0, split);
                    string value = line.Substring(split + 1);
                    double micros = -1;
                    if (key == "out_time_us" || key == "out_time_ms")
                    {
                        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out micros);
                    }
                    double percent = -1;
                    if (micros >= 0 && duration > 0)
                    {
                        percent = Math.Max(0, Math.Min(100, micros / 1000000.0 / duration * 100.0));
                    }
                    else if (key == "progress" && value == "end")
                    {
                        percent = 100;
                    }
                    if (percent >= 0)
                    {
                        int overall = (int)Math.Round(from + (to - from) * percent / 100.0);
                        if (overall > lastPercent) lastPercent = overall;
                        report(overall, stage);
                    }
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    err.AppendLine(e.Data);
                    if (err.Length > 16000) err.Remove(0, err.Length - 8000);
                };

                try
                {
                    p.Start();
                }
                catch (Exception ex)
                {
                    throw new ConversionException("Не удалось запустить FFmpeg: " + ex.Message);
                }

                process = p;
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                process = null;

                if (cancelRequested)
                {
                    throw new ConversionCancelledException();
                }
                if (p.ExitCode != 0)
                {
                    throw new ConversionException(Ffmpeg.ReadableError(err.ToString()));
                }
            }

            report(to, stage);
        }
    }
}
