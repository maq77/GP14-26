using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.Domain.Enums;

public enum IncidentSource
{
    Manual = 1,
    AIDetection = 2,
    Sensor = 3,
    CitizenReport = 4
}