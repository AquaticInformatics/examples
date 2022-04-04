using System;
using System.Collections.Generic;
using ServiceStack;

namespace ObservationReportExporter.ExtraApis.TimeSeries
{
    public enum HackTagValueType
    {
        Unknown,
        None,
        PickList,
        Text, // This changed from "Text" (before 2021.3) to "String" in 2021.3, breaking a bunch of serializations. Yuck.
        Number,
        Boolean,
        DateTime,
    }

    [Route("/tags", HttpMethods.Get)]
    public class GetHackTags
        : IReturn<HackTagsResponse>
    {
    }

    public class HackTagsResponse
    {
        public HackTagsResponse()
        {
            Results = new List<HackTag>();
        }

        public List<HackTag> Results { get; set; }
    }

    public class HackTag
    {
        public HackTag()
        {
            PickListValues = new List<string>();
        }

        public Guid UniqueId { get; set; }
        public string Key { get; set; }
        public HackTagValueType? ValueType { get; set; } // This is why all the hacked Tag DTOs are required
        public List<string> PickListValues { get; set; }
        public bool AppliesToAttachments { get; set; }
        public bool AppliesToLocations { get; set; }
        public bool AppliesToLocationNotes { get; set; }
        public bool AppliesToReports { get; set; }
        public bool AppliesToSensorsGauges { get; set; }
    }
}
