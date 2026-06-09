namespace TraceWorks.Shared.Models;

public sealed class SampleModel
{
    public int TagId { get; init; }

    public string TagName { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; }

    public double Value { get; init; }
}
