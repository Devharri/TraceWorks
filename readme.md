# TraceWorks
Industrial historian and trend visualization tool built with ASP.NET Core and TimescaleDB.

## Overview
Tool to solve the urgent need for trace and trending tool. From servo drive parameter tuning to long term trace. 

## Features
- Siemens S7 PLC communication
- OPC UA support
- Historical data logging
- Trend visualization
- Cross-platform support
- Local web UI
- TimescaleDB storage

### Prerequisites
- .NET 10+ 
- Docker & Docker Compose
- TimescaleDB instance

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
6. TimescaleDB
7. ASP.NET Core
8. browser charts

## Development Status
Early prototype - not production-ready

## Roadmap
- [ ] Read PLC variables
- [ ] Store data to database
- [ ] Add buffering
- [ ] Create trend UI
- [ ] OPC UA support

## Technologies
- ASP.NET Core
- C#
- TimescaleDB
- Docker
- S7NetPlus (Siemens communication)
- OPC UA .NET Standard

## License
MIT License - see [MIT License](LICENSE) file for details

## Authors
- Harri Honkanen