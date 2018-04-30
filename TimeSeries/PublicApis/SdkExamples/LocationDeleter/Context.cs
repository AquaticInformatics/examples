using System;
using System.Collections.Generic;

namespace LocationDeleter
{
    public class Context
    {
        public string Server { get; set; }
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin";
        public bool SkipConfirmation { get; set; }
        public bool DryRun { get; set; }
        public bool RecreateLocations { get; set; }
        public List<string> LocationsToDelete { get; set; } = new List<string>();
        public List<string> TimeSeriesToDelete { get; set; } = new List<string>();
        public DateTimeOffset? VisitsBefore { get; set; }
        public DateTimeOffset? VisitsAfter { get; set; }
    }
}
