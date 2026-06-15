using S7.Net;

namespace TraceWorks.Shared.Models;

public sealed class PlcConnectionParameters
{
    public CpuType CpuType { get; set; }
    public string IpAddress { get; set; } = "";
    public short Rack { get; set; }
    public short Slot { get; set; }
}