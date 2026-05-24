namespace WindowPacTunneling.Models;

public sealed class ServiceListDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public IReadOnlyList<string> DomainFiles { get; init; } = [];

    public IReadOnlyList<string> SubnetFiles { get; init; } = [];

    public static IReadOnlyList<ServiceListDefinition> All { get; } =
    [
        new ServiceListDefinition
        {
            Id = "russia-inside",
            DisplayName = "Russia inside",
            DomainFiles = ["Russia/inside-raw.lst"]
        },
        new ServiceListDefinition
        {
            Id = "russia-outside",
            DisplayName = "Russia outside",
            DomainFiles = ["Russia/outside-raw.lst"]
        },
        new ServiceListDefinition
        {
            Id = "ukraine",
            DisplayName = "Ukraine",
            DomainFiles = ["Ukraine/inside-raw.lst"]
        },
        new ServiceListDefinition
        {
            Id = "geoblock",
            DisplayName = "Geo Block",
            DomainFiles = ["Categories/geoblock.lst"]
        },
        new ServiceListDefinition
        {
            Id = "block",
            DisplayName = "Заблокировать",
            DomainFiles = ["Categories/block.lst"]
        },
        new ServiceListDefinition
        {
            Id = "porn",
            DisplayName = "Porn",
            DomainFiles = ["Categories/porn.lst"]
        },
        new ServiceListDefinition
        {
            Id = "news",
            DisplayName = "Новости",
            DomainFiles = ["Categories/news.lst"]
        },
        new ServiceListDefinition
        {
            Id = "anime",
            DisplayName = "Anime",
            DomainFiles = ["Categories/anime.lst"]
        },
        new ServiceListDefinition
        {
            Id = "youtube",
            DisplayName = "Youtube",
            DomainFiles = ["Services/youtube.lst"]
        },
        new ServiceListDefinition
        {
            Id = "discord",
            DisplayName = "Discord",
            DomainFiles = ["Services/discord.lst"],
            SubnetFiles = ["Subnets/IPv4/discord.lst"]
        },
        new ServiceListDefinition
        {
            Id = "meta",
            DisplayName = "Meta",
            DomainFiles = ["Services/meta.lst"],
            SubnetFiles = ["Subnets/IPv4/meta.lst"]
        },
        new ServiceListDefinition
        {
            Id = "twitter",
            DisplayName = "Twitter (X)",
            DomainFiles = ["Services/twitter.lst"],
            SubnetFiles = ["Subnets/IPv4/twitter.lst"]
        },
        new ServiceListDefinition
        {
            Id = "hdrezka",
            DisplayName = "HDRezka",
            DomainFiles = ["Services/hdrezka.lst"]
        },
        new ServiceListDefinition
        {
            Id = "tiktok",
            DisplayName = "Tik-Tok",
            DomainFiles = ["Services/tiktok.lst"]
        },
        new ServiceListDefinition
        {
            Id = "telegram",
            DisplayName = "Telegram",
            DomainFiles = ["Services/telegram.lst"],
            SubnetFiles = ["Subnets/IPv4/telegram.lst"]
        },
        new ServiceListDefinition
        {
            Id = "cloudflare",
            DisplayName = "Cloudflare",
            DomainFiles = ["Services/cloudflare.lst"],
            SubnetFiles = ["Subnets/IPv4/cloudflare.lst"]
        },
        new ServiceListDefinition
        {
            Id = "google-ai",
            DisplayName = "Google AI",
            DomainFiles = ["Services/google_ai.lst"]
        },
        new ServiceListDefinition
        {
            Id = "google-play",
            DisplayName = "Google Play",
            DomainFiles = ["Services/google_play.lst"]
        },
        new ServiceListDefinition
        {
            Id = "hodca",
            DisplayName = "H.O.D.C.A",
            DomainFiles = ["Categories/hodca.lst"]
        },
        new ServiceListDefinition
        {
            Id = "roblox",
            DisplayName = "Roblox",
            DomainFiles = ["Services/roblox.lst"],
            SubnetFiles = ["Subnets/IPv4/roblox.lst"]
        },
        new ServiceListDefinition
        {
            Id = "hetzner-asn",
            DisplayName = "Hetzner ASN",
            SubnetFiles = ["Subnets/IPv4/hetzner.lst"]
        },
        new ServiceListDefinition
        {
            Id = "ovh-asn",
            DisplayName = "OVH ASN",
            SubnetFiles = ["Subnets/IPv4/ovh.lst"]
        },
        new ServiceListDefinition
        {
            Id = "digitalocean-asn",
            DisplayName = "Digital Ocean ASN",
            SubnetFiles = ["Subnets/IPv4/digitalocean.lst"]
        },
        new ServiceListDefinition
        {
            Id = "cloudfront-asn",
            DisplayName = "CloudFront ASN",
            SubnetFiles = ["Subnets/IPv4/cloudfront.lst"]
        }
    ];

    public static ServiceListDefinition? FindById(string id) =>
        All.FirstOrDefault(x => x.Id == id);
}
