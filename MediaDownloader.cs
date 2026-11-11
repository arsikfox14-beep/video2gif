using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Video2Gif
{
    internal sealed class MediaTrack
    {
        public string Title = "";
        public string Artist = "";
        public int DurationMs;

        public string Query
        {
            get { return (Artist + " " + Title).Trim(); }
        }
    }

    internal static class SpotifyLinks
    {
        private static readonly Regex WebLink = new Regex(
            @"open\.spotify\.com/(?:intl-[a-z-]+/)?(track|album|playlist)/([A-Za-z0-9]+)",
            RegexOptions.IgnoreCase);
        private static readonly Regex UriLink = new Regex(
            @"spotify:(track|album|playlist):([A-Za-z0-9]+)",
            RegexOptions.IgnoreCase);

        static SpotifyLinks()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { }
        }

        public static bool LooksLike(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.IndexOf("spotify.com", StringComparison.OrdinalIgnoreCase) >= 0
                || url.IndexOf("spoti.fi", StringComparison.OrdinalIgnoreCase) >= 0
                || url.IndexOf("spotify.link", StringComparison.OrdinalIgnoreCase) >= 0
                || url.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase);
        }

        public static List<MediaTrack> Resolve(string url, Action<int, string> report)
        {
            string embed = BuildEmbedUrl(url);
            report(-1, "Получаем данные из Spotify…");
            string html = Fetch(embed);
            return ParseHtml(html);
        }

        internal static List<MediaTrack> ParseHtml(string html)
        {
            Dictionary<string, object> entity = FindEntity(html);
            if (entity == null)
            {
                throw new ConversionException("Не удалось прочитать ссылку Spotify.");
            }

            List<MediaTrack> tracks = new List<MediaTrack>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            object[] list = Get(entity, "trackList") as object[];
            if (list != null && list.Length > 0)
            {
                for (int i = 0; i < list.Length; i++)
                {
                    Dictionary<string, object> item = list[i] as Dictionary<string, object>;
                    if (item == null) continue;
                    if (!Playable(item)) continue;
                    AddTrack(tracks, seen,
                        Str(Get(item, "title")),
                        Str(Get(item, "subtitle")),
                        Int(Get(item, "duration")));
                }
            }
            else
            {
                if (!Playable(entity))
                {
                    throw new ConversionException("Этот трек Spotify недоступен для прослушивания.");
                }
                AddTrack(tracks, seen,
                    Str(Get(entity, "name")),
                    JoinArtists(entity),
                    Int(Get(entity, "duration")));
            }

            if (tracks.Count == 0)
            {
                throw new ConversionException("В ссылке Spotify не нашлось доступных треков.");
            }
            return tracks;
        }

        private static bool Playable(Dictionary<string, object> item)
        {
            object value = Get(item, "isPlayable");
            return !(value is bool) || (bool)value;
        }

        private static void AddTrack(List<MediaTrack> tracks, HashSet<string> seen,
            string title, string artist, int duration)
        {
            title = (title ?? "").Trim();
            artist = (artist ?? "").Trim();
            if (title.Length == 0) return;
            string key = (artist + "|" + title).ToLowerInvariant();
            if (!seen.Add(key)) return;

            MediaTrack track = new MediaTrack();
            track.Title = title;
            track.Artist = artist;
            track.DurationMs = duration;
            tracks.Add(track);
        }

        private static string JoinArtists(Dictionary<string, object> entity)
        {
            object[] artists = Get(entity, "artists") as object[];
            if (artists == null || artists.Length == 0) return "";
            List<string> names = new List<string>();
            for (int i = 0; i < artists.Length; i++)
            {
                Dictionary<string, object> artist = artists[i] as Dictionary<string, object>;
                if (artist == null) continue;
                string name = Str(Get(artist, "name")).Trim();
                if (name.Length > 0) names.Add(name);
            }
            return string.Join(", ", names.ToArray());
        }

        private static string BuildEmbedUrl(string url)
        {
            Match match = WebLink.Match(url ?? "");
            if (match.Success)
            {
                return "https://open.spotify.com/embed/" + match.Groups[1].Value + "/" + match.Groups[2].Value;
            }

            match = UriLink.Match(url ?? "");
            if (match.Success)
            {
                return "https://open.spotify.com/embed/" + match.Groups[1].Value + "/" + match.Groups[2].Value;
            }

            string final = FollowRedirect(url);
            match = WebLink.Match(final ?? "");
            if (match.Success)
            {
                return "https://open.spotify.com/embed/" + match.Groups[1].Value + "/" + match.Groups[2].Value;
            }

            throw new ConversionException("Это не похоже на ссылку Spotify (трек, альбом или плейлист).");
        }

        private static string FollowRedirect(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.AllowAutoRedirect = true;
                request.Method = "GET";
                request.Timeout = 20000;
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return response.ResponseUri != null ? response.ResponseUri.ToString() : url;
                }
            }
            catch
            {
                return url;
            }
        }

        private static string Fetch(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 20000;
                request.ReadWriteTimeout = 20000;
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                request.Accept = "text/html,application/xhtml+xml";
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                throw new ConversionException("Не удалось открыть ссылку Spotify: " + ex.Message);
            }
        }

        private static Dictionary<string, object> FindEntity(string html)
        {
            int marker = html.IndexOf("id=\"__NEXT_DATA__\"", StringComparison.Ordinal);
            if (marker < 0) return null;
            int start = html.IndexOf('>', marker);
            if (start < 0) return null;
            start++;
            int end = html.IndexOf("</script>", start, StringComparison.Ordinal);
            if (end < 0) return null;

            string json = html.Substring(start, end - start);
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 8 * 1024 * 1024;
                object root = serializer.DeserializeObject(json);
                object state = Dig(root, "props", "pageProps", "state");
                object data = Dig(state, "data");
                return Dig(data, "entity") as Dictionary<string, object>;
            }
            catch
            {
                return null;
            }
        }

        private static object Dig(object root, params string[] path)
        {
            object current = root;
            for (int i = 0; i < path.Length; i++)
            {
                Dictionary<string, object> map = current as Dictionary<string, object>;
                if (map == null) return null;
                object next;
                if (!map.TryGetValue(path[i], out next)) return null;
                current = next;
            }
            return current;
        }

        private static object Get(Dictionary<string, object> map, string key)
        {
            if (map == null) return null;
            object value;
            return map.TryGetValue(key, out value) ? value : null;
        }

        private static string Str(object value)
        {
            return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int Int(object value)
        {
            if (value == null) return 0;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }
    }

    internal sealed class MediaDownloader
    {
        private static readonly Regex Progress = new Regex(
            @"^\[download\]\s+([0-9]+(?:\.[0-9]+)?)%", RegexOptions.Compiled);

        private readonly object gate = new object();
        private readonly List<string> files = new List<string>();
        private Process active;
        private volatile bool cancelled;

        public void Cancel()
        {
            cancelled = true;
            Process process;
            lock (gate) { process = active; }
            if (process == null) return;
            try
            {
                ProcessStartInfo kill = new ProcessStartInfo(
                    "taskkill", "/PID " + process.Id + " /T /F");
                kill.CreateNoWindow = true;
                kill.UseShellExecute = false;
                Process.Start(kill);
            }
            catch
            {
                try { process.Kill(); } catch { }
            }
        }

        public List<string> Download(string url, bool spotify, bool audio, string audioFormat,
            string folder, Action<int, string> report)
        {
            cancelled = false;
            lock (gate) { files.Clear(); }

            report(-1, "Готовим загрузчик…");
            YtDlpBundle.Ensure();
            Directory.CreateDirectory(folder);

            List<MediaTrack> tracks;
            if (spotify)
            {
                tracks = SpotifyLinks.Resolve(url, report);
            }
            else
            {
                tracks = new List<MediaTrack>();
                MediaTrack single = new MediaTrack();
                single.Title = audio ? "Музыка" : "Видео";
                tracks.Add(single);
            }

            int total = tracks.Count;
            int failed = 0;
            string lastError = null;
            for (int i = 0; i < total; i++)
            {
                if (cancelled) throw new ConversionCancelledException();

                MediaTrack track = tracks[i];
                string target;
                if (spotify)
                {
                    report(-1, total > 1
                        ? "Ищем на YouTube " + (i + 1) + " из " + total + " · " + track.Title
                        : "Ищем на YouTube: " + track.Title);
                    target = FindTarget(track);
                    if (cancelled) throw new ConversionCancelledException();
                }
                else
                {
                    target = url;
                }

                int index = i;
                string stage;
                if (spotify)
                {
                    stage = total > 1
                        ? "Скачиваем " + (i + 1) + " из " + total + " · " + track.Title
                        : "Скачиваем · " + track.Title;
                }
                else
                {
                    stage = audio ? "Скачиваем музыку…" : "Скачиваем видео…";
                }

                try
                {
                    RunOne(target, audio, audioFormat, folder, delegate(int percent)
                    {
                        int overall = total > 1
                            ? (int)Math.Round((index + percent / 100.0) * 100.0 / total)
                            : percent;
                        report(overall, stage);
                    });
                }
                catch (ConversionCancelledException)
                {
                    throw;
                }
                catch (ConversionException ex)
                {
                    if (!spotify) throw;
                    failed++;
                    lastError = ex.Message;
                    report(total > 1 ? (int)Math.Round((index + 1) * 100.0 / total) : 100,
                        "Пропущено: " + track.Title);
                }
            }

            lock (gate)
            {
                if (files.Count == 0)
                {
                    throw new ConversionException(lastError != null
                        ? lastError
                        : "Ничего не скачалось.");
                }
                if (failed > 0 && spotify)
                {
                    report(100, "Готово, пропущено треков: " + failed);
                }
                return new List<string>(files);
            }
        }

        private string FindTarget(MediaTrack track)
        {
            string query = track.Query;
            string id = FindId(query, DurationFilter(track));
            if (id != null) return "https://www.youtube.com/watch?v=" + id;

            // Если подходящего по длительности видео нет, берём лучший
            // результат обычного поиска — как в версии 2.2.
            id = FindId(query, null);
            if (id != null) return "https://www.youtube.com/watch?v=" + id;
            return "ytsearch1:" + query;
        }

        private string FindId(string query, string matchFilter)
        {
            StringBuilder args = new StringBuilder();
            Add(args, "--ignore-config");
            Add(args, "--no-warnings");
            Add(args, "--color", "never");
            Add(args, "--simulate");
            Add(args, "--flat-playlist");
            Add(args, "--no-playlist");
            Add(args, "--print", "%(id)s");
            if (!string.IsNullOrEmpty(matchFilter))
            {
                Add(args, "--match-filter", matchFilter);
            }
            Add(args, "ytsearch5:" + query);

            ProcessStartInfo psi = new ProcessStartInfo(YtDlpBundle.Path, args.ToString());
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            StringBuilder output = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) output.AppendLine(e.Data.Trim());
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { };

                lock (gate) { active = process; }
                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch
                {
                    return null;
                }
                finally
                {
                    lock (gate) { active = null; }
                }
            }

            if (cancelled) return null;

            string[] lines = output.ToString().Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length < 6 || line.Length > 32) continue;
                bool valid = true;
                for (int j = 0; j < line.Length; j++)
                {
                    char c = line[j];
                    if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid) return line;
            }
            return null;
        }

        internal static string DurationFilter(MediaTrack track)
        {
            if (track.DurationMs <= 0) return null;
            int seconds = track.DurationMs / 1000;
            if (seconds < 20) return null;

            // YouTube-версии трека обычно отличаются на несколько секунд,
            // поэтому допускаем небольшой запас, но отсекаем клипы и миксы.
            int tolerance = seconds < 90 ? 6 : 10;
            int low = Math.Max(0, seconds - tolerance);
            int high = seconds + tolerance;
            return "duration >= " + low.ToString(CultureInfo.InvariantCulture)
                + " & duration <= " + high.ToString(CultureInfo.InvariantCulture);
        }

        private const string BestVideoFormat = "bv*[ext=mp4]+ba[ext=m4a]/bv*+ba/b";
        private const string CombinedVideoFormat = "b[ext=mp4]/b/bv*+ba/b";

        private sealed class Attempt
        {
            public readonly string Format;
            public readonly string ExtractorArgs;

            public Attempt(string format, string extractorArgs)
            {
                Format = format;
                ExtractorArgs = extractorArgs;
            }
        }

        private void RunOne(string target, bool audio, string audioFormat, string folder,
            Action<int> percent)
        {
            string ffmpegDir = Path.GetDirectoryName(FfmpegBundle.Path);
            string format = string.IsNullOrEmpty(audioFormat) ? "mp3" : audioFormat;
            string template = Path.Combine(folder, audio
                ? "%(title).200B [%(id)s] (audio).%(ext)s"
                : "%(title).200B [%(id)s].%(ext)s");
            string listFile = Path.Combine(Path.GetTempPath(),
                "video2gif-" + Guid.NewGuid().ToString("N") + ".txt");

            List<Attempt> attempts = new List<Attempt>();
            if (audio)
            {
                attempts.Add(new Attempt(null, null));
                attempts.Add(new Attempt(null, "youtube:player_client=tv_embedded"));
                attempts.Add(new Attempt(null, "youtube:player_client=android"));
            }
            else
            {
                // Сначала лучшее качество: видео и звук отдельными дорожками.
                attempts.Add(new Attempt(BestVideoFormat, null));
                // Если первый клиент не отдал звук, пробуем другой —
                // тоже в максимальном качестве.
                attempts.Add(new Attempt(BestVideoFormat, "youtube:player_client=tv_embedded"));
                // Последний шанс: готовый файл со звуком, без слияния.
                attempts.Add(new Attempt(CombinedVideoFormat, "youtube:player_client=android"));
            }

            string lastError = null;
            List<string> found = null;

            for (int i = 0; i < attempts.Count; i++)
            {
                if (cancelled) throw new ConversionCancelledException();

                HashSet<string> before = Snapshot(folder);
                DateTime attemptStart = DateTime.UtcNow;
                try { if (File.Exists(listFile)) File.Delete(listFile); } catch { }

                try
                {
                    found = RunAttempt(attempts[i], target, audio, format, folder,
                        template, ffmpegDir, listFile, percent);
                }
                catch (ConversionCancelledException)
                {
                    CleanupNew(folder, before, attemptStart);
                    throw;
                }
                catch (ConversionException ex)
                {
                    lastError = ex.Message;
                    CleanupNew(folder, before, attemptStart);
                    found = null;
                    if (i + 1 < attempts.Count) continue;
                    throw new ConversionException(lastError);
                }

                if (found.Count == 0)
                {
                    string recent = FindRecentFile(folder, attemptStart, audio, format);
                    if (recent != null) found.Add(recent);
                }

                if (found.Count == 0)
                {
                    lastError = "Файл скачался, но программа не смогла определить его путь.";
                    CleanupNew(folder, before, attemptStart);
                    found = null;
                    if (i + 1 < attempts.Count) continue;
                    throw new ConversionException(lastError);
                }

                break;
            }

            try { if (File.Exists(listFile)) File.Delete(listFile); } catch { }

            if (found == null || found.Count == 0)
            {
                throw new ConversionException(lastError != null ? lastError : "Ничего не скачалось.");
            }

            lock (gate)
            {
                for (int i = 0; i < found.Count; i++)
                {
                    string path = found[i];
                    bool exists = files.Exists(delegate(string known)
                    {
                        return string.Equals(known, path, StringComparison.OrdinalIgnoreCase);
                    });
                    if (!exists) files.Add(path);
                }
            }
        }

        private List<string> RunAttempt(Attempt attempt, string target, bool audio, string format,
            string folder, string template, string ffmpegDir, string listFile, Action<int> percent)
        {
            StringBuilder args = new StringBuilder();
            Add(args, "--ignore-config");
            Add(args, "--no-warnings");
            Add(args, "--color", "never");
            Add(args, "--newline");
            Add(args, "--no-playlist");
            Add(args, "--force-overwrites");
            Add(args, "--concurrent-fragments", "4");
            Add(args, "--retries", "3");
            Add(args, "--fragment-retries", "5");
            Add(args, "--ffmpeg-location", ffmpegDir);
            if (!string.IsNullOrEmpty(attempt.ExtractorArgs))
            {
                Add(args, "--extractor-args", attempt.ExtractorArgs);
            }
            Add(args, "-o", template);
            Add(args, "--print-to-file", "after_move:filepath");
            Add(args, listFile);

            if (audio)
            {
                Add(args, "-x");
                Add(args, "--audio-format", format);
                if (string.Equals(format, "mp3", StringComparison.OrdinalIgnoreCase))
                {
                    Add(args, "--audio-quality", "0");
                }
            }
            else
            {
                Add(args, "-f", attempt.Format);
                Add(args, "--merge-output-format", "mp4");
            }
            Add(args, target);

            ProcessStartInfo psi = new ProcessStartInfo(YtDlpBundle.Path, args.ToString());
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            StringBuilder error = new StringBuilder();
            StringBuilder lastLine = new StringBuilder();
            bool collecting = false;

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    string line = e.Data.Trim();
                    if (line.Length == 0) return;

                    Match match = Progress.Match(line);
                    if (match.Success)
                    {
                        double value;
                        if (double.TryParse(match.Groups[1].Value, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out value))
                        {
                            percent((int)Math.Max(0, Math.Min(100, value)));
                        }
                    }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    string line = e.Data.Trim();
                    if (line.Length == 0) return;

                    if (line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                    {
                        error.Length = 0;
                        error.Append(line);
                        collecting = true;
                        return;
                    }

                    if (collecting)
                    {
                        if (line.StartsWith("[download]", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("[Merger]", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                        if (error.Length < 700) error.Append(' ').Append(line);
                        return;
                    }

                    lastLine.Length = 0;
                    lastLine.Append(line);
                };

                lock (gate) { active = process; }
                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                finally
                {
                    lock (gate) { active = null; }
                }

                if (cancelled) throw new ConversionCancelledException();
                if (process.ExitCode != 0)
                {
                    string message = error.ToString().Trim();
                    if (message.Length <= 6 && lastLine.Length > 0)
                    {
                        message = lastLine.ToString();
                    }
                    if (message.Length <= 6)
                    {
                        message = "yt-dlp завершился с ошибкой (код " + process.ExitCode + ").";
                    }
                    throw new ConversionException(message);
                }
            }

            return ReadResultFile(listFile, folder);
        }

        private static HashSet<string> Snapshot(string folder)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string[] entries = Directory.GetFiles(folder);
                for (int i = 0; i < entries.Length; i++)
                {
                    names.Add(Path.GetFileName(entries[i]));
                }
            }
            catch { }
            return names;
        }

        private static void CleanupNew(string folder, HashSet<string> before, DateTime since)
        {
            // Неудачная попытка могла оставить видео без звука или недокачанный
            // файл. Убираем только то, чего не было до попытки.
            try
            {
                string[] exts = new string[]
                {
                    ".mp4", ".m4a", ".webm", ".mkv", ".mov", ".opus",
                    ".mp3", ".wav", ".part", ".ytdl", ".temp"
                };
                string[] entries = Directory.GetFiles(folder);
                for (int i = 0; i < entries.Length; i++)
                {
                    string path = entries[i];
                    string name = Path.GetFileName(path);
                    if (before.Contains(name)) continue;

                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    bool allowed = false;
                    for (int j = 0; j < exts.Length; j++)
                    {
                        if (ext == exts[j]) { allowed = true; break; }
                    }
                    if (!allowed) continue;
                    if (File.GetLastWriteTimeUtc(path) < since.AddSeconds(-2)) continue;

                    try { File.Delete(path); } catch { }
                }
            }
            catch { }
        }

        private static List<string> ReadResultFile(string path, string folder)
        {
            List<string> paths = new List<string>();
            try
            {
                if (!File.Exists(path)) return paths;
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;

                    string resolved = ResolvePath(line, folder);
                    if (resolved == null) continue;

                    bool exists = paths.Exists(delegate(string known)
                    {
                        return string.Equals(known, resolved, StringComparison.OrdinalIgnoreCase);
                    });
                    if (!exists) paths.Add(resolved);
                }
            }
            catch { }
            return paths;
        }

        internal static string ResolvePath(string printed, string folder)
        {
            if (File.Exists(printed)) return printed;

            // yt-dlp иногда печатает путь в коротком формате 8.3
            // (C:\Users\NAME~1\...), который может не открываться.
            // Берём только имя файла и ищем его в папке сохранения.
            string name;
            try { name = Path.GetFileName(printed); }
            catch { name = null; }
            if (string.IsNullOrEmpty(name)) return null;

            string direct = Path.Combine(folder, name);
            if (File.Exists(direct)) return direct;

            try
            {
                string[] matches = Directory.GetFiles(folder, name);
                for (int i = 0; i < matches.Length; i++)
                {
                    if (string.Equals(Path.GetFileName(matches[i]), name,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return matches[i];
                    }
                }
            }
            catch { }
            return null;
        }

        private static string FindRecentFile(string folder, DateTime startedUtc,
            bool audio, string format)
        {
            try
            {
                string[] allowed = audio
                    ? new string[] { "." + format, ".m4a", ".opus", ".webm" }
                    : new string[] { ".mp4", ".mkv", ".webm", ".mov" };

                DateTime threshold = startedUtc.AddSeconds(-5);
                string best = null;
                DateTime bestTime = DateTime.MinValue;

                string[] entries = Directory.GetFiles(folder);
                for (int i = 0; i < entries.Length; i++)
                {
                    string path = entries[i];
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".part" || ext == ".ytdl" || ext == ".temp") continue;

                    bool ok = false;
                    for (int j = 0; j < allowed.Length; j++)
                    {
                        if (ext == allowed[j]) { ok = true; break; }
                    }
                    if (!ok) continue;

                    DateTime written = File.GetLastWriteTimeUtc(path);
                    if (written < threshold) continue;
                    if (best == null || written > bestTime)
                    {
                        best = path;
                        bestTime = written;
                    }
                }
                return best;
            }
            catch
            {
                return null;
            }
        }

        private static void Add(StringBuilder args, string value)
        {
            if (args.Length > 0) args.Append(' ');
            args.Append('"').Append((value ?? "").Replace("\"", "\\\"")).Append('"');
        }

        private static void Add(StringBuilder args, string name, string value)
        {
            Add(args, name);
            Add(args, value);
        }
    }
}
