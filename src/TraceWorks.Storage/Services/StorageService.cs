using TraceWorks.Shared.Models;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

namespace TraceWorks.Storage.Services;
public sealed class StorageService : BackgroundService
{
    private readonly Channel<SampleModel> _channel;
    public StorageService(Channel<SampleModel> channel)
    {
        _channel = channel;
    }
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (var sample in _channel.Reader
            .ReadAllAsync(stoppingToken))
        {
            Console.WriteLine(
                $"From channel: {sample.TagName} = {sample.Value} = {sample.TimestampUtc}");
        }
    }
}