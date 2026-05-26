# TraceWorks

Industrial historian and trend visualization tool.

## Features

- Siemens S7 PLC communication
- OPC UA support
- Historical data logging
- Trend visualization
- Cross-platform support
- Local web UI
- TimescaleDB storage
- Docker support

## Architecture

┌─────────────────────┐
│ Siemens PLC / OPCUA │
└──────────┬──────────┘
           │
           ▼
┌────────────────────────────┐
│ Acquisition Service        │
│                            │
│ TraceWorks.Protocols.S7    │
│ TraceWorks.Protocols.OpcUa │
│                            │
│ - PLC communication        │
│ - Polling                  │
│ - Decoding                 │
│ - Reconnect handling       │
└──────────┬─────────────────┘
           │
           ▼
┌────────────────────────────┐
│ In-Memory Channel Queue    │
│                            │
│ - High-speed buffering     │
│ - Sample transport         │
│ - Async processing         │
└──────────┬─────────────────┘
           │
           ▼
┌────────────────────────────┐
│ TraceWorks.Storage         │
│                            │
│ - Reads Channel<T>         │
│ - Batch buffering          │
│ - Batched SQL inserts      │
│ - Storage abstraction      │
└──────────┬─────────────────┘
           │
           ▼
┌────────────────────────────┐
│ TimescaleDB / PostgreSQL   │
│                            │
│ - Historical storage       │
│ - Compression              │
│ - Retention policies       │
└──────────┬─────────────────┘
           │
           ▼
┌────────────────────────────┐
│ TraceWorks.Server          │
│                            │
│ - ASP.NET Core UI          │
│ - APIs                     │
│ - Configuration            │
│ - Playback                 │
└──────────┬─────────────────┘
           │
           ▼
┌────────────────────────────┐
│ Browser UI                 │
│                            │
│ - Trends                   │
│ - Visualization            │
│ - Timeline playback        │
└────────────────────────────┘

## Development Status

Early prototype.

## Roadmap

- [ ] Read PLC variables
- [ ] Store data to database
- [ ] Add buffering
- [ ] Create trend UI
- [ ] Docker integration
- [ ] OPC UA support

## Technologies

- ASP.NET Core
- C#
- TimescaleDB
- PostgreSQL
- Docker

## License

TBD