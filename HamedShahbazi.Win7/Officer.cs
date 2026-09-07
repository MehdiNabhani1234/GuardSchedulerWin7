using System;

namespace HamedShahbazi.Win7
{
    [Serializable]
    public class Officer
    {
        public string Name { get; set; }

        public double Score { get; set; }

        public bool IsReplacement { get; set; }

        public Officer()
        {
            Name = "";
            Score = 1;
            IsReplacement = false;
        }
    }
}