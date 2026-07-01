namespace WPT.Core.Services.Bypass;

public sealed class BypassProgressReport
{
    public string? StatusMessage { get; init; }

    public int? ProbeCurrent { get; init; }

    public int? ProbeTotal { get; init; }

    public static BypassProgressReport Status(string message) => new() { StatusMessage = message };

    public static BypassProgressReport Probe(int current, int total) => new()
    {
        ProbeCurrent = current,
        ProbeTotal = total
    };
}
