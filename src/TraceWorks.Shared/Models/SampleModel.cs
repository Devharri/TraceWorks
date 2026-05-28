namespace TraceWorks.Shared.Models;

public sealed class SampleModel
{
    public int TagId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }

    public double Value { get; init; }
}
