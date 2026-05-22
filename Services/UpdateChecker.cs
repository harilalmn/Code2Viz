using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Code2Viz.Services;

public sealed record UpdateInfo(Version Current, Version Latest, string ReleaseUrl, string TagName);

public static class UpdateChecker
{
    private const string ReleasesApi = "https://api.github.com/repos/harilalmn/Code2Viz/releases/latest";
    private const string UserAgent = "Code2Viz-UpdateCheck";

    private static readonly HttpClient _http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public static Version CurrentVersion
    {
        get
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');
                if (plus > 0) info = info.Substring(0, plus);
                if (Version.TryParse(info, out var v)) return v;
            }
            return asm.GetName().Version ?? new Version(0, 0, 0);
        }
    }

    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var resp = await _http.GetAsync(ReleasesApi).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<ReleasePayload>(stream).ConfigureAwait(false);
            if (payload is null || string.IsNullOrWhiteSpace(payload.TagName)) return null;

            var tag = payload.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var latest)) return null;

            var current = CurrentVersion;
            var releaseUrl = string.IsNullOrWhiteSpace(payload.HtmlUrl)
                ? $"https://github.com/harilalmn/Code2Viz/releases/tag/{payload.TagName}"
                : payload.HtmlUrl!;

            return latest > current
                ? new UpdateInfo(current, latest, releaseUrl, payload.TagName!)
                : null;
        }
        catch
        {
            // Offline, rate-limited, DNS down — silently skip. Never block startup.
            return null;
        }
    }

    public static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
        }
    }

    private sealed class ReleasePayload
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }
}
