namespace WPT.Core.Services.TgWsProxy;

internal sealed class CfProxyBalancer
{
    private readonly object _sync = new();
    private List<string> _domains = [];
    private Dictionary<int, string> _dcToDomain = [];

    public void UpdateDomainsList(IReadOnlyList<string> domainsList)
    {
        lock (_sync)
        {
            if (_domains.SequenceEqual(domainsList))
            {
                return;
            }

            _domains = domainsList.ToList();
            _dcToDomain = new[] { 1, 2, 3, 4, 5, 203 }
                .ToDictionary(dc => dc, _ => _domains[Random.Shared.Next(_domains.Count)]);
        }
    }

    public bool UpdateDomainForDc(int dcId, string domain)
    {
        lock (_sync)
        {
            if (_dcToDomain.TryGetValue(dcId, out var current) && current == domain)
            {
                return false;
            }

            _dcToDomain[dcId] = domain;
            return true;
        }
    }

    public IReadOnlyList<string> GetDomainsForDc(int dcId)
    {
        lock (_sync)
        {
            string? currentDomain = null;
            var result = new List<string>();
            if (_dcToDomain.TryGetValue(dcId, out var mapped))
            {
                currentDomain = mapped;
                result.Add(mapped);
            }

            var shuffled = _domains.OrderBy(_ => Random.Shared.Next()).ToList();
            foreach (var domain in shuffled)
            {
                if (domain != currentDomain)
                {
                    result.Add(domain);
                }
            }

            return result;
        }
    }
}

internal static class CfProxyDomains
{
    private const string Suffix = ".co.uk";

    private static readonly string[] Encoded =
    [
        "virkgj.com", "vmmzovy.com", "mkuosckvso.com", "zaewayzmplad.com", "twdmbzcm.com",
        "awzwsldi.com", "clngqrflngqin.com", "tjacxbqtj.com", "bxaxtxmrw.com", "dmohrsgmohcrwb.com",
        "vwbmtmoi.com", "khgrre.com", "ulihssf.com", "tmhqsdqmfpmk.com", "xwuwoqbm.com",
        "orgcnunpj.com", "zhkuldz.com", "zypoljnslxa.com", "efabnxaowuzs.com", "zaftuzsftqdq.com"
    ];

    public static IReadOnlyList<string> DefaultDomains => Encoded.Select(Decode).ToList();

    private static string Decode(string value)
    {
        if (!value.EndsWith(".com", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var prefix = value[..^4];
        var shift = prefix.Count(char.IsLetter);
        var chars = prefix.Select(c =>
        {
            if (!char.IsLetter(c))
            {
                return c;
            }

            var baseChar = char.IsLower(c) ? 'a' : 'A';
            return (char)(baseChar + (c - baseChar - shift + 26) % 26);
        });

        return string.Concat(chars) + Suffix;
    }
}
