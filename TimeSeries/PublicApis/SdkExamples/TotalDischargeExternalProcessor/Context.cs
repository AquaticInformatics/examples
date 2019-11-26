using System;
using System.Collections.Generic;

namespace TotalDischargeExternalProcessor
{
    public class Context
    {
        public string Server { get; set; }
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin";
        public TimeSpan MinimumEventDuration { get; set; } = TimeSpan.FromHours(2);
        public List<Processor> Processors { get; } = new List<Processor>();
    }
}
