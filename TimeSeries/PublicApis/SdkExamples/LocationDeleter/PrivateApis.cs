using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ServiceStack;

// ReSharper disable once CheckNamespace
namespace LocationDeleter.PrivateApis
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

        // Stolen from 3.10 SiteVisit endpoint 
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

        // Stolen from 17.3 SiteVisit
        [Route("/locations/{Id}/visits", HttpMethods.Get)]
        public class GetLocationVisits : IReturn<List<Visit>>
        {
            public long Id { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
        }

        public class Visit
        {
            public long Id { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public bool IsLocked { get; set; }
        }

        [Route("/visits/{Id}", HttpMethods.Delete)]
        public class DeleteVisit : IReturnVoid
        {
            public long Id { get; set; }
        }

        // Approval DTOs lifted from 17.3 SiteVisit and trimmed down to minimum required properties
        [Route("/locations/{Id}/approvallevels", HttpMethods.Get)]
        public class GetLocationApprovalLevels : IReturn<ResolvedLocationRole>
        {
            public long Id { get; set; }
        }

        public class ResolvedLocationRole
        {
            public string Name { get; set; }
            public bool IsLocationVisible { get; set; }
            public bool CanEditLocationDetails { get; set; }
            public bool CanAssignUserRoles { get; set; }
            public bool CanReadData { get; set; }
            public bool CanAddData { get; set; }
            public bool CanEditData { get; set; }
            public bool CanAddOrRemoveLocations { get; set; }
            public bool CanRemoveFieldVisits { get; set; }

            public List<ApprovalLevel> ApprovalLevels { get; set; }
            public List<ApprovalTransition> AllowedTransitions { get; set; }
        }

        public class ApprovalLevel
        {
            public long Id { get; set; }

            public long Level { get; set; }

            public string Name { get; set; }

            public string HexColor { get; set; }
        }

        public class ApprovalTransition
        {
            public long FromApprovalLevelId { get; set; }
            public long ToApprovalLevelId { get; set; }
        }

        [Route("/datasets/{Id}/approval", HttpMethods.Post)]
        public class PostDatasetApproval : IReturn<DatasetApprovalSaveResult>
        {
            public long Id { get; set; }
            public bool IsMigrationRequest { get; set; }
            public long ApprovalLevelId { get; set; }
        }

        [Route("/approvaljobs/{Id}", HttpMethods.Get)]
        public class GetApprovalJob : IReturn<DatasetApprovalSaveResult>
        {
            public Int64 Id { get; set; }
        }

        public class DatasetApprovalSaveResult
        {
            public Int64 Id { get; set; }
            public bool Complete { get; set; }
            public bool Success { get; set; }
            public List<RelatedDataset> RelatedDatasets { get; set; }
        }

        public class RelatedDataset
        {
            public int Order { get; set; }
            public Dataset Dataset { get; set; }
            public bool IsMinimumRequired { get; set; }
            public bool IsApprovalChangeRequired { get; set; }
            public List<string> ApprovalRejectionReasons { get; set; }
        }

        public class Dataset
        {
            public long Id { get; set; }
            public long LocationId { get; set; }
            public string Identifier { get; set; }
            public string FullIdentifier { get; set; }
            public string LocationIdentifier { get; set; }
            public string LocationName { get; set; }
            public string SubLocationIdentifier { get; set; }
            public string Label { get; set; }
            public string Description { get; set; }
            public string ParameterId { get; set; }
            public string ParameterName { get; set; }
            public string UnitId { get; set; }
            public string UnitDisplayName { get; set; }
            public DateTime LastModified { get; set; }
            public bool Publish { get; set; }
            public long UtcOffsetMinutes { get; set; }
            public bool IsActive { get; set; }
            public string ComputationDisplayName { get; set; }
            public string ComputationPeriodDisplayName { get; set; }
        }
    }

    namespace Processor
    {
        public static class Root
        {
            public const string Endpoint = Aquarius.TimeSeries.Client.EndPoints.Root.EndPoint + "/Processor";
        }

        // Stolen from /Processor 2017.3 endpoint
        [Route("/migration/location/identifier/{LocationIdentifier}", HttpMethods.Delete)]
        public class DeleteMigrationLocationByIdentifier : IReturnVoid
        {
            public string LocationIdentifier { get; set; }
        }

        [Route("/location/{LocationId}/timeseries", HttpMethods.Get)]
        public class GetTimeSeriesForLocationRequest : IReturn<List<TimeSeriesInfo>>
        {
            public long LocationId { get; set; }
        }

        public class TimeSeriesInfo
        {
            public long TimeSeriesId { get; set; }
            public Guid UniqueId { get; set; }
        }

        [Route("/timeseries/{TimeSeriesId}", HttpMethods.Delete)]
        public class DeleteTimeSeriesRequest : IReturnVoid
        {
            public long TimeSeriesId { get; set; }
        }

        [Route("/location/{LocationId}/ratingmodels", HttpMethods.Get)]
        public class GetRatingModelsForLocationRequest : IReturn<List<RatingModelInfo>>
        {
            public long LocationId { get; set; }
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

        public class PortInfo
        {
            public string Units { get; set; }
            public string UnitsSymbol { get; set; }
            public string ParameterType { get; set; }
            public string ParameterDisplayId { get; set; }
        }

        [Route("/ratingmodel/{RatingModelId}", HttpMethods.Delete)]
        public class DeleteRatingModelRequest : IReturnVoid
        {
            public long RatingModelId { get; set; }
        }
    }
}
