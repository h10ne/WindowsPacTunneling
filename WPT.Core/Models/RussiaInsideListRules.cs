namespace WPT.Core.Models;

public static class RussiaInsideListRules
{
    public const string RussiaInsideId = "russia-inside";

    private static readonly HashSet<string> AllowedCompanionIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "meta",
        "twitter",
        "telegram",
        "cloudflare",
        "google-ai",
        "google-play",
        "hetzner-asn",
        "ovh-asn",
        "hodca",
        "roblox",
        "digitalocean-asn",
        "cloudfront-asn"
    };

    private static readonly HashSet<string> IncludedListIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "anime",
        "block",
        "geoblock",
        "news",
        "porn",
        "hdrezka",
        "tiktok",
        "youtube"
    };

    public static bool IsRussiaInside(string listId) =>
        listId.Equals(RussiaInsideId, StringComparison.OrdinalIgnoreCase);

    public static bool IsIncludedInRussiaInside(string listId) =>
        IncludedListIds.Contains(listId);

    public static bool IsAllowedWithRussiaInside(string listId) =>
        IsRussiaInside(listId) || AllowedCompanionIds.Contains(listId);

    public static string InfoBarText { get; } =
        "Списки YouTube, Anime, Geo Block, Block, Porn, News, HDRezka, Tik-Tok уже входят в Russia inside " +
        "и их нельзя добавить отдельно.";
}
