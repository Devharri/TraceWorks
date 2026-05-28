namespace TraceWorks.Shared.Models;

public sealed class TagDefinition
{
    public int Id { get; init; }

    public string Name { get; init; } = "";

    public string Address { get; init; } = "";

    public TagDataType DataType { get; init; }

    public int ScanRateMs { get; init; }
}