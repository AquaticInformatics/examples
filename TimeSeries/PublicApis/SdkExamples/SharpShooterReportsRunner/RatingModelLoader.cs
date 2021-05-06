using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using ServiceStack;
using Parameter = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.Parameter;

namespace SharpShooterReportsRunner
{
    public class RatingModelLoader
    {
        public Context Context { get; set; }
        public IAquariusClient Client { get; set; }

        private IServiceClient SiteVisit { get; set; }
        private IServiceClient Processor { get; set; }

        private RatingModelDescription RatingModelDescription { get; set; }
        private Parameter InputParameter { get; set; }
        private Parameter OutputParameter { get; set; }
        private string HydroMlXml { get; set; }
        private byte[] AopBytes { get; set; }
        // ReSharper disable once CollectionNeverUpdated.Local
        private List<FieldVisitReading> FieldVisitReadings { get; set; }
        public LocationDataServiceResponse LocationData { get; set; }

        public void Load(RatingModelDescription ratingModelDescription)
        {
            RatingModelDescription = ratingModelDescription;

            var allParameters = Client.Provisioning.Get(new GetParameters()).Results;

            InputParameter = allParameters.Single(p => p.Identifier == ratingModelDescription.InputParameter);
            OutputParameter = allParameters.Single(p => p.Identifier == ratingModelDescription.OutputParameter);

            CreateConnectedClient();

            var ratingModelInfo = GetRatingModelInfo();

            AopBytes = GetRatingModelAop(ratingModelInfo.RatingModelId);

            HydroMlXml = GetHydroMlXml();

            LoadFieldVisitReadings();
        }

        private void LoadFieldVisitReadings()
        {
            // TODO: Fetch this in a better way
            FieldVisitReadings = new List<FieldVisitReading>();
        }

        public class FieldVisitReading
        {
            public long DiscreteMeasurementId { get; set; }
            public string DisplayId { get; set; }
            public DateTime MeasurementTime { get; set; }
            public DateTime? MeasurementEndTime { get; set; }
            public DateTime? ResultStartTime { get; set; }
            public DateTime? ResultEndTime { get; set; }
            public string ParameterId { get; set; }
            public double Result { get; set; }
            public string UnitId { get; set; }
            public double UtcOffset { get; set; }
            public string Party { get; set; }
            public string Remarks { get; set; }
            public long LocationId { get; set; }
            public long VisitId { get; set; }
            public long? ResultApproval { get; set; }
            public string Method { get; set; }
            public string Condition { get; set; }
            public string ControlCode { get; set; }
            public string ControlConditionComments { get; set; }
            public long? Quality { get; set; }
            public double? Uncertainty { get; set; }
            public bool IsRedbootsVisit { get; set; }
            public string VisitUrl { get; set; }
        }

        public class RatingCurveResult
        {
            public List<ExpandedPoint> ExpandedPoints { get; set; } = new List<ExpandedPoint>();
            public List<EquationPoint> EquationPoints { get; set; } = new List<EquationPoint>();
            public List<RatingMeasurement> RatingMeasurements { get; set; } = new List<RatingMeasurement>();
        }

        public class EquationPoint
        {
            public double Stage { get; set; }
            public double Discharge { get; set; }
        }

        public class ExpandedPoint
        {
            public double Stage { get; set; }
            public double Discharge { get; set; }
            public double ShiftedStage { get; set; }
            public double ShiftedDischarge { get; set; }
        }

        public class RatingMeasurement
        {
            public double? Area { get; set; }
            public string Condition { get; set; }
            public double? DepVariableValue { get; set; }
            public string DepVariableDescription { get; set; }
            public double? IndepVariableValue { get; set; }
            public string IndepVariableDescription { get; set; }
            public double? EffectiveDepth { get; set; }
            public double? MeanVelocity { get; set; }
            public string MeasuredBy { get; set; }
            public DateTime MeasurementTimestamp { get; set; }
            public string Method { get; set; }
            public string Quality { get; set; }
            public double? StageChange { get; set; }
            public double? Verticals { get; set; }
            public double? Width { get; set; }
        }

