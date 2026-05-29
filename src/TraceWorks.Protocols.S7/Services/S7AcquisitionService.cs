using S7.Net;
using TraceWorks.Shared.Models;

namespace TraceWorks.Protocols.S7.Services;

public class S7AcquisitionService
{
    private readonly Plc _plc;

    private readonly List<TagDefinition> _tags;

    public S7AcquisitionService()
    {
        _plc = new Plc(
            CpuType.S71500,
            "192.168.1.2",
            0,
            1);

        _tags = new List<TagDefinition>
        {


            new()
            {
                Id = 1,
                Name = "bool",
                Address = "DB100.DBX0.0",
                DataType = TagDataType.Bool
            },
            new()
            {
                Id = 2,
                Name = "real",
                Address = "DB100.DBD2",
                DataType = TagDataType.Float
            },
            new()
            {
                Id = 3,
                Name = "int",
                Address = "DB100.DBW6",
                DataType = TagDataType.Int
            }
        };
    }

    public async Task StartAsync()
    {
        Console.WriteLine("Opening PLC connection...");
        
        _plc.Open();

        Console.WriteLine("PLC connected.");

        while (true)
        {
            foreach (var tag in _tags)
            {
                try
                {
                    var raw = _plc.Read(tag.Address);
                    
                    if (raw is null)
                    {
                        Console.WriteLine($"No value read for {tag.Name}");
                        continue;
                    }
                    Console.WriteLine(raw);
                    double value = ConvertToDouble(raw);
                    Console.WriteLine(value);
                    var sample = new SampleModel
                    {
                        TagId = tag.Id,
                        TimestampUtc = DateTime.UtcNow,
                        Value = value
                    };
                    
                    Console.WriteLine(
                        $"{sample.TimestampUtc:HH:mm:ss.fff} | " +
                        $"{sample.TagId} = {sample.Value}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading {tag.Name}: {ex}");
                    Console.WriteLine($"Error reading {tag.Name}: {ex.Message}");
                }
            }
            Console.WriteLine($"Waiting for next acquisition cycle...");
            await Task.Delay(5000);
        }
    }

    private static double ConvertToDouble(object raw)
    {
     return raw switch
    {
        bool b => b ? 1.0 : 0.0,
        byte b => b,
        ushort us => us,
        short s => s,
        int i => i,
        uint ui => ui,
        float f => f,
        double d => d,
        _ => 0.0
    };
    }
}