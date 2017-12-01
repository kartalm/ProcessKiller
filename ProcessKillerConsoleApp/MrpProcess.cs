using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessKillerConsoleApp
{
    public class MrpProcess
    {
        public System.Diagnostics.Process Process { get; set; }

        public TimeSpan TotalProcessTime { get; set; }

        public DateTime IdleSince { get; set; }

        public bool Flag { get; set; }

    }
}