        public RatingCurveResult LoadEffectiveRatingCurve(RatingModelDescription ratingModelDescription, double stepSize, DateTimeOffset curveEffectiveTime)
        {
            var result = new RatingCurveResult();

            var expandedRatingCurve = GetExpandedRatingCurve(ratingModelDescription, stepSize, curveEffectiveTime);

            if (expandedRatingCurve == null)
                return result;

            if (expandedRatingCurve.BaseRatingTable.Count != expandedRatingCurve.AdjustedRatingTable.Count)
                throw new ExpectedException($"Expanded rating curve for '{ratingModelDescription.Identifier}:{expandedRatingCurve.Id}' has a mis-matched point count. {nameof(expandedRatingCurve.BaseRatingTable)}.Count = {expandedRatingCurve.BaseRatingTable.Count} points but {nameof(expandedRatingCurve.AdjustedRatingTable)}.Count = {expandedRatingCurve.AdjustedRatingTable.Count} points.");

            for (var i = 0; i < expandedRatingCurve.BaseRatingTable.Count; ++i)
            {
                result.ExpandedPoints.Add(new ExpandedPoint
                {
                    Stage = expandedRatingCurve.BaseRatingTable[i].InputValue ?? double.NaN,
                    Discharge = expandedRatingCurve.BaseRatingTable[i].OutputValue ?? double.NaN,
                    ShiftedStage = expandedRatingCurve.AdjustedRatingTable[i].InputValue ?? double.NaN,
                    ShiftedDischarge = expandedRatingCurve.AdjustedRatingTable[i].OutputValue ?? double.NaN
                });
            }

            var curveStartTime = expandedRatingCurve.PeriodsOfApplicability.First().StartTime;
            var curveEndTime = expandedRatingCurve.PeriodsOfApplicability.Last().EndTime;

            var exceptions = GetRatingExceptions(expandedRatingCurve.Id);

            var fieldVisitReadings = FieldVisitReadings.Where(r =>
            {
                var time = new DateTimeOffset(DateTime.SpecifyKind(r.MeasurementTime, DateTimeKind.Unspecified), TimeSpan.FromHours(r.UtcOffset));

                return exceptions.IncludeIds.Contains(r.DiscreteMeasurementId)
                       || !exceptions.ExcludeIds.Contains(r.DiscreteMeasurementId) && curveStartTime <= time && time < curveEndTime;
            }).ToList();

            var measurementIds = fieldVisitReadings
                .Select(f => f.DiscreteMeasurementId)
                .Distinct();

            result.RatingMeasurements = measurementIds
                .Select(id => CreateRatingMeasurement(fieldVisitReadings.Where(f => f.DiscreteMeasurementId == id).ToList()))
                .Where(r => r != null)
                .ToList();

            return result;
        }

        private ExpandedRatingCurve GetExpandedRatingCurve(RatingModelDescription ratingModelDescription, double stepSize, DateTimeOffset curveEffectiveTime)
        {
            try
            {
                return Client.Publish.Get(new EffectiveRatingCurveServiceRequest
                {
                    RatingModelIdentifier = ratingModelDescription.Identifier,
                    EffectiveTime = curveEffectiveTime,
                    StepSize = stepSize
                }).ExpandedRatingCurve;
            }
            catch (WebServiceException exception)
            {
                if (exception.StatusCode == (int) HttpStatusCode.BadRequest && exception.ErrorMessage.Contains(" has no active Rating Curves"))
                    return null;

                throw;
            }
        }

