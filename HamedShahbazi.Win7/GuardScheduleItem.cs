using System;

namespace HamedShahbazi.Win7
{
    [Serializable]
    public class GuardScheduleItem
    {
        public int Day { get; set; }

        public int Pass { get; set; }

        public Officer Guard { get; set; }

        public Officer Reserve { get; set; }

        public Officer Chief { get; set; }

        public GuardScheduleItem()
        {
            Guard = null;
            Reserve = null;
            Chief = null;
        }
    }
}