using Microsoft.Extensions.Hosting;

namespace TraceWorks.Shared.Services;

public sealed class MetricsReporterService : BackgroundService
{
    private readonly MetricsService _metrics;

    public MetricsReporterService(MetricsService metrics)
    {
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var m = _metrics.GetSnapshot();

            Console.Clear();

            Console.WriteLine($"Uptime:          {m.UptimeSeconds:F0}s");
            Console.WriteLine($"PLC Reads:       {m.PlcReads}");
            Console.WriteLine($"Samples:         {m.SamplesProduced}");
            Console.WriteLine($"DB Rows:         {m.SamplesWrittenToDb}");

            Console.WriteLine();

            Console.WriteLine($"PLC Reads/s:     {m.PlcReadsPerSecond:F1}");
            Console.WriteLine($"Samples/s:       {m.SamplesPerSecond:F1}");
            Console.WriteLine($"DB Rows/s:       {m.DbRowsPerSecond:F1}");
            Console.WriteLine($"DB MB/s:         {m.DbMbPerSecond:F3}");

            Console.WriteLine();

            Console.WriteLine($"Channel Depth:   {m.ChannelDepth}");
            Console.WriteLine($"Buffer Size:     {m.BufferSize}");

            Console.WriteLine();

            Console.WriteLine($"Avg PLC Read:    {m.AvgPlcReadMs:F2} ms");
            Console.WriteLine($"Avg DB Write:    {m.AvgDbWriteMs:F2} ms");

            Console.WriteLine();
            Console.WriteLine($"Memory Usage:    {m.MemoryMb:F1} MB");
            Console.WriteLine($"Process Memory:  {m.ProcessMemoryMb:F1} MB");

            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Service is shutting down
                break;
            }
        }
    }
}