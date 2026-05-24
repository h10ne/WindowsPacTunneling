namespace WPT.Wpf.ViewModels;

public sealed class ListChipItem(string id, string displayName)
{
    public string Id { get; } = id;

    public string DisplayName { get; } = displayName;
}
