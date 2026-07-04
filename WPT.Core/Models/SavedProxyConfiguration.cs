namespace WPT.Core.Models;

public sealed class SavedProxyConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string Link { get; set; } = string.Empty;

    public string Protocol { get; set; } = string.Empty;
}
