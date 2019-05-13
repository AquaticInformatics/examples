using System;
using System.Collections.Generic;

namespace ChangeVisitApprovals
{
    public class Context
    {
        public string Server { get; set; }
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin";
        public int? ApprovalLevel { get; set; }
        public string ApprovalName { get; set; }
        public bool SkipConfirmation { get; set; }
        public bool DryRun { get; set; }
        public List<string> Locations { get; set; } = new List<string>();
        public DateTimeOffset? VisitsBefore { get; set; }
        public DateTimeOffset? VisitsAfter { get; set; }
    }
}
