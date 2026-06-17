using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using TraceWorks.Shared.Models;
using TraceWorks.Shared.Services;

namespace TraceWorks.Storage.Services;

public sealed class StorageService : BackgroundService
{
    private readonly Channel<SampleModel> _channel;
    private readonly List<SampleModel> _buffer = new();
    private readonly object _bufferLock = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private const int BatchSize = 1000;
    private const int EstimatedBytesPerSample = 32;
    private SqliteConnection? _connection;
    private static readonly string ConnectionString = $"Data Source={Path.Combine("/Users/harrihonkanen/DATA/TraceWorks/data", "sample.db")}";
    private readonly MetricsService _metrics;

    public StorageService(Channel<SampleModel> channel, MetricsService metrics)
    {
        _channel = channel;
        _metrics = metrics;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = new SqliteConnection(ConnectionString);
        await _connection.OpenAsync(cancellationToken);

        // Ensure the Samples table exists and set journal mode to WAL for better concurrent performance
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"PRAGMA journal_mode = WAL;
CREATE TABLE IF NOT EXISTS Samples (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TagId INTEGER,
    TimestampUtc INTEGER NOT NULL,
    Value REAL NOT NULL
);";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await foreach (var sample in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            _metrics.IncrementConsumed(1);
            var shouldFlush = false;

            lock (_bufferLock)
            {
                _buffer.Add(sample);
                _metrics.SetBufferSize(_buffer.Count);
                shouldFlush = _buffer.Count >= BatchSize;
            }

            if (shouldFlush)
            {
                await FlushAsync(cancellationToken);
            }
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

            var sw = Stopwatch.StartNew();

            // Use a transaction and prepared statement for efficient batch insertion
            using var transaction = _connection.BeginTransaction();
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO Samples (TagId, TimestampUtc, Value) VALUES (@tagId, @timestamp, @value);";

            var pTagId = cmd.CreateParameter(); pTagId.ParameterName = "@tagId"; cmd.Parameters.Add(pTagId);
            var pTimestamp = cmd.CreateParameter(); pTimestamp.ParameterName = "@timestamp"; cmd.Parameters.Add(pTimestamp);
            var pValue = cmd.CreateParameter(); pValue.ParameterName = "@value"; cmd.Parameters.Add(pValue);

            foreach (var sample in batch)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                pTagId.Value = sample.TagId;
                pTimestamp.Value = sample.TimestampUtc.ToUnixTimeMilliseconds();
                pValue.Value = sample.Value;
                // Execute the insert command for each sample in the batch
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Commit the transaction to persist all changes at once
            transaction.Commit();

            sw.Stop();
            _metrics.RecordDbWrite(sw.Elapsed);
            _metrics.IncrementWrittenToDb(batch.Length);
            _metrics.RecordDbBytes(batch.Length * EstimatedBytesPerSample);

            lock (_bufferLock)
            {
                _buffer.RemoveRange(0, batch.Length);
                _metrics.SetBufferSize(_buffer.Count);
            }
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