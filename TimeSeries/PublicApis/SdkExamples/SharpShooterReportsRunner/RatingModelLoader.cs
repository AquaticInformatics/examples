using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using AQModelComputationDotNETLib;
using CommunicationShared.Dto;
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
        private LegacyDataServiceClient SoapClient { get; set; }

        private RatingModelDescription RatingModelDescription { get; set; }
        private Parameter InputParameter { get; set; }
        private Parameter OutputParameter { get; set; }
        private string HydroMlXml { get; set; }
        private byte[] AopBytes { get; set; }
        private CHydroModel HydroModel { get; set; }
        private List<FieldVisitReading> FieldVisitReadings { get; set; }
        private string CsvMetaHeader { get; set; }
        private string CsvMetadata { get; set; }

        public void Load(RatingModelDescription ratingModelDescription)
        {
            RatingModelDescription = ratingModelDescription;

            var allParameters = Client.Provisioning.Get(new GetParameters()).Results;

            InputParameter = allParameters.Single(p => p.Identifier == ratingModelDescription.InputParameter);
            OutputParameter = allParameters.Single(p => p.Identifier == ratingModelDescription.OutputParameter);

            using (SoapClient = CreateConnectedClient())
            {
                var ratingModelInfo = GetRatingModelInfo();

                AopBytes = GetRatingModelAop(ratingModelInfo.RatingModelId);

                HydroMlXml = GetHydroMlXml();

                HydroModel = new CHydroModel(AopBytes);

                CsvMetaHeader = "LocationId,InParameter,OutParameter";
                CsvMetadata = $"UNKNOWN,{ratingModelDescription.InputParameter},{ratingModelDescription.OutputParameter}";

                LoadFieldVisitReadings();
            }
        }

        private void LoadFieldVisitReadings()
        {
            var discreteMeasurementJson = SoapClient.GetSiteVisitData(RatingModelDescription.LocationIdentifier);

            FieldVisitReadings = discreteMeasurementJson.FromJson<List<FieldVisitReading>>();
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

        public RatingCurveResult LoadRatingCurve(RatingCurve curve, int stepPrecision, DateTimeOffset curveEffectiveTime)
        {
            var result = new RatingCurveResult();

            var curveCsv = HydroModel.GetExpandedRatingTable(
                curveEffectiveTime.UtcDateTime.ToOADate(),
                stepPrecision,
                CsvMetaHeader,
                CsvMetadata);

            if (string.IsNullOrEmpty(curveCsv))
                return result;

            var csvRating = AQCSV.Load(curveCsv, true, new[] { "ExpandedPoints", "OriginalPoints" });

            var regularStages = CSVUtil.ToDouble(csvRating.dataSet[0].datas, 0);
            var regularDischarges = CSVUtil.ToDouble(csvRating.dataSet[0].datas, 1);
            var shiftStages = CSVUtil.ToDouble(csvRating.dataSet[0].datas, 2);
            var shiftDischarges = CSVUtil.ToDouble(csvRating.dataSet[0].datas, 3);

            for (var i = 0; i < regularStages.Length; ++i)
            {
                result.ExpandedPoints.Add(new ExpandedPoint
                {
                    Stage = regularStages[i],
                    Discharge = regularDischarges[i],
                    ShiftedStage = shiftStages[i],
                    ShiftedDischarge = shiftDischarges[i]
                });
            }

            var equationStages = CSVUtil.ToDouble(csvRating.dataSet[1].datas, 0);
            var equationDischarges = CSVUtil.ToDouble(csvRating.dataSet[1].datas, 1);

            for (var i = 0; i < equationStages.Length; ++i)
            {
                result.EquationPoints.Add(new EquationPoint
                {
                    Stage = equationStages[i],
                    Discharge = equationDischarges[i]
                });
            }

            var curveStartTime = curve.PeriodsOfApplicability.First().StartTime;
            var curveEndTime = curve.PeriodsOfApplicability.Last().EndTime;

            var exceptions = GetRatingExceptions(curve.Id);

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
            var inputReadings = readings.Where(r => r.ParameterId == InputParameter.ParameterId).ToList();
            var outputReadings = readings.Where(r => r.ParameterId == OutputParameter.ParameterId).ToList();

            if (!inputReadings.Any() || !outputReadings.Any())
                return null;

            var outputReading = outputReadings.First();

            // TODO: Should this be RatingMeasurement_T to grab control condition, method, area and width?

            var rm = new RatingMeasurement
            {
                DiscreteMeasurementID = outputReading.DiscreteMeasurementId.ToString(),
                DisplayID = outputReading.DisplayId,
                LocationID = outputReading.LocationId,
                LocationVisitID = outputReading.VisitId,
                MeasuredBy = outputReading.Party,
                MeasurementDetails = outputReading.Remarks,
                Quality = outputReading.Quality?.ToString(),
                Indep = inputReadings.Average(r => r.Result),
                Dep = outputReadings.Average(r => r.Result),
                MeasurementTime = outputReading.MeasurementTime
            };

            return rm;
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

        public AllTablesResult LoadAllTables(int stepPrecision)
        {
            var result = new AllTablesResult();

            var tablesCsv = HydroModel.GetExpandedRatingTables(stepPrecision, CsvMetaHeader, CsvMetadata);

            if (string.IsNullOrEmpty(tablesCsv))
                return result;

            var csvRatings = AQCSV.Load(tablesCsv, true, new[] {"NumTables", "NumPeriods", "NumPoints"});

            var tableNumber = CSVUtil.ToDouble(csvRatings.dataSet[0].datas, 0);
            var tableName = CSVUtil.ToString(csvRatings.dataSet[0].datas, 1);
            var numPeriods = CSVUtil.ToDouble(csvRatings.dataSet[0].datas, 2);
            var numPoints = CSVUtil.ToDouble(csvRatings.dataSet[0].datas, 3);

            for (var i = 0; i < tableNumber.Length; ++i)
            {
                result.Tables.Add(new Table
                {
                    TableNumber = tableNumber[i],
                    TableName = tableName[i],
                    NumPeriods = numPeriods[i],
                    NumPoints = numPoints[i]
                });
            }

            var tableNumber2 = CSVUtil.ToDouble(csvRatings.dataSet[1].datas, 0);
            var startDate = CSVUtil.ToString(csvRatings.dataSet[1].datas, 1);
            var endDate = CSVUtil.ToString(csvRatings.dataSet[1].datas, 2);

            for (var i = 0; i < tableNumber2.Length; ++i)
            {
                result.TableDates.Add(new TableDate
                {
                    TableNumber = tableNumber2[i],
                    StartDate = ParseTime(startDate[i]),
                    EndDate = string.IsNullOrEmpty(endDate[i]) ? DateTimeOffset.MaxValue : ParseTime(endDate[i])
                });
            }

            var tableNumber3 = CSVUtil.ToDouble(csvRatings.dataSet[2].datas, 0);
            var input = CSVUtil.ToDouble(csvRatings.dataSet[2].datas, 1);
            var output = CSVUtil.ToDouble(csvRatings.dataSet[2].datas, 2);

            for (var i = 0; i < tableNumber3.Length; ++i)
            {
                result.TableValues.Add(new TableValue
                {
                    TableNumber = tableNumber3[i],
                    Input = input[i],
                    Output = output[i]
                });
            }

            return result;
        }

        private static DateTimeOffset? ParseTime(string text)
        {
            if (!DateTimeOffset.TryParseExact(text, "yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime))
                return null;

            return dateTime;
        }

        private LegacyDataServiceClient CreateConnectedClient()
        {
            SiteVisit = Client.RegisterCustomClient(PrivateApis.SiteVisit.Root.Endpoint);
            Processor = Client.RegisterCustomClient(PrivateApis.Processor.Root.Endpoint);

            return LegacyDataServiceClient.Create(Context.Server, Context.Username, Context.Password);
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
            return SoapClient.GetRatingModelAop(ratingModelId);
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
