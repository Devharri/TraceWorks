using TraceWorks.Shared.Models;
using S7.Net;

namespace TraceWorks.Shared.Services;

public sealed class PlcConfigurationService
{
    private PlcConnectionParameters _parameters;

    public event Action? ConnectionSettingsChanged;
    public PlcConfigurationService()
    {
        //Initialize with default parameters
        _parameters = new PlcConnectionParameters
        {
            CpuType = CpuType.S71500,
            IpAddress = "192.168.1.2",
            Rack = 0,
            Slot = 1
        };
    }

    public void UpdateParameters(PlcConnectionParameters parameters)
    {
        _parameters = parameters;
        ConnectionSettingsChanged?.Invoke();
    }

    public PlcConnectionParameters GetParameters()
    {
        return _parameters;
    } 
}