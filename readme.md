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

[ Siemens PLC ]
        ↓
[ Acquisition Service ]
        ↓
[ In-memory Queue / Channel ]
        ↓
[ TimescaleDB ]
        ↓
[ ASP.NET Core Local Web UI ]
        ↓
[ Browser Charts ]

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