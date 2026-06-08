using S7.Net;
using TraceWorks.Shared.Models;
using TraceWorks.Shared.Services;

namespace TraceWorks.Protocols.S7.Services;

public class S7AcquisitionService
{
    private readonly Plc _plc;
    private readonly TagConfigurationService _tagConfigurationService;
    private readonly object _plcLock = new();
    private readonly object _sync = new();

    private readonly CancellationTokenSource _serviceCts = new();
    private CancellationTokenSource _acquisitionCts = new();
    private Task? _runningAcquisitionTask;

    public S7AcquisitionService(TagConfigurationService tagConfigurationService)
    {
        _tagConfigurationService = tagConfigurationService;
        _tagConfigurationService.TagsChanged += OnTagsChanged;

        _plc = new Plc(
            CpuType.S71500,
            "192.168.1.2",
            0,
            1);
    }

    public Task StartAsync()
    {
        _runningAcquisitionTask = RunAcquisitionLoopAsync(_serviceCts.Token);
        return _runningAcquisitionTask;
    }

    public async Task AddTagsTest()
    {
        await AddTagAsync(new TagDefinition
        {
            Id = 1,
            Name = "bool",
            Address = "DB100.DBX0.0",
            DataType = TagDataType.Bool,
            PollingIntervalMs = PollingInterval.Ms3000
        }, 5000);
        await AddTagAsync(new TagDefinition
        {
            Id = 2,
            Name = "real",
            Address = "DB100.DBD2",
            DataType = TagDataType.Float,
            PollingIntervalMs = PollingInterval.Ms2000
        }, 10000);
        await AddTagAsync(new TagDefinition
        {
            Id = 3,
            Name = "int",
            Address = "DB100.DBW6",
            DataType = TagDataType.Int,
            PollingIntervalMs = PollingInterval.Ms1000
        }, 15000);
    }

    public void Stop()
    {
        _serviceCts.Cancel();
    }

    private async Task RunAcquisitionLoopAsync(CancellationToken serviceToken)
    {
        try
        {
            _plc.Open();
            Console.WriteLine("PLC connected.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PLC connection failed: {ex.Message}");
            return;
        }

        while (!serviceToken.IsCancellationRequested)
        {
            CancellationToken restartToken;

            lock (_sync)
            {
                _acquisitionCts.Cancel();
                _acquisitionCts.Dispose();
                _acquisitionCts = new CancellationTokenSource();
                restartToken = _acquisitionCts.Token;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(serviceToken, restartToken);
            var combinedToken = linkedCts.Token;

            // Group tags by their polling interval to optimize acquisition loops
            var tagsByRate = _tagConfigurationService
                .GetTags()
                .GroupBy(t => t.PollingIntervalMs)
                .ToDictionary(g => g.Key, g => g.ToList());

            Console.WriteLine($"Starting acquisition with {tagsByRate.Count} polling groups.");

            if (tagsByRate.Count == 0)
            {
                Console.WriteLine("No configured tags. Waiting for tags to be added...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), combinedToken);
                }
                catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
                {
                    // restart or stop requested
                }

                continue;
            }

            // Start acquisition tasks for each group of tags with the same polling interval
            var tasks = tagsByRate
                .Select(kvp => AcquireTagGroupAsync(kvp.Key, kvp.Value, combinedToken))
                .ToList();

            try
            {
                await Task.WhenAll(tasks);
                break; // only happens if all tasks complete normally
            }
            catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
            {
                if (serviceToken.IsCancellationRequested)
                {
                    break;
                }

                Console.WriteLine("Tag configuration changed, restarting acquisition tasks.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Acquisition tasks failed: {ex.Message}");
                Console.WriteLine(ex);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), combinedToken);
                }
                catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
                {
                    // restart or stop requested
                }
            }
        }
    }

    private async Task AcquireTagGroupAsync(PollingInterval pollingInterval, List<TagDefinition> tagsInGroup, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Starting acquisition loop for {pollingInterval} with {tagsInGroup.Count} tags.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                foreach (var tag in tagsInGroup)
                {
                    try
                    {
                        object? raw;
                        lock (_plcLock)  // Protect PLC read
                        {
                            raw = _plc.Read(tag.Address);
                        }

                        if (raw is null)
                        {
                            Console.WriteLine($"No value read for {tag.Name}");
                            continue;
                        }

                        // Convert raw value to double for uniformity in processing
                        double value = ConvertToDouble(raw);

                        var sample = new SampleModel
                        {
                            TagId = tag.Id,
                            TimestampUtc = DateTimeOffset.UtcNow,
                            Value = value
                        };

                        Console.WriteLine(
                            $"{sample.TimestampUtc:HH:mm:ss.fff} | " +
                            $"{sample.TagId} ({tag.Name}) = {sample.Value}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading {tag.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in polling interval {pollingInterval}ms loop: {ex.Message}");
            }

            // Wait for this group's polling interval before next acquisition
            await Task.Delay((int)pollingInterval, cancellationToken);
        }
    }

    private void OnTagsChanged()
    {
        Console.WriteLine("Tag configuration changed.");
        lock (_sync)
        {
            _acquisitionCts.Cancel();
        }
    }

    private async Task AddTagAsync(TagDefinition tag, int delayMs)
    {
        // Simulate async work if needed (e.g., validation, database update)
        await Task.Delay(delayMs);
        _tagConfigurationService.AddTag(tag);
    }


    private static double ConvertToDouble(object raw)
    {
        return raw switch
        {
            bool b => b ? 1.0 : 0.0,
            byte b => b,
            sbyte sb => sb,
            ushort us => us,
            short s => s,
            uint ui => ui,
            int i => i,
            ulong ul => ul,
            long l => l,
            float f => f,
            double d => d,
            decimal dec => (double)dec,
            _ => 0.0
        };
    }
}