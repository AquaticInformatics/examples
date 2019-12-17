using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aquarius.Samples.Client.ServiceModel;
using ServiceStack;

namespace SamplesTripScheduler.PrivateApis
{
    [Route("/v1/fieldvisits/{VisitId}/activityfromplannedactivity", "POST")]
    public class PostSpecimensFromPlannedActivity : IReturn<Activity>
    {
        public string VisitId { get; set; }
        public string Id { get; set; }
        public ActivityTemplate ActivityTemplate { get; set; }
        public string Instruction { get; set; }
        public PlannedActivityActivityType? ActivityType { get; set; }
        public MediumType? Medium { get; set; }
        public CollectionMethod CollectionMethod { get; set; }
        public string HashForFieldsThatRequireUniqueness { get; set; }
        public AuditAttributes AuditAttributes { get; set; }
    }
}
