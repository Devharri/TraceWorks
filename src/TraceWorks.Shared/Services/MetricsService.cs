using TraceWorks.Shared.Models;
using System.Diagnostics;

namespace TraceWorks.Shared.Services;
public sealed class MetricsService
{
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    private long _plcReads;
    private long _samplesProduced;
    private long _samplesConsumed;
    private long _samplesWrittenToDb;

    private long _channelDepth;
    private long _bufferSize;

    private long _plcReadTicks;
    private long _plcReadCount;

    private long _dbWriteTicks;
    private long _dbWriteCount;

    public void IncrementPlcReads()
        => Interlocked.Increment(ref _plcReads);

    public void IncrementProduced(long count = 1)
    {
        Interlocked.Add(ref _samplesProduced, count);
        Interlocked.Add(ref _channelDepth, count);
    }

    public void IncrementConsumed(long count = 1)
    {
        Interlocked.Add(ref _samplesConsumed, count);
        Interlocked.Add(ref _channelDepth, -count);
    }

    public void IncrementWrittenToDb(long count)
        => Interlocked.Add(ref _samplesWrittenToDb, count);

    public void SetBufferSize(int size)
        => Volatile.Write(ref _bufferSize, size);

    public void RecordPlcRead(TimeSpan duration)
    {
        Interlocked.Add(ref _plcReadTicks, duration.Ticks);
        Interlocked.Increment(ref _plcReadCount);
    }

    public void RecordDbWrite(TimeSpan duration)
    {
        Interlocked.Add(ref _dbWriteTicks, duration.Ticks);
        Interlocked.Increment(ref _dbWriteCount);
    }

    public MetricsSnapshotModel GetSnapshot()
    {
        var uptimeSeconds = Math.Max(_uptime.Elapsed.TotalSeconds, 1);

        var plcReadCount = Volatile.Read(ref _plcReadCount);
        var dbWriteCount = Volatile.Read(ref _dbWriteCount);

        return new MetricsSnapshotModel
        {
            UptimeSeconds = uptimeSeconds,

            PlcReads = Volatile.Read(ref _plcReads),
            SamplesProduced = Volatile.Read(ref _samplesProduced),
            SamplesConsumed = Volatile.Read(ref _samplesConsumed),
            SamplesWrittenToDb = Volatile.Read(ref _samplesWrittenToDb),

            ChannelDepth = Volatile.Read(ref _channelDepth),
            BufferSize = Volatile.Read(ref _bufferSize),

            PlcReadsPerSecond =
                Volatile.Read(ref _plcReads) / uptimeSeconds,

            SamplesPerSecond =
                Volatile.Read(ref _samplesProduced) / uptimeSeconds,

            DbRowsPerSecond =
                Volatile.Read(ref _samplesWrittenToDb) / uptimeSeconds,

            AvgPlcReadMs =
                plcReadCount == 0
                    ? 0
                    : TimeSpan.FromTicks(
                        Volatile.Read(ref _plcReadTicks) / plcReadCount)
                        .TotalMilliseconds,

            AvgDbWriteMs =
                dbWriteCount == 0
                    ? 0
                    : TimeSpan.FromTicks(
                        Volatile.Read(ref _dbWriteTicks) / dbWriteCount)
                        .TotalMilliseconds
        };
    }
}