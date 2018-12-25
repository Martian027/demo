using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Schedule.Classes
{
    public class ScheduleRow : BaseRecord
    {
        public int MovieTheater { get; set; }
        public string MovieTheaterName { get; set; }
        public int Movie { get; set; }
        public string MovieTitle { get; set; }
        public DateTime Date { get; set; }
        public List<ScheduleTime> StartTimeList { get; set; }
    }
}