        private (List<long> ExcludeIds, List<long> IncludeIds) GetRatingExceptions(string curveName)
        {
            var xml = new XmlDocument();
            xml.LoadXml(HydroMlXml);

            var ns = new XmlNamespaceManager(xml.NameTable);
            ns.AddNamespace("nwis", "http://water.usgs.gov/XML/NWIS/4.7");

            var rating = xml.SelectSingleNode($"//nwis:Rating[nwis:RatingNumber = '{curveName}']", ns);

            return (ExtractIds(rating, "nwis:MeasurementExceptionOff", ns), ExtractIds(rating, "nwis:MeasurementExceptionOn", ns));
        }

        private static List<long> ExtractIds(XmlNode rating, string xpath, XmlNamespaceManager namespaceManager)
        {
            var nodes = rating?.SelectNodes(xpath, namespaceManager);

            if (nodes != null)
            {
                return nodes
                    .Cast<XmlNode>()
                    .Select(node => long.TryParse(node.InnerText, out var value) ? value : 0)
                    .ToList();
            }

            return new List<long>();
        }

        private RatingMeasurement CreateRatingMeasurement(List<FieldVisitReading> readings)
        {
            var inputReadings = GetParameterReadings(readings, InputParameter.ParameterId);
            var outputReadings = GetParameterReadings(readings, OutputParameter.ParameterId);

            if (!inputReadings.Any() || !outputReadings.Any())
                return null;

            var outputReading = outputReadings.First();

            var areaReadings = GetParameterReadings(readings, "RiverSectionArea");
            var widthReadings = GetParameterReadings(readings, "RiverSectionWidth");
            var velocityReadings = GetParameterReadings(readings, "WV");
            var depthReadings = GetParameterReadings(readings, "Depth");

            var rm = new RatingMeasurement
            {
                Area = GetAverageReading(areaReadings),
                Condition = outputReading.Condition,
                DepVariableValue = GetAverageReading(outputReadings),
                DepVariableDescription = RatingModelDescription.OutputParameter,
                IndepVariableValue = GetAverageReading(inputReadings),
                IndepVariableDescription = RatingModelDescription.InputParameter,
                EffectiveDepth = GetAverageReading(depthReadings),
                MeanVelocity = GetAverageReading(velocityReadings),
                MeasuredBy = outputReading.Party,
                MeasurementTimestamp = outputReading.MeasurementTime,
                Method = outputReading.Method,
                Quality = outputReading.Quality?.ToString(),
                Width = GetAverageReading(widthReadings),
            };

            return rm;
        }

        private static List<FieldVisitReading> GetParameterReadings(List<FieldVisitReading> readings, string parameterId)
        {
            return readings
                .Where(r => r.ParameterId == parameterId)
                .ToList();
        }

        private static double? GetAverageReading(List<FieldVisitReading> readings)
        {
            if (!readings.Any())
                return null;

            return readings.Average(r => r.Result);
        }

        public class AllTablesResult
        {
            public List<Table> Tables { get; set; } = new List<Table>();
            public List<TableDate> TableDates { get; set; } = new List<TableDate>();
            public List<TableValue> TableValues { get; set; } = new List<TableValue>();
        }

        public class Table
        {
            public double TableNumber { get; set; }
            public string TableName { get; set; }
            public double NumPeriods { get; set; }
            public double NumPoints { get; set; }
        }

        public class TableDate
        {
            public double TableNumber { get; set; }
            public DateTimeOffset? StartDate { get; set; }
            public DateTimeOffset? EndDate { get; set; }
        }

        public class TableValue
        {
            public double TableNumber { get; set; }
            public double Input { get; set; }
            public double Output { get; set; }
        }

