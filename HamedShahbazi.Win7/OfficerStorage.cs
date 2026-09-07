using System;
using System.Collections.Generic;

namespace HamedShahbazi.Win7
{
    [Serializable]
    public class SavedOfficer
    {
        public string Name { get; set; }

        public double Score { get; set; }

        public bool IsReplacement { get; set; }

        public SavedOfficer()
        {
            Name = "";
            Score = 0;
            IsReplacement = false;
        }
    }

    [Serializable]
    public class OfficerStorage
    {
        public List<SavedOfficer> GuardOfficers { get; set; }

        public List<SavedOfficer> ReserveOfficers { get; set; }

        public List<SavedOfficer> ChiefOfficers { get; set; }

        public OfficerStorage()
        {
            GuardOfficers = new List<SavedOfficer>();
            ReserveOfficers = new List<SavedOfficer>();
            ChiefOfficers = new List<SavedOfficer>();
        }
    }
}