using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.DAL.Enums
{
    [Flags]
    public enum CameraAICapabilities
    {
        None = 0,
        Face = 1 << 0,
        Object = 1 << 1,
        Behavior = 1 << 2,

        All = Face | Object | Behavior
    }
}
