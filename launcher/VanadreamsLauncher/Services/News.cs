using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vanadreams.Services
{
    public sealed class NewsItem
    {
        public string Date { get; set; } = "";
        public string By { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";

        /// <summary>"18 Oct · Terry"</summary>
        public string Byline
        {
            get
            {
                DateTime d;
                var when = DateTime.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d.ToString("d MMM", CultureInfo.InvariantCulture) : Date;
                return string.IsNullOrWhiteSpace(By) ? when : when + " · " + By;
            }
        }
    }

    /// <summary>
    /// Vana'News, the Play page's feed: news.json at the root of the repository, newest first. The last copy fetched
    /// is kept beside the settings, so the page has something to show before the fetch and when it fails.
    /// </summary>
    public static class News
    {
        public const int Shown = 8;

        public static List<NewsItem> Parse(string json)
        {
            var d = Json.ParseObject(json);
            var list = new List<NewsItem>();
            if (d == null || !d.ContainsKey("items")) return list;
            foreach (var x in Json.List(d["items"]))
            {
                var o = Json.Obj(x);
                if (o == null) continue;
                var item = new NewsItem { Date = Json.Str(o, "date", ""), By = Json.Str(o, "by", ""), Title = Json.Str(o, "title", ""), Body = Json.Str(o, "body", "") };
                if (item.Title.Length > 0 || item.Body.Length > 0) list.Add(item);
            }
            return list.Take(Shown).ToList();
        }

        public static List<NewsItem> ReadCache(string path)
        {
            try { return File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : new List<NewsItem>(); }
            catch (Exception ex) { Log.Warn("news cache unreadable: " + ex.Message); return new List<NewsItem>(); }
        }

        /// <summary>The feed, fresh when it can be fetched and the cached copy when it cannot.</summary>
        public static async Task<List<NewsItem>> FetchAsync(Downloader downloader, string url, string cachePath)
        {
            try
            {
                var text = await downloader.GetStringAsync(url);
                var items = Parse(text);
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllText(cachePath, text, new UTF8Encoding(false));
                return items;
            }
            catch (Exception ex)
            {
                Log.Warn("news fetch failed, using cache: " + ex.Message);
                return ReadCache(cachePath);
            }
        }
    }
}
