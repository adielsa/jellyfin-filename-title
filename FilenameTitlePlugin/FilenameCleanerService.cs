using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.FilenameTitlePlugin;

public class FilenameCleanerService
{
    private static readonly string[] QualityTags =
    [
        "2160p", "1080p", "1080i", "720p", "480p", "4K", "UHD",
        "BluRay", "BDRip", "BDRemux", "BRRip",
        "WEB-DL", "WEBRip", "WEBDL", "WEB",
        "HDTV", "DVDRip", "DVDScr", "DVD",
        "x264", "x265", "H264", "H265", "HEVC", "AVC",
        "AAC", "AC3", "DTS", "MP3", "TrueHD", "Atmos",
        "HDR", "HDR10", "SDR", "DV", "DoVi",
        "PROPER", "REPACK", "EXTENDED", "THEATRICAL", "UNRATED",
        "COMPLETE", "INTERNAL"
    ];

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".m4v", ".ts", ".m2ts", ".webm", ".mpg", ".mpeg" };

    private static readonly Regex SiteNameRegex = new(
        @"(?:www\.)?(?:\w+\.)*\w+\.(to|com|net|org|io|tv)(?=[_.\s]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Strips surrounding brackets from years like (2024) or [2024] but keeps the year itself
    private static readonly Regex BracketedYearRegex = new(
        @"[\(\[]+((19|20)\d{2})[\)\]]+",
        RegexOptions.Compiled);

    private static readonly Regex ExtraSpacesRegex = new(
        @"\s{2,}",
        RegexOptions.Compiled);

    public string Clean(string filename)
    {
        // Step 1: Strip extension only for known media formats; bare words like "Knight" must not be lost
        var ext = Path.GetExtension(filename);
        var name = VideoExtensions.Contains(ext)
            ? Path.GetFileNameWithoutExtension(filename)
            : Path.GetFileName(filename);

        // Step 2: Remove site name tokens (e.g. www.eztvx.to)
        name = SiteNameRegex.Replace(name, " ");

        // Step 3: Replace dots and underscores with spaces
        name = name.Replace('.', ' ').Replace('_', ' ');

        // Step 4: Remove quality/codec tags
        foreach (var tag in QualityTags)
        {
            name = Regex.Replace(name, $@"\b{Regex.Escape(tag)}\b", " ", RegexOptions.IgnoreCase);
        }

        // Step 5: Strip brackets around years — (2024) → 2024, [2024] → 2024
        name = BracketedYearRegex.Replace(name, "$1");

        // Step 6: Collapse whitespace
        name = ExtraSpacesRegex.Replace(name, " ").Trim();

        return name;
    }
}
