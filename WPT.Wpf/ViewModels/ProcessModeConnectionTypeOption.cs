using WPT.Core.Models;

namespace WPT.Wpf.ViewModels;

public sealed class ProcessModeConnectionTypeOption
{
    public ProcessModeConnectionType Value { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}
