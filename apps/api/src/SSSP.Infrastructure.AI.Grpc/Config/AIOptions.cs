using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.Infrastructure.AI.Grpc.Config
{
    public class AIOptions
    {
        public string RestUrl { get; set; } = string.Empty;
        public string GrpcUrl { get; set; } = string.Empty;
    }
}
