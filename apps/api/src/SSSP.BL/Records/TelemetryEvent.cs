using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.BL.Records
{
    public record TelemetryEvent(string Name, Dictionary<string, string> Properties, Dictionary<string, double> Metrics);

}
