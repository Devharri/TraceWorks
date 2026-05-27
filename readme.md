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

Early prototype.

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

## License

TBD