using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using ImPluginEngine.Abstractions;
using ImPluginEngine.Abstractions.Entities;
using ImPluginEngine.Abstractions.Interfaces;
using ImPluginEngine.Helpers;
using ImPluginEngine.Types;

namespace SMAZLyricsPlugin
{
    public class SMAZLyricsPlugin : IPlugin, ILyrics
    {
        public string Name => "SMAZLyrics";
        public string Version => "0.1.0";

        public async Task GetLyrics(PluginLyricsInput input, CancellationToken ct, Action<PluginLyricsResult> updateAction)
        {
            String url = string.Format("https://search.azlyrics.com/search.php?q={0}+{1}", HttpUtility.UrlEncode(input.Artist), HttpUtility.UrlEncode(input.Title));
            var client = new HttpClient();
            String web = string.Empty;
            try
            {
                var response = await client.GetAsync(url, ct);
                var data = await response.Content.ReadAsByteArrayAsync();
                web = Encoding.UTF8.GetString(data);
            }
            catch (HttpRequestException)
            {
                return;
            }
            Regex LyricsRegex = new Regex(@"\d+\. <a href=""(?'url'[^""]+)"" target=""_blank""><b>(?'title'[^<]+)</b></a>  by <b>(?'artist'[^<]+)</b><br>", RegexOptions.Compiled);
            MatchCollection matches = LyricsRegex.Matches(web);
            foreach (Match match in matches)
            {
                if ((match.Groups["title"].Value.ToLower().Contains(input.Title.ToLower()) || input.Title.ToLower().Contains(match.Groups["title"].Value.ToLower())) &&
                    (match.Groups["artist"].Value.ToLower().Contains(input.Artist.ToLower()) || input.Artist.ToLower().Contains(match.Groups["artist"].Value.ToLower())))
                {
                    var result = new PluginLyricsResult();
                    result.Artist = match.Groups["artist"].Value;
                    result.Title = match.Groups["title"].Value;
                    result.FoundByPlugin = string.Format("{0} v{1}", Name, Version);
                    result.Lyrics = await DownloadLyrics(match.Groups["url"].Value, ct);
                    updateAction(result);
                }
            }
        }
        private async Task<String> DownloadLyrics(String url, CancellationToken ct)
        {
            var client = new HttpClient();
            string web;
            try
            {
                var response = await client.GetAsync(url, ct);
                var data = await response.Content.ReadAsByteArrayAsync();
                web = Encoding.UTF8.GetString(data);
            }
            catch (HttpRequestException)
            {
                return "Lyrics download failed.";
            }
            web = web.Replace("\r", "").Replace("\n", "");
            Regex LyricsRegex = new Regex(@"Sorry about that\. -->(?'lyrics'.*)</div>\s*<br><br>\s*<!-- MxM", RegexOptions.Compiled);
            string lyrics = "This is a sample text";
            var match = LyricsRegex.Match(web);
            if (match.Success)
            {
                lyrics = CleanLine(match.Groups["lyrics"].Value);
            }
            return lyrics;
        }

        private static string CleanLine(String line)
        {
            line = Regex.Replace(line, "<a href=[^>]+>", "", RegexOptions.IgnoreCase);
            line = line.Replace("</a>", "");
            line = line.Replace("<br />", "\n").Replace("<br/>", "\n").Replace("<br>", "\n").Replace("\n", "<br/>\n");
            line = line.Replace("<br/>\n<br/>\n", "</p>\n<p>");
            line = line.Replace("´", "'").Replace("`", "'").Replace("’", "'").Replace("‘", "'");
            line = line.Replace("…", "...").Replace(" ...", "...").Trim();
            return "<p>" + line.Trim() + "</p>";
        }
    }
}
