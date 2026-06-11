using System.IO;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using TraceWorks.Shared.Models;

namespace TraceWorks.Storage.Services;

public sealed class StorageService : BackgroundService
{
    private readonly Channel<SampleModel> _channel;
    private readonly List<SampleModel> _buffer = new();
    private readonly object _bufferLock = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private const int BatchSize = 10;
    private SqliteConnection? _connection;
    private static readonly string ConnectionString = $"Data Source={Path.Combine("/Users/harrihonkanen/DATA/TraceWorks/data", "sample.db")}";
    public StorageService(Channel<SampleModel> channel)
    {
        _channel = channel;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = new SqliteConnection(ConnectionString);
        await _connection.OpenAsync(cancellationToken);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"PRAGMA journal_mode = WAL;
CREATE TABLE IF NOT EXISTS Samples (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TagId INTEGER,
    TagName TEXT NOT NULL,
    TimestampUtc TEXT NOT NULL,
    Value REAL NOT NULL
);";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await foreach (var sample in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            var shouldFlush = false;

            lock (_bufferLock)
            {
                _buffer.Add(sample);
                shouldFlush = _buffer.Count >= BatchSize;
            }

            if (shouldFlush)
            {
                await FlushAsync(cancellationToken);
            }

            Console.WriteLine($"From channel: {sample.TagName} = {sample.Value} = {sample.TimestampUtc:O}");
        }

        await FlushAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await FlushAsync(cancellationToken);

        if (_connection is not null)
        {
            _connection.Close();
            _connection.Dispose();
            _connection = null;
        }
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Starting to flush");
        if (_connection is null)
            return;

        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            SampleModel[] batch;
            lock (_bufferLock)
            {
                if (_buffer.Count == 0)
                    return;

                batch = _buffer.ToArray();
            }

            using var transaction = _connection.BeginTransaction();
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO Samples (TagId, TagName, TimestampUtc, Value) VALUES (@tagId, @tagName, @timestamp, @value);";

            var pTagId = cmd.CreateParameter(); pTagId.ParameterName = "@tagId"; cmd.Parameters.Add(pTagId);
            var pTagName = cmd.CreateParameter(); pTagName.ParameterName = "@tagName"; cmd.Parameters.Add(pTagName);
            var pTimestamp = cmd.CreateParameter(); pTimestamp.ParameterName = "@timestamp"; cmd.Parameters.Add(pTimestamp);
            var pValue = cmd.CreateParameter(); pValue.ParameterName = "@value"; cmd.Parameters.Add(pValue);

            foreach (var sample in batch)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                pTagId.Value = sample.TagId;
                pTagName.Value = sample.TagName;
                pTimestamp.Value = sample.TimestampUtc.UtcDateTime.ToString("o");
                pValue.Value = sample.Value;
                Console.WriteLine("executing sql");
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();

            lock (_bufferLock)
            {
                _buffer.RemoveRange(0, batch.Length);
            }

            Console.WriteLine($"Flushed {batch.Length} samples to DB.");
            Console.WriteLine($"Buffer: size: {_buffer.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to flush samples: {ex.Message}");
        }
        finally
        {
            _flushLock.Release();
        }
    }
}
