using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ItimHebrewCalendar.Services
{
    public static class UpdateChecker
    {
        private const string LatestReleaseApiUrl =
            "https://api.github.com/repos/abaye123/ItimHebrewCalendar/releases/latest";

        public class ReleaseInfo
        {
            public string TagName { get; set; } = "";
            public string Name { get; set; } = "";
            public string Body { get; set; } = "";
            public string HtmlUrl { get; set; } = "";
            public string PublishedAt { get; set; } = "";
            public string AssetUrl { get; set; } = "";
            public string AssetName { get; set; } = "";
            public long AssetSize { get; set; }
            public Version? ParsedVersion { get; set; }
        }

        public static Version GetCurrentVersion()
        {
            try
            {
                var v = typeof(UpdateChecker).Assembly.GetName().Version;
                if (v != null) return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
            catch { }
            return new Version(1, 0, 0);
        }

        public static async Task<ReleaseInfo> GetLatestReleaseAsync(CancellationToken ct = default)
        {
            using var http = CreateHttpClient();
            using var resp = await http.GetAsync(LatestReleaseApiUrl, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var info = new ReleaseInfo
            {
                TagName = GetStr(root, "tag_name"),
                Name = GetStr(root, "name"),
                Body = GetStr(root, "body"),
                HtmlUrl = GetStr(root, "html_url"),
                PublishedAt = GetStr(root, "published_at")
            };

            var verStr = info.TagName.TrimStart('v', 'V');
            if (Version.TryParse(verStr, out var ver))
                info.ParsedVersion = new Version(ver.Major, ver.Minor, Math.Max(0, ver.Build));

            if (root.TryGetProperty("assets", out var assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = GetStr(a, "name");
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        info.AssetName = name;
                        info.AssetUrl = GetStr(a, "browser_download_url");
                        if (a.TryGetProperty("size", out var sz) &&
                            sz.ValueKind == JsonValueKind.Number)
                            info.AssetSize = sz.GetInt64();
                        break;
                    }
                }
            }

            return info;
        }

        public static bool IsNewer(Version current, Version remote) => remote > current;

        public static async Task<string> DownloadAssetAsync(
            string url,
            IProgress<double>? progress,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
                throw new InvalidOperationException("חסר URL להורדת קובץ ההתקנה.");

            var dest = Path.Combine(
                Path.GetTempPath(),
                $"ItimHebrewCalendar-Update-{DateTime.Now:yyyyMMddHHmmss}.exe");

            using var http = CreateHttpClient();
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? 0;

            await using (var stream = await resp.Content.ReadAsStreamAsync(ct))
            await using (var file = File.Create(dest))
            {
                var buf = new byte[81920];
                long written = 0;
                int read;
                while ((read = await stream.ReadAsync(buf.AsMemory(0, buf.Length), ct)) > 0)
                {
                    await file.WriteAsync(buf.AsMemory(0, read), ct);
                    written += read;
                    if (total > 0 && progress != null)
                        progress.Report((double)written / total);
                }
            }

            return dest;
        }

        public static void RunInstaller(string installerPath, bool silent)
        {
            if (!File.Exists(installerPath))
                throw new FileNotFoundException("קובץ ההתקנה לא נמצא", installerPath);

            // Inno Setup silent flags. /SUPPRESSMSGBOXES suppresses any [Code] MsgBox that
            // wasn't already gated by `not WizardSilent`, so unexpected dialogs cannot block.
            var args = silent ? "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" : "";
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(installerPath) ?? ""
            };
            Process.Start(psi);
        }

        private static HttpClient CreateHttpClient()
        {
            var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("ItimHebrewCalendar", GetCurrentVersion().ToString()));
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            http.Timeout = TimeSpan.FromMinutes(5);
            return http;
        }

        private static string GetStr(JsonElement el, string name) =>
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() ?? ""
                : "";
    }
}
