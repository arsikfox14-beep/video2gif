using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Video2Gif
{
    internal sealed class TikTokException : Exception
    {
        public TikTokException(string message) : base(message) { }
    }

    internal sealed class TikTokInfo
    {
        public string PostId;
        public string Author = "tiktok";
        public string VideoUrl;
        public string SuggestedName;
        public bool IsPhotoPost;
        public List<string> PhotoUrls = new List<string>();
        public string Description = "";
        public string CookieHeader = "";
    }

    internal static class TikTok
    {
        // cobalt: api/src/processing/services/tiktok.js
        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36";

        private const string ShortDomain = "https://vt.tiktok.com/";

        private static readonly Regex PostIdInPath = new Regex(
            @"/(?:video|photo)/(\d{5,25})(?:/|$)|/v/(\d{5,25})\.html", RegexOptions.IgnoreCase);

        private static readonly Regex ShortPostId = new Regex(
            @"/(?:video|photo)/(\d{5,25})", RegexOptions.IgnoreCase);

        static TikTok()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { }
        }

        public static bool LooksLikeUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string value = text.Trim();
            if (value.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (value.IndexOf(' ') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0) return false;
            return true;
        }

        public static TikTokInfo Resolve(string input)
        {
            string raw = (input ?? "").Trim();
            if (raw.Length == 0) throw new TikTokException("Вставьте ссылку на видео TikTok.");

            if (raw.IndexOf("://", StringComparison.Ordinal) < 0) raw = "https://" + raw;

            Uri uri;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new TikTokException("Это не похоже на ссылку TikTok.");
            }

            if (uri.Host != "tiktok.com" &&
                !uri.Host.EndsWith(".tiktok.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new TikTokException("Поддерживаются только ссылки tiktok.com.");
            }

            CookieContainer cookies = new CookieContainer();
            string postId = FindPostId(uri.AbsolutePath);

            if (postId == null)
            {
                string code = ShortCode(uri.AbsolutePath);
                if (code.Length == 0)
                    throw new TikTokException("В ссылке нет кода видео.");

                string resolved = ResolveShortLink(ShortDomain + code + "/", cookies);
                postId = FindPostId(new Uri(resolved).AbsolutePath);
                if (postId == null) postId = MatchShortPostId(resolved);

                if (postId == null)
                    throw new TikTokException("Не удалось разобрать короткую ссылку TikTok.");
            }

            string page = FetchPage("https://www.tiktok.com/@i/video/" + postId, cookies);

            Dictionary<string, object> root = ParseJson(ExtractUniversalData(page));
            Dictionary<string, object> scope = Obj(root, "__DEFAULT_SCOPE__");
            Dictionary<string, object> detail = Obj(scope, "webapp.video-detail");
            if (detail == null)
                throw new TikTokException("TikTok вернул неожиданный ответ. Попробуйте другую ссылку.");

            string status = Str(detail, "statusMsg");
            if (!string.IsNullOrEmpty(status))
            {
                if (status.IndexOf("delete", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new TikTokException("Видео удалено или недоступно.");
                throw new TikTokException("Видео недоступно (" + status + ").");
            }

            Dictionary<string, object> itemInfo = Obj(detail, "itemInfo");
            Dictionary<string, object> item = Obj(itemInfo, "itemStruct");
            if (item == null) throw new TikTokException("Видео недоступно или удалено.");

            bool classified;
            if (Bool(item, "isContentClassified", out classified) && classified)
                throw new TikTokException("У видео есть возрастное ограничение, TikTok не отдаёт его без входа.");

            Dictionary<string, object> author = Obj(item, "author");
            string uniqueId = Str(author, "uniqueId");
            if (string.IsNullOrEmpty(uniqueId))
                throw new TikTokException("Не удалось определить автора видео.");

            TikTokInfo info = new TikTokInfo();
            info.PostId = postId;
            info.Author = SafeName(uniqueId);
            info.Description = Str(item, "desc") ?? "";

            Dictionary<string, object> imagePost = Obj(item, "imagePost");
            object[] images = imagePost != null ? Arr(imagePost, "images") : null;
            if (images != null && images.Length > 0)
            {
                info.IsPhotoPost = true;
                info.PhotoUrls = ExtractPhotos(images);
                if (info.PhotoUrls.Count == 0)
                    throw new TikTokException("В фото-посте не нашлось картинок.");
                info.SuggestedName = "tiktok_" + info.Author + "_" + postId;
                return info;
            }

            Dictionary<string, object> video = Obj(item, "video");
            string videoUrl = Str(video, "playAddr");
            if (string.IsNullOrEmpty(videoUrl)) videoUrl = Str(video, "downloadAddr");
            if (string.IsNullOrEmpty(videoUrl)) videoUrl = BestBitrateUrl(video);

            if (string.IsNullOrEmpty(videoUrl) || videoUrl.IndexOf("http", StringComparison.OrdinalIgnoreCase) != 0)
                throw new TikTokException("TikTok не вернул ссылку на видео.");

            info.VideoUrl = videoUrl;
            info.SuggestedName = "tiktok_" + info.Author + "_" + postId + ".mp4";
            info.CookieHeader = cookies.GetCookieHeader(new Uri("https://www.tiktok.com/"));
            return info;
        }

        private static string ResolveShortLink(string url, CookieContainer cookies)
        {
            HttpWebRequest request = Create(url, cookies);
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 10;

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    string finalUrl = response.ResponseUri != null
                        ? response.ResponseUri.AbsoluteUri
                        : url;

                    if (FindPostId(new Uri(finalUrl).AbsolutePath) != null) return finalUrl;

                    string body = ReadText(response);
                    Match match = Regex.Match(body,
                        @"https?://(?:www\.)?tiktok\.com/[^""'<>\s\\]+?/(?:video|photo)/(\d{5,25})",
                        RegexOptions.IgnoreCase);
                    if (match.Success) return match.Value;

                    string id = MatchShortPostId(body);
                    if (id != null) return "https://www.tiktok.com/@i/video/" + id;

                    return finalUrl;
                }
            }
            catch (WebException ex)
            {
                throw FriendlyNetworkError(ex, "короткую ссылку");
            }
        }

        private static string FetchPage(string url, CookieContainer cookies)
        {
            HttpWebRequest request = Create(url, cookies);
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            request.Headers["Accept-Language"] = "en-US,en;q=0.9";
            request.Referer = "https://www.tiktok.com/";

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if ((int)response.StatusCode != 200)
                        throw new TikTokException("TikTok ответил кодом " + (int)response.StatusCode + ".");
                    return ReadText(response);
                }
            }
            catch (WebException ex)
            {
                throw FriendlyNetworkError(ex, "страницу TikTok");
            }
        }

        private static HttpWebRequest Create(string url, CookieContainer cookies)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = UserAgent;
            request.CookieContainer = cookies;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.KeepAlive = false;
            return request;
        }

        private static string ReadText(HttpWebResponse response)
        {
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static string ExtractUniversalData(string html)
        {
            const string marker =
                "<script id=\"__UNIVERSAL_DATA_FOR_REHYDRATION__\" type=\"application/json\">";
            int start = html.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                Match script = Regex.Match(html,
                    @"<script[^>]*id=""__UNIVERSAL_DATA_FOR_REHYDRATION__""[^>]*>(.*?)</script>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (!script.Success)
                    throw new TikTokException("TikTok не отдал данные видео. Проверьте ссылку.");
                return script.Groups[1].Value;
            }

            start += marker.Length;
            int end = html.IndexOf("</script>", start, StringComparison.Ordinal);
            if (end < 0) throw new TikTokException("TikTok вернул повреждённую страницу.");
            return html.Substring(start, end - start);
        }

        private static Dictionary<string, object> ParseJson(string json)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 16 * 1024 * 1024;
                Dictionary<string, object> root =
                    serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null) throw new TikTokException("Не удалось разобрать ответ TikTok.");
                return root;
            }
            catch (TikTokException) { throw; }
            catch
            {
                throw new TikTokException("Не удалось разобрать ответ TikTok.");
            }
        }

        private static Dictionary<string, object> Obj(Dictionary<string, object> parent, string key)
        {
            if (parent == null) return null;
            object value;
            if (!parent.TryGetValue(key, out value)) return null;
            return value as Dictionary<string, object>;
        }

        private static object[] Arr(Dictionary<string, object> parent, string key)
        {
            if (parent == null) return null;
            object value;
            if (!parent.TryGetValue(key, out value)) return null;
            return value as object[];
        }

        private static string Str(Dictionary<string, object> parent, string key)
        {
            if (parent == null) return null;
            object value;
            if (!parent.TryGetValue(key, out value) || value == null) return null;
            return value as string;
        }

        private static bool Bool(Dictionary<string, object> parent, string key, out bool result)
        {
            result = false;
            if (parent == null) return false;
            object value;
            if (!parent.TryGetValue(key, out value) || value == null) return false;
            if (value is bool)
            {
                result = (bool)value;
                return true;
            }
            return false;
        }

        private static string BestBitrateUrl(Dictionary<string, object> video)
        {
            object[] bitrates = Arr(video, "bitrateInfo");
            if (bitrates == null) return null;
            for (int i = 0; i < bitrates.Length; i++)
            {
                Dictionary<string, object> item = bitrates[i] as Dictionary<string, object>;
                Dictionary<string, object> playAddr = Obj(item, "PlayAddr");
                object[] urls = Arr(playAddr, "UrlList");
                if (urls != null && urls.Length > 0)
                {
                    string url = urls[0] as string;
                    if (!string.IsNullOrEmpty(url)) return url;
                }
            }
            return null;
        }

        private static List<string> ExtractPhotos(object[] images)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < images.Length; i++)
            {
                Dictionary<string, object> image = images[i] as Dictionary<string, object>;
                Dictionary<string, object> imageUrl = Obj(image, "imageURL");
                object[] urls = Arr(imageUrl, "urlList");
                if (urls == null) continue;

                string chosen = null;
                for (int j = 0; j < urls.Length; j++)
                {
                    string url = urls[j] as string;
                    if (string.IsNullOrEmpty(url)) continue;
                    if (chosen == null) chosen = url;
                    if (url.IndexOf(".jpeg", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        chosen = url;
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(chosen)) result.Add(chosen);
            }
            return result;
        }

        private static string FindPostId(string path)
        {
            Match match = PostIdInPath.Match(path ?? "");
            if (!match.Success) return null;
            return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        }

        private static string MatchShortPostId(string text)
        {
            Match match = ShortPostId.Match(text ?? "");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ShortCode(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string[] parts = path.Trim('/').Split('/');
            if (parts.Length == 0) return "";
            if (parts[0] == "t" && parts.Length > 1) return parts[1];
            return parts[0];
        }

        private static string SafeName(string value)
        {
            string name = Regex.Replace((value ?? "").Trim(), @"[^A-Za-z0-9._-]+", "_");
            name = name.Trim('_', '.', '-');
            return name.Length > 0 ? name : "tiktok";
        }

        private static TikTokException FriendlyNetworkError(WebException ex, string what)
        {
            string message;
            if (ex.Status == WebExceptionStatus.TrustFailure)
                message = "Не удалось проверить сертификат TikTok.";
            else if (ex.Status == WebExceptionStatus.Timeout)
                message = "TikTok не ответил вовремя. Попробуйте ещё раз.";
            else if (ex.Status == WebExceptionStatus.NameResolutionFailure ||
                     ex.Status == WebExceptionStatus.ConnectFailure)
                message = "Нет соединения с TikTok. Проверьте интернет.";
            else if (ex.Response is HttpWebResponse)
            {
                int code = (int)((HttpWebResponse)ex.Response).StatusCode;
                if (code == 403) message = "TikTok заблокировал запрос (403). Попробуйте позже.";
                else if (code == 404) message = "Страница TikTok не найдена (404).";
                else message = "TikTok ответил кодом " + code + ".";
            }
            else
            {
                message = "Не удалось загрузить " + what + ": " + ex.Message;
            }
            return new TikTokException(message);
        }
    }

    internal sealed class TikTokDownloader
    {
        private volatile bool cancelled;
        private HttpWebRequest current;

        public bool Cancelled
        {
            get { return cancelled; }
        }

        public void Cancel()
        {
            cancelled = true;
            try
            {
                if (current != null) current.Abort();
            }
            catch { }
        }

        public void Download(TikTokInfo info, string path, Action<int, string> report)
        {
            cancelled = false;
            if (info.IsPhotoPost)
            {
                DownloadPhotos(info, path, report);
                return;
            }
            DownloadVideo(info, path, report);
        }

        private void DownloadVideo(TikTokInfo info, string path, Action<int, string> report)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(info.VideoUrl);
            request.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36";
            request.Referer = "https://www.tiktok.com/";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.KeepAlive = false;
            if (!string.IsNullOrEmpty(info.CookieHeader))
            {
                try { request.Headers[HttpRequestHeader.Cookie] = info.CookieHeader; } catch { }
            }

            current = request;
            string part = path + ".part";
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream input = response.GetResponseStream())
                using (FileStream output = new FileStream(part, FileMode.Create, FileAccess.Write))
                {
                    long total = response.ContentLength;
                    long done = 0;
                    byte[] buffer = new byte[128 * 1024];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (cancelled) throw new TikTokException("Скачивание отменено.");
                        output.Write(buffer, 0, read);
                        done += read;
                        int percent = total > 0
                            ? (int)Math.Min(99, done * 100L / total)
                            : -1;
                        report(percent, "Скачиваем видео… " + Size(done));
                    }
                }
            }
            catch (WebException ex)
            {
                TryDelete(part);
                if (cancelled) throw new TikTokException("Скачивание отменено.");
                throw new TikTokException("Не удалось скачать видео: " + ex.Message);
            }
            catch
            {
                TryDelete(part);
                throw;
            }
            finally
            {
                current = null;
            }

            if (cancelled)
            {
                TryDelete(part);
                throw new TikTokException("Скачивание отменено.");
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(part, path);
            report(100, "Готово");
        }

        private void DownloadPhotos(TikTokInfo info, string folder, Action<int, string> report)
        {
            Directory.CreateDirectory(folder);
            int count = info.PhotoUrls.Count;

            for (int i = 0; i < count; i++)
            {
                if (cancelled) throw new TikTokException("Скачивание отменено.");
                string name = "photo_" + (i + 1).ToString("00") + ".jpg";
                string path = Path.Combine(folder, name);
                string part = path + ".part";
                report(i * 100 / count, "Скачиваем фото " + (i + 1) + " из " + count + "…");

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(info.PhotoUrls[i]);
                request.UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36";
                request.Referer = "https://www.tiktok.com/";
                request.Timeout = 30000;
                request.ReadWriteTimeout = 30000;
                request.KeepAlive = false;
                current = request;

                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream input = response.GetResponseStream())
                    using (FileStream output = new FileStream(part, FileMode.Create, FileAccess.Write))
                    {
                        input.CopyTo(output);
                    }
                }
                catch (WebException ex)
                {
                    TryDelete(part);
                    if (cancelled) throw new TikTokException("Скачивание отменено.");
                    throw new TikTokException("Не удалось скачать фото " + (i + 1) + ": " + ex.Message);
                }
                catch
                {
                    TryDelete(part);
                    throw;
                }
                finally
                {
                    current = null;
                }

                if (cancelled)
                {
                    TryDelete(part);
                    throw new TikTokException("Скачивание отменено.");
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(part, path);
            }

            report(100, "Готово · фото: " + count);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static string Size(long bytes)
        {
            if (bytes < 1024) return bytes + " Б";
            if (bytes < 1024L * 1024L) return (bytes / 1024.0).ToString("0.0") + " КБ";
            if (bytes < 1024L * 1024L * 1024L) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " МБ";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.00") + " ГБ";
        }
    }
}
