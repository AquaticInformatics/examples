using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ServiceStack;

// ReSharper disable once CheckNamespace
namespace PrivateApis
{
    // These private API endpoints and operations are not supported for external integrations, and are subject to change without notice.
    // They have been hand-curated to work across multiple versions of AQTS software.
    // May contain traces of peanuts.
    namespace SiteVisit
    {
        public static class Root
        {
            public const string Endpoint = Aquarius.TimeSeries.Client.EndPoints.Root.EndPoint + "/apps/v1";
        }

        // Lifted from 3.10 SiteVisit endpoint 
        [DataContract]
        [Route("/locations/search", HttpMethods.Get)]
        public class GetSearchLocations : IReturn<SearchLocationsResponse>
        {
            [DataMember(Name = "q")]
            public string SearchText { get; set; }

            [DataMember(Name = "n")]
            public int MaxResults { get; set; }

            [DataMember(Name = "h")]
            public bool HideTruncatedResults { get; set; }
        }

        public class SearchLocationsResponse
        {
            public List<SearchLocation> Results { get; set; }
            public bool LimitExceeded { get; set; }
        }

        public class SearchLocation
        {
            public long Id { get; set; }
            public string Identifier { get; set; }
            public string Name { get; set; }
        }
    }

    namespace Processor
    {
        public static class Root
        {
            public const string Endpoint = Aquarius.TimeSeries.Client.EndPoints.Root.EndPoint + "/Processor";
        }

        // Lifted from 2017.4
        [Route("/location/{LocationId}/ratingmodels", HttpMethods.Get)]
        public class GetRatingModelsForLocationRequest
            : IReturn<List<RatingModelInfo>>
        {
            public long LocationId { get; set; }
        }

        public class PortInfo
        {
            public string Units { get; set; }
            public string UnitsSymbol { get; set; }
            public string ParameterType { get; set; }
            public string ParameterDisplayId { get; set; }
        }

        public class RatingModelInfo
        {
            public long RatingModelId { get; set; }
            public long LocationId { get; set; }
            public string LocationIdentifier { get; set; }
            public DateTime LastModified { get; set; }
            public string LastModifiedBy { get; set; }
            public string Identifier { get; set; }
            public string Label { get; set; }
            public string Description { get; set; }
            public string Comment { get; set; }
            public PortInfo InputInfo { get; set; }
            public PortInfo OutputInfo { get; set; }
            public TimeSpan UtcOffset { get; set; }
            public string DefaultInputIds { get; set; }
            public string Status { get; set; }
            public long? TemplateId { get; set; }
            public bool Publish { get; set; }
        }
    }
}
