using S7.Net;
using TraceWorks.Shared.Models;
using TraceWorks.Shared.Services;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace TraceWorks.Protocols.S7.Services;

public sealed class S7AcquisitionService : BackgroundService
{
    private readonly PlcConfigurationService _plcConnectionService;
    private readonly TagConfigurationService _tagConfigurationService;
    private readonly Channel<SampleModel> _channel;
    private readonly MetricsService _metrics;
    private Plc? _plc;
    private PlcConnectionModel? _currentParameters;
    private readonly object _plcLock = new();
    private readonly object _sync = new();
    private CancellationTokenSource _restartCts = new();
    private volatile bool _recordingEnabled;
    private bool simulation = false; // Set to true to enable simulation mode with fake data generation
    public S7AcquisitionService(TagConfigurationService tagConfigurationService, Channel<SampleModel> channel, PlcConfigurationService plcConnectionService, MetricsService metrics)
    {
        _tagConfigurationService = tagConfigurationService;
        _tagConfigurationService.TagsChanged += OnTagsChanged;
        _channel = channel;
        _plcConnectionService = plcConnectionService;
        _metrics = metrics;
        _plcConnectionService.ConnectionSettingsChanged += OnPlcParametersChanged;
    }
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // If recording is not enabled, wait and check again
            if (!_recordingEnabled)
            {
                await Task.Delay(1000, cancellationToken);
                Console.WriteLine("Recording is disabled. Waiting...");
                StartRecording();
                continue;
            }

            if (simulation)
            {
                Console.WriteLine("Running in simulation mode. Generating fake data.");
                var simulatedTags = Enumerable.Range(1, 1000).Select(i => new TagModel
                {
                    Id = i,
                    Name = $"SimTag{i}",
                    Address = $"SIM{i}",
                    DataType = TagDataType.Float,
                    PollingIntervalMs = TagPollingInterval.Ms1000
                }).ToList();
                var random = new Random();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var cycleStopwatch = Stopwatch.StartNew();

                    foreach (var tag in simulatedTags)
                    {
                        var readStopwatch = Stopwatch.StartNew();

                        var sample = new SampleModel
                        {
                            TagId = tag.Id,
                            TagName = tag.Name,
                            TimestampUtc = DateTimeOffset.UtcNow,
                            Value = Math.Round(random.NextDouble() * 100, 3)
                        };

                        readStopwatch.Stop();

                        _metrics.RecordPlcRead(readStopwatch.Elapsed);
                        _metrics.IncrementPlcReads();

                        await _channel.Writer.WriteAsync(sample, cancellationToken);
                        _metrics.IncrementProduced(1);
                    }

                    var elapsedMs = (int)cycleStopwatch.ElapsedMilliseconds;
                    var delayMs = Math.Max(0, 50 - elapsedMs);

                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }
                }

                break; // Exit the main loop if we're in simulation mode and the cancellation token is triggered
            }
            // Ensure PLC connection is established before starting acquisition
            if (!_plc?.IsConnected ?? true)
            {
                await ConnectPlcAsync(cancellationToken);
            }
            // If we have a connection, start the acquisition loop
            if (_plc?.IsConnected == true)
            {
                // Start acquisition loop which will run until a restart is requested or service is stopped
                await RunAcquisitionLoopAsync(cancellationToken);
            }
        }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _restartCts.Cancel();
        }

        _tagConfigurationService.TagsChanged -= OnTagsChanged;
        _plcConnectionService.ConnectionSettingsChanged -= OnPlcParametersChanged;
        await base.StopAsync(cancellationToken);
        if (_plc?.IsConnected == true)
        {
            _plc?.Close();
        }

        (_plc as IDisposable)?.Dispose();
        _restartCts.Dispose();
    }
    private async Task ConnectPlcAsync(CancellationToken cancellationToken)
    {
        EnsurePlc();
        var delay = TimeSpan.FromSeconds(3);
        Console.WriteLine("Trying to connect to PLC.");
        while (_plc?.IsConnected != true && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                _plc!.Open();
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
    private void EnsurePlc()
    {
        var parameters = _plcConnectionService.GetParameters();
        lock (_plcLock)
        {
            // If we don't have a PLC instance yet, or if the connection parameters have changed, create a new PLC instance
            if (_plc is null || !parameters.Equals(_currentParameters))
            {
                _plc?.Close();
                (_plc as IDisposable)?.Dispose();

                _plc = new Plc(parameters.CpuType, parameters.IpAddress, parameters.Rack, parameters.Slot);
                _currentParameters = parameters;
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
    private async Task AcquireTagGroupAsync(TagPollingInterval pollingInterval, List<TagModel> tagsInGroup, CancellationToken cancellationToken)
    {

        Console.WriteLine($"Starting acquisition loop for {pollingInterval} with {tagsInGroup.Count} tags.");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_plc?.IsConnected != true)
                {
                    // wait a bit for reconnect, then exit this group task so the outer loop can reconnect
                    await Task.Delay(1000, cancellationToken);
                    return;
                }

                foreach (var tag in tagsInGroup)
                {
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        object? raw;
                        lock (_plcLock)  // Protect PLC read
                        {
                            raw = _plc.Read(tag.Address);
                        }
                        sw.Stop();
                        _metrics.RecordPlcRead(sw.Elapsed);
                        _metrics.IncrementPlcReads();
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
                        // Write sample to channel for processing by other services
                        await _channel.Writer.WriteAsync(sample, cancellationToken);
                        _metrics.IncrementProduced(1);
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
    private void OnPlcParametersChanged()
    {
        Console.WriteLine("PLC connection parameters changed.");
        lock (_sync)
        {
            _restartCts.Cancel();
        }
    }
    private static double ConvertToDouble(object raw)
    {
        // Convert various PLC data types to double for uniform processing. This is a simplified example and may need to be expanded based on actual tag types used.
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