using TraceWorks.Shared.Models;
using S7.Net;

namespace TraceWorks.Shared.Services;

public sealed class PlcConfigurationService
{
    private PlcConnectionModel _parameters;

    public event Action? ConnectionSettingsChanged;
    public PlcConfigurationService()
    {
        //Initialize with default parameters
        _parameters = new PlcConnectionModel
        {
            CpuType = CpuType.S71500,
            IpAddress = "192.168.1.2",
            Rack = 0,
            Slot = 1
        };
    }

    public void UpdateParameters(PlcConnectionModel parameters)
    {
        _parameters = parameters;
        ConnectionSettingsChanged?.Invoke();
    }

    public PlcConnectionModel GetParameters()
    {
        return _parameters;
    } 
}