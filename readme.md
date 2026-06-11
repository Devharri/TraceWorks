# TraceWorks
Industrial historian and trend visualization tool built with ASP.NET Core and SQlite.

## Overview
Tool to solve the urgent need for trace and trending tool. From servo drive parameter tuning to long term trace. 

## Features
- Siemens S7 PLC communication
- OPC UA support
- Historical data logging
- Trend visualization
- Cross-platform support
- Local web UI
- SQlite storage

### Prerequisites
- .NET 10+ 

### Installation
```bash
git clone https://github.com/Devharri/TraceWorks.git
cd TraceWorks
dotnet build
dotnet run --project src/TraceWorks.Server
```

## Architecture
1. Siemens PLC
2. Acquisition service
3. In-memory queue
4. Storage service
5. Batch buffering
6. SQlite database
7. ASP.NET Core
8. browser charts

## Development Status
Early prototype - not production-ready

## Roadmap
- [x] Read PLC variables
- [x] Store to in-memory queue (10,000 samples)
- [x] Add Batch buffer (1,000 samples)
- [x] Store data to database in batches
- [ ] Benchmark and monitor: in-memory queue, batch buffer, plc read, sqlite inserts
- [ ] Create trend UI
- [ ] OPC UA support

## Technologies
- ASP.NET Core
- C#
- SQlite
- Docker
- S7NetPlus (Siemens communication)
- OPC UA .NET Standard

## License
MIT License - see [MIT License](LICENSE) file for details

## Authors
- Harri Honkanen