using S7.Net;
using TraceWorks.Shared.Models;
using TraceWorks.Shared.Services;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

namespace TraceWorks.Protocols.S7.Services;

public sealed class S7AcquisitionService : BackgroundService
{
    private readonly Plc _plc;
    private readonly TagConfigurationService _tagConfigurationService;
    private readonly object _plcLock = new();
    private readonly object _sync = new();
    private CancellationTokenSource _restartCts = new();
    private readonly Channel<SampleModel> _channel;
    private volatile bool _recordingEnabled;
    public S7AcquisitionService(TagConfigurationService tagConfigurationService, Channel<SampleModel> channel)
    {
        _tagConfigurationService = tagConfigurationService;
        _tagConfigurationService.TagsChanged += OnTagsChanged;
        _channel = channel;
        _plc = new Plc(CpuType.S71500, "192.168.1.2", 0, 1);
    }
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_recordingEnabled)
            {
                await Task.Delay(1000, cancellationToken);
                Console.WriteLine("Recording is disabled. Waiting...");
                StartRecording();
                continue;
            }
            if (!_plc.IsConnected)
            {
                await ConnectPlcAsync(cancellationToken);
            }
            await RunAcquisitionLoopAsync(cancellationToken);
        }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _restartCts.Cancel();
        }

        _tagConfigurationService.TagsChanged -= OnTagsChanged;
        await base.StopAsync(cancellationToken);
        if (_plc.IsConnected)
        {
            _plc.Close();
        }

        (_plc as IDisposable)?.Dispose();
        _restartCts.Dispose();
    }
    private async Task ConnectPlcAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(3);
        Console.WriteLine("Trying to connect to PLC.");
        while (!_plc.IsConnected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                _plc.Open();
                Console.WriteLine("PLC reconnected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PLC connect failed: {ex.Message}");
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2));
            }
        }
    }
    public void StartRecording()
    {
        _recordingEnabled = true;
    }
    public void StopRecording()
    {
        _recordingEnabled = false;

        lock (_sync)
        {
            _restartCts.Cancel();
        }
    }
    private async Task RunAcquisitionLoopAsync(CancellationToken serviceToken)
    {
        while (!serviceToken.IsCancellationRequested)
        {
            CancellationTokenSource oldCts;
            CancellationToken restartToken;

            lock (_sync)
            {
                oldCts = _restartCts;
                _restartCts = new CancellationTokenSource();
                restartToken = _restartCts.Token;
            }

            oldCts.Cancel();
            oldCts.Dispose();

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
    private async Task AcquireTagGroupAsync(TagPollingInterval pollingInterval, List<TagDefinition> tagsInGroup, CancellationToken cancellationToken)
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

                        var sample = new SampleModel
                        {
                            TagName = tag.Name,
                            TagId = tag.Id,
                            TimestampUtc = DateTimeOffset.UtcNow,
                            Value = ConvertToDouble(raw)
                        };
                        await _channel.Writer.WriteAsync(sample, cancellationToken);
                        //Console.WriteLine($"{sample.TimestampUtc:HH:mm:ss.fff} | " + $"{sample.TagId} ({tag.Name}) = {sample.Value}");
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
            _restartCts.Cancel();
        }
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