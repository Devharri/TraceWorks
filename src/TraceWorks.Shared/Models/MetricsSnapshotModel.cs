
namespace TraceWorks.Shared.Models;
public sealed class MetricsSnapshotModel
{
    public double UptimeSeconds { get; init; }
    public long PlcReads { get; init; }
    public long SamplesProduced { get; init; }
    public long SamplesConsumed { get; init; }
    public long SamplesWrittenToDb { get; init; }
    public long ChannelDepth { get; init; }
    public long BufferSize { get; init; }
    public double PlcReadsPerSecond { get; init; }
    public double SamplesPerSecond { get; init; }
    public double DbRowsPerSecond { get; init; }
    public double DbMbPerSecond { get; init; }
    public double AvgPlcReadMs { get; init; }
    public double AvgDbWriteMs { get; init; }
    public double MemoryMb { get; init; }
    public double ProcessMemoryMb { get; init; }
}