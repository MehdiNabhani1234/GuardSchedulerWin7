using System;
using System.Collections.Generic;

namespace HamedShahbazi.Win7
{
    [Serializable]
    public class CurrentProgramStorage
    {
        public int ProgramDays { get; set; }

        public DateTime StartDate { get; set; }

        public List<GuardScheduleItem> Schedule { get; set; }

        public List<string> UsedReplacementGuards { get; set; }

        public List<string> UsedReplacementReserves { get; set; }

        public List<string> UsedReplacementChiefs { get; set; }

        public CurrentProgramStorage()
        {
            ProgramDays = 31;
            StartDate = DateTime.Now.Date;

            Schedule =
                new List<GuardScheduleItem>();

            UsedReplacementGuards =
                new List<string>();

            UsedReplacementReserves =
                new List<string>();

            UsedReplacementChiefs =
                new List<string>();
        }
    }
}