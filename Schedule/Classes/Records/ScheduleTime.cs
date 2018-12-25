using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Schedule.Classes
{
    public class ScheduleTime : BaseRecord
    {
        public int ScheduleRow { get; set; }
        public TimeSpan Time { get; set; }
    }
}