        public AllTablesResult LoadAllTables(RatingModelDescription ratingModelDescription, RatingCurveListServiceResponse ratingCurves, double stepSize)
        {
            var result = new AllTablesResult();

            var validTableNumbers = new HashSet<double>(
                ratingCurves
                .RatingCurves
                .Select(c => double.TryParse(c.Id, out var number) ? (double?) number : null)
                .Where(d => d.HasValue)
                .Select(d => d.Value));

            for(var i = 0; i < ratingCurves.RatingCurves.Count; ++i)
            {
                var ratingCurve = ratingCurves.RatingCurves[i];

                if (!double.TryParse(ratingCurve.Id, out var tableNumber))
                {
                    tableNumber = 1.0 + i;

                    while (validTableNumbers.Contains(tableNumber))
                        tableNumber += ratingCurves.RatingCurves.Count;
                }

                result.Tables.Add(new Table
                {
                    TableNumber = tableNumber,
                    TableName = ratingCurve.Id,
                    NumPeriods = ratingCurve.PeriodsOfApplicability.Count,
                    NumPoints = ratingCurve.BaseRatingTable.Count
                });

                result.TableDates.AddRange(ratingCurve.PeriodsOfApplicability.Select(p => new TableDate
                {
                    TableNumber = tableNumber,
                    StartDate = p.StartTime,
                    EndDate = p.EndTime
                }));

                result.TableValues.AddRange(ratingCurve.BaseRatingTable.Select(p => new TableValue
                {
                    TableNumber = tableNumber,
                    Input = p.InputValue ?? double.NaN,
                    Output = p.OutputValue ?? double.NaN
                }));
            }

            return result;
        }

        private void CreateConnectedClient()
        {
            SiteVisit = Client.RegisterCustomClient(PrivateApis.SiteVisit.Root.Endpoint);
            Processor = Client.RegisterCustomClient(PrivateApis.Processor.Root.Endpoint);
        }

        private PrivateApis.Processor.RatingModelInfo GetRatingModelInfo()
        {
            var location = GetSiteVisitLocation(RatingModelDescription.LocationIdentifier);

            var locationRatingModels = Processor
                .Get(new PrivateApis.Processor.GetRatingModelsForLocationRequest { LocationId = location.Id });

            var ratingModelInfo = locationRatingModels
                .SingleOrDefault(r => r.Identifier == RatingModelDescription.Identifier);

            if (ratingModelInfo == null)
                throw new ExpectedException($"Can't find rating model '{RatingModelDescription.Identifier}'");

            return ratingModelInfo;
        }

        private PrivateApis.SiteVisit.SearchLocation GetSiteVisitLocation(string locationIdentifier)
        {
            var searchResults = SiteVisit
                .Get(new PrivateApis.SiteVisit.GetSearchLocations { SearchText = locationIdentifier });

            if (searchResults.LimitExceeded)
                throw new ExpectedException($"Cannot resolve location ID for identifier='{locationIdentifier}'. LimitExceeded=true. Results.Count={searchResults.Results.Count}");

            var location = searchResults.Results
                .SingleOrDefault(l => l.Identifier == locationIdentifier);

            if (location == null)
                throw new ExpectedException($"Cannot resolve locationID for unknown identifier='{locationIdentifier}', even with Results.Count={searchResults.Results.Count}");

            return location;
        }

        private byte[] GetRatingModelAop(long ratingModelId)
        {
            return Processor.Get(new PrivateApis.Processor.GetRatingModelData
            {
                RatingModelId = ratingModelId
            }).OptimizedPackage;
        }

        private string GetHydroMlXml()
        {
            using (var memoryStream = new MemoryStream(AopBytes))
            using (var zipArchive = new ZipArchive(memoryStream))
            {
                foreach (var entry in zipArchive.Entries.Where(e => !e.FullName.Equals("aquarius.xml", StringComparison.InvariantCultureIgnoreCase)))
                {
                    using (var reader = new BinaryReader(entry.Open()))
                    {
                        var bytes = reader.ReadBytes((int)entry.Length);

                        try
                        {
                            var text = Encoding.UTF8.GetString(bytes).Trim(char.MinValue);

                            // The HydroML is the first in the archive.
                            return text;
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                }
            }

            return null;
        }
    }
}
