using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using log4net;
using NodaTime;
using NodaTime.Text;
using PerpetuumSoft.Reporting.Components;
using PerpetuumSoft.Reporting.Export.Pdf;
using PerpetuumSoft.Reporting.Rendering;
using InterpolationType = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.InterpolationType;

namespace SharpShooterReportsRunner
{
    public class ReportRunner : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Context _context;
        private readonly IAquariusClient _client;

        public ReportRunner(Context context)
        {
            _context = context;
            _client = CreateConnectedClient();
        }

        public void Dispose()
        {
            _client?.Dispose();
        }

        private IAquariusClient CreateConnectedClient()
        {
            if (string.IsNullOrWhiteSpace(_context.Server))
                throw new ExpectedException("No -Server=value was specified.");

            if (string.IsNullOrWhiteSpace(_context.Username))
                throw new ExpectedException("No -Username=value was specified.");

            if (string.IsNullOrWhiteSpace(_context.Password))
                throw new ExpectedException("No -Password=value was specified.");

            var client = AquariusClient.CreateConnectedClient(_context.Server, _context.Username, _context.Password);

            Log.Info($"Connected to {_context.Server} ({client.ServerVersion})");

            return client;
        }

        public void Run()
        {
            if (!File.Exists(_context.TemplatePath))
                throw new ExpectedException($"Can't find template file '{_context.TemplatePath}'.");

            if (_context.LaunchReportDesigner)
            {
                LaunchReportDesigner();
                return;
            }

            RenderReport();
            UploadReport();
        }

        private void LaunchReportDesigner()
        {
            var reportManager = CreateReportManager();

            var reportSlot = CreateReportSlot(reportManager);

            Log.Info("Launching SharpShooter Reports Designer ...");

            reportSlot.DesignTemplate();

            Log.Info("SharpShooter Reports Designer has exited.");
        }

        private InlineReportSlot CreateReportSlot(ReportManager reportManager)
        {
            var reportSlot = new InlineReportSlot {DocumentStream = LoadReportTemplate()};

            reportManager.Reports.Add(reportSlot);

            return reportSlot;
        }

        private string LoadReportTemplate()
        {
            Log.Info($"Loading report template '{_context.TemplatePath}' ...");

            var template = File.ReadAllText(_context.TemplatePath)
                .Replace(
                    FormatAssemblyQualifiedName("ReportApp"),
                    FormatAssemblyQualifiedName(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)));

            return template;
        }

        private static string FormatAssemblyQualifiedName(string name)
        {
            return $", {name}, Version=";
        }

        private ReportManager CreateReportManager()
        {
            var reportManager = new ReportManager();

            Log.Info($"Loading data for {_context.TimeSeries.Count + _context.ExternalDataSets.Count + _context.RatingModels.Count} data-sets ...");

            AddDataSets(reportManager, CreateCompatibilityDataSet(), CreateCommonDataSet());
            AddDataSets(reportManager, CreateExternalDataSets().ToArray());
            AddDataSets(reportManager, CreateAllTimeSeriesDataSets().ToArray());
            AddDataSets(reportManager, CreateAllRatingModelDataSets().ToArray());

            MergeParameterOverrides(reportManager);

            return reportManager;
        }

        private static void AddDataSets(ReportManager reportManager, params DataSet[] dataSets)
        {
            foreach (var dataSet in dataSets)
            {
                reportManager.DataSources.Add(dataSet.DataSetName, dataSet);
            }
        }

        private DataSet CreateCompatibilityDataSet()
        {
            // Lifted from <AquariusRepo>\AQTCSReportApp\ReportApp\ReportingLib.cs: ReportApp.ReportDescription.AddReportParametersToRMan()
            var dataSet = new DataSet("ReportParameters");

            CreateSingleRowTable(dataSet, "Parameters", new List<(string ColumnName, Type ColumnType, object RowValue)>
            {
                ("Name", typeof(string), string.Empty),
                ("Description", typeof(string), string.Empty),
                ("Comment", typeof(string), string.Empty),
                ("ShowMessages", typeof(bool), false),
                ("LocIDs", typeof(long[]), new long[0]),
                ("FolderID", typeof(long), 0),
                ("ClientService", typeof(object), null),
            });

            CreateDummyGlobalSettingsTable(dataSet);

            return dataSet;
        }

        private static void CreateSingleRowTable(DataSet dataSet, string tableName, List<(string ColumnName, Type ColumnType, object RowValue)> columns)
        {
            CreateTable(
                dataSet,
                tableName,
                columns.Select(c => (c.ColumnName, c.ColumnType)),
                columns.Select(c => c.RowValue).ToArray());
        }

        private static void CreateTable<TItem>(
            DataSet dataSet,
            string tableName,
            IEnumerable<(string ColumnName, Type ColumnType)> columns,
            IEnumerable<TItem> items,
            Func<TItem,object[]> itemConverter) where TItem : new()
        {
            CreateTable(dataSet, tableName, columns, items.Select(itemConverter).ToArray());
        }

        private static DataTable CreateTable(DataSet dataSet, string tableName, IEnumerable<(string ColumnName, Type ColumnType)> columns, params object[][] rows)
        {
            var table = dataSet.Tables.Add(tableName);

            foreach (var column in columns)
            {
                table.Columns.Add(column.ColumnName, column.ColumnType);
            }

            foreach (var row in rows)
            {
                table.Rows.Add(row);
            }

            return table;
        }

        private void CreateDummyGlobalSettingsTable(DataSet dataSet)
        {
            CreateTable(dataSet, "GlobalSettings", new List<(string ColumnName, Type ColumnType)>
            {
                ("SettingGroup", typeof(string)),
                ("SettingKey", typeof(string)),
                ("SettingValue", typeof(string)),
            });
        }

        private DataSet CreateCommonDataSet()
        {
            var dataSet = new DataSet("Common");

            AddCommandLineParameters(dataSet);
            AddSdkConnection(dataSet);

            return dataSet;
        }

        private void AddCommandLineParameters(DataSet dataSet)
        {
            CreateTable(dataSet, "CommandLineParameters", new List<(string ColumnName, Type ColumnType)>
                {
                    ("Name", typeof(string)),
                    ("Value", typeof(string)),
                },
                _context.ReportParameters,
                pair => new object[]
                {
                    pair.Key,
                    pair.Value
                });
        }

        private void AddSdkConnection(DataSet dataSet)
        {
            CreateSingleRowTable(dataSet, "Connections", new List<(string ColumnName, Type ColumnType, object RowValue)>
            {
                ("AquariusClient", typeof(IAquariusClient), _client)
            });
        }

        private IEnumerable<DataSet> CreateExternalDataSets()
        {
            return _context.ExternalDataSets.Select(CreateExternalDataSet);
        }

        private DataSet CreateExternalDataSet(ExternalDataSet externalDataSet)
        {
            Log.Info($"Loading external data set '{externalDataSet.Path}' ...");
            var dataSet = new DataSet();
            dataSet.ReadXml(externalDataSet.Path);

            if (!string.IsNullOrWhiteSpace(externalDataSet.Name))
                dataSet.DataSetName = externalDataSet.Name;

            return dataSet;
        }

        private IEnumerable<DataSet> CreateAllTimeSeriesDataSets()
        {
            return _context.TimeSeries.Select((timeSeries, i) => CreateTimeSeriesDataSet(timeSeries, $"TimeSeries{i + 1}"));
        }

        private DataSet CreateTimeSeriesDataSet(TimeSeries timeSeries, string dataSetName)
        {
            var timeSeriesDescription = GetTimeSeriesDescription(timeSeries.Identifier);

            if (timeSeriesDescription == null)
                throw new ExpectedException($"Can't find time-series '{timeSeries.Identifier}'");

            var dataSet = new DataSet(dataSetName);

            var request = new TimeSeriesDataCorrectedServiceRequest
            {
                TimeSeriesUniqueId = timeSeriesDescription.UniqueId,
                QueryFrom = ParseDateTime(timeSeriesDescription, timeSeries.QueryFrom),
                QueryTo = ParseDateTime(timeSeriesDescription, timeSeries.QueryTo),
                Unit = timeSeries.OutputUnitId,
                IncludeGapMarkers = true
            };

            var summary = new StringBuilder();
            summary.Append($"Loading time-series '{timeSeriesDescription.Identifier}'");

            if (request.QueryFrom.HasValue)
                summary.Append($" with QueryFrom={request.QueryFrom:O}");

            if (request.QueryTo.HasValue)
                summary.Append($" with QueryTo={request.QueryTo:O}");

            if (!string.IsNullOrEmpty(request.Unit))
                summary.Append($" with output unit '{request.Unit}'");

            Log.Info(summary.ToString());

            var correctedData = _client.Publish.Get(request);

            Log.Info($"Creating {dataSetName} dataset ...");

            var locationData = GetLocationData(timeSeriesDescription.LocationIdentifier);

            AddLocation(dataSet, locationData);

            AddMetadata(dataSet, timeSeriesDescription, correctedData);

            AddPoints(dataSet, correctedData, timeSeries.GroupBy);

            return dataSet;
        }

        private IEnumerable<DataSet> CreateAllRatingModelDataSets()
        {
            return _context.RatingModels.Select((ratingModel, i) => CreateRatingModelDataSet(ratingModel, $"RatingCurve{i + 1}"));
        }

        private DataSet CreateRatingModelDataSet(RatingModel ratingModel, string dataSetName)
        {
            // Get the location, so we can know the UtcOffset
            var locationIdentifier = ParseLocationIdentifier(ratingModel.Identifier);

            var ratingModelDescriptions = _client.Publish.Get(new RatingModelDescriptionListServiceRequest {LocationIdentifier = locationIdentifier})
                .RatingModelDescriptions;

            var ratingModelDescription = ratingModelDescriptions
                .FirstOrDefault(r => r.Identifier == ratingModel.Identifier);

            if (ratingModelDescription == null)
                throw new ExpectedException($"Can't find rating model '{ratingModel.Identifier}'.");

            var locationData = GetLocationData(locationIdentifier);

            var request = new RatingCurveListServiceRequest
            {
                RatingModelIdentifier = ratingModelDescription.Identifier,
                QueryFrom = ParseDateTime(locationData, ratingModel.QueryFrom),
                QueryTo = ParseDateTime(locationData, ratingModel.QueryTo),
            };

            var stepSize = double.TryParse(ratingModel.StepSize, NumberStyles.Any, CultureInfo.InvariantCulture, out var incrementSize)
                ? incrementSize
                : 0.01;

            var stepPrecision = (int)Math.Round(Math.Log(stepSize) / Math.Log(0.1));

            var summary = new StringBuilder();
            summary.Append($"Loading rating model '{request.RatingModelIdentifier}'");

            if (request.QueryFrom.HasValue)
                summary.Append($" with QueryFrom={request.QueryFrom:O}");

            if (request.QueryTo.HasValue)
                summary.Append($" with QueryTo={request.QueryTo:O}");

            summary.Append($" with StepSize={stepSize}");

            Log.Info(summary.ToString());

            var curveEffectiveTime = request.QueryFrom ?? DateTimeOffset.UtcNow;

            var ratingCurves = _client.Publish.Get(request);

            var ratingModelLoader = new RatingModelLoader
                {
                    Context = _context,
                    Client = _client
                };
            ratingModelLoader.Load(ratingModelDescription);

            var ratingCurveResult = ratingModelLoader.LoadRatingCurve(ratingCurves.RatingCurves.First(), stepPrecision, curveEffectiveTime);
            var allTablesResult = ratingModelLoader.LoadAllTables(stepPrecision);

            var dataSet = new DataSet(dataSetName);

            AddLocation(dataSet, locationData);

            AddMetadata(dataSet, locationData, ratingModelDescription);

            var expandedRatingCurve = _client.Publish.Get(new EffectiveRatingCurveServiceRequest
            {
                RatingModelIdentifier = ratingModelDescription.Identifier,
                StepSize = stepSize,
                EffectiveTime = request.QueryFrom
            }).ExpandedRatingCurve;

            var expandedPointsTable = CreateTable(dataSet, "ExpandedPoints", new List<(string ColumnName, Type ColumnType)>
            {
                ("Stage", typeof(double)),
                ("Discharge", typeof(double)),
                ("ShiftStage", typeof(double)),
                ("ShiftDischarge", typeof(double)),
            });

            var maxCount = Math.Max(expandedRatingCurve.AdjustedRatingTable.Count, expandedRatingCurve.BaseRatingTable.Count);

            for (var i = 0; i < maxCount; ++i)
            {
                var row = new object[expandedPointsTable.Columns.Count];

                var adjustedRatingPoint = i < expandedRatingCurve.AdjustedRatingTable.Count
                    ? expandedRatingCurve.AdjustedRatingTable[i]
                    : null;

                var baseRatingPointPoint = i < expandedRatingCurve.BaseRatingTable.Count
                    ? expandedRatingCurve.BaseRatingTable[i]
                    : null;

                row[0] = baseRatingPointPoint?.InputValue;
                row[1] = baseRatingPointPoint?.OutputValue;
                row[2] = adjustedRatingPoint?.InputValue;
                row[3] = adjustedRatingPoint?.OutputValue;

                expandedPointsTable.Rows.Add(row);
            }

            CreateTable(dataSet, "EquationPoints",
                new List<(string ColumnName, Type ColumnType)>
                {
                    ("Stage", typeof(double)),
                    ("Discharge", typeof(double)),
                },
                ratingCurveResult.EquationPoints,
                equationPoint => new object[]
                {
                    equationPoint.Stage,
                    equationPoint.Discharge
                });

            // ExpandedMetadata - single row
            CreateSingleRowTable(dataSet, "ExpandedMetadata", new List<(string ColumnName, Type ColumnType, object DefaultValue)>
            {
                ("DateTime", typeof(DateTimeOffset), curveEffectiveTime),
                ("InputParameterName", typeof(string), ratingModelDescription.InputParameter),
                ("OutputParameterName", typeof(string), ratingModelDescription.OutputParameter),
                ("Precision", typeof(int), stepPrecision),
                ("StartDate", typeof(DateTimeOffset), expandedRatingCurve.PeriodsOfApplicability.First().StartTime),
                ("EndDate", typeof(DateTimeOffset), expandedRatingCurve.PeriodsOfApplicability.Last().EndTime),
            });

            // RatingMeasurementsMetadata - single row
            CreateSingleRowTable(dataSet, "RatingMeasurementsMetadata", new List<(string ColumnName, Type ColumnType, object DefaultValue)>
            {
                ("Label", typeof(string), ratingModelDescription.Label),
                ("Description", typeof(string), ratingModelDescription.Description),
                ("Comment", typeof(string), ratingModelDescription.Comment),
                ("IndepVariableParameterName", typeof(string), ratingModelDescription.InputParameter),
                ("IndepVariableUnits", typeof(string), ratingModelDescription.InputUnit),
                ("DepVariableParameterName", typeof(string), ratingModelDescription.OutputParameter),
                ("DepVariableUnits", typeof(string), ratingModelDescription.OutputUnit),
                ("TimeZone", typeof(string), $"UTC{OffsetPattern.GeneralInvariantPattern.Format(Offset.FromTicks(TimeSpan.FromHours(locationData.UtcOffset).Ticks))}"),
                ("AncestorName1", typeof(string), "Location"),
                ("AncestorLabel1", typeof(string), locationData.LocationName),
            });

            var locationUtcOffset = TimeSpan.FromHours(locationData.UtcOffset);

            // RatingMeasurements - many rows
            CreateTable(dataSet, "RatingMeasurements", new List<(string ColumnName, Type ColumnType)>
                {
                    ("Area", typeof(double)),
                    ("Condition", typeof(string)),
                    ("DepVariableValue", typeof(double)),
                    ("DepVariableDescription", typeof(string)),
                    ("IndepVariableValue", typeof(double)),
                    ("IndepVariableDescription", typeof(string)),
                    ("EffectiveDepth", typeof(double)),
                    ("MeanVelocity", typeof(double)),
                    ("MeasuredBy", typeof(string)),
                    ("MeasurementEpoch", typeof(double)),
                    ("MeasurementTime", typeof(double)),
                    ("MeasurementTimestamp", typeof(DateTimeOffset)),
                    ("Method", typeof(string)),
                    ("Quality", typeof(string)),
                    ("StageChange", typeof(double)),
                    ("Verticals", typeof(double)),
                    ("Width", typeof(double)),
                },
                ratingCurveResult.RatingMeasurements,
                rm => new object[]
                {
                    rm.Area,
                    "Unknown condition",
                    rm.Dep,
                    ratingModelDescription.OutputParameter,
                    rm.Indep,
                    ratingModelDescription.InputParameter,
                    null, //Depth
                    null, //Velocity
                    rm.MeasuredBy,
                    0, // Epoch
                    rm.MeasurementTime?.ToOADate(),
                    rm.MeasurementTime.HasValue
                        ? new DateTimeOffset(
                            DateTime.SpecifyKind(rm.MeasurementTime.Value.Add(locationUtcOffset),
                                DateTimeKind.Unspecified), locationUtcOffset)
                        : (DateTimeOffset?) null,
                    null, // method
                    rm.Quality,
                    null, // StageChange
                    null, // Verticals
                    rm.Width
                });

            // Tables - many rows (one per curve): ID, name, numPeriods, numPoints
            CreateTable(dataSet, "Tables", new List<(string ColumnName, Type ColumnType)>
                {
                    ("TableNumber", typeof(double)),
                    ("TableName", typeof(string)),
                    ("NumPeriods", typeof(double)),
                    ("NumPoints", typeof(double)),
                },
                allTablesResult.Tables,
                table => new object[]
                {
                    table.TableNumber,
                    table.TableName,
                    table.NumPeriods,
                    table.NumPoints,
                });

            // TableDates - many rows (one per curve): ID, StartDate, EndDate
            CreateTable(dataSet, "TableDates", new List<(string ColumnName, Type ColumnType)>
                {
                    ("TableNumber", typeof(double)),
                    ("StartDate", typeof(DateTime)),
                    ("EndDate", typeof(DateTime)),
                },
                allTablesResult.TableDates,
                tableDate => new object[]
                {
                    tableDate.TableNumber,
                    tableDate.StartDate?.DateTime,
                    tableDate.EndDate?.DateTime,
                });

            // TableValues - many rows: ID, inputValue, outputValue
            CreateTable(dataSet, "TableValues", new List<(string ColumnName, Type ColumnType)>
                {
                    ("TableNumber", typeof(double)),
                    ("Input", typeof(double)),
                    ("Output", typeof(double)),
                },
                allTablesResult.TableValues,
                tableValue => new object[]
                {
                    tableValue.TableNumber,
                    tableValue.Input,
                    tableValue.Output,
                });

            // CurveMetadata - single row
            CreateSingleRowTable(dataSet, "CurveMetadata", new List<(string ColumnName, Type ColumnType, object DefaultValue)>
            {
                ("InParameterName", typeof(string), ratingModelDescription.InputParameter),
                ("OutParameterName", typeof(string), ratingModelDescription.OutputParameter),
                ("NumTables", typeof(string), allTablesResult.Tables.Count.ToString()),
                ("NumPeriods", typeof(string), allTablesResult.Tables.Sum(t => (int)t.NumPeriods).ToString()),
                ("NumPoints", typeof(string), allTablesResult.Tables.Sum(t => (int)t.NumPoints).ToString()),
            });

            return dataSet;
        }

        private void AddMetadata(DataSet dataSet, LocationDataServiceResponse locationData, RatingModelDescription ratingModelDescription)
        {
            CreateSingleRowTable(dataSet, "MetaData", new List<(string ColumnName, Type ColumnType, object DefaultValue)>
            {
                ("Label", typeof(string), ratingModelDescription.Label),
                ("Description", typeof(string), ratingModelDescription.Description),
                ("Comment", typeof(string), ratingModelDescription.Comment),
                ("InputParameterName", typeof(string), ratingModelDescription.InputParameter),
                ("InputUnits", typeof(string), ratingModelDescription.InputUnit),
                ("OutputParameterName", typeof(string), ratingModelDescription.OutputParameter),
                ("OutputUnits", typeof(string), ratingModelDescription.OutputUnit),
                ("TimeZone", typeof(string), $"UTC{OffsetPattern.GeneralInvariantPattern.Format(Offset.FromTicks(TimeSpan.FromHours(locationData.UtcOffset).Ticks))}"),
                ("AncestorName1", typeof(string), "Location"),
                ("AncestorLabel1", typeof(string), locationData.LocationName),
            });
        }

        private static DateTimeOffset? ParseDateTime(TimeSeriesDescription timeSeriesDescription, string timeText)
        {
            return ParseDateTime(timeText, () => timeSeriesDescription.UtcOffsetIsoDuration.ToTimeSpan());
        }

        private static DateTimeOffset? ParseDateTime(LocationDataServiceResponse location, string timeText)
        {
            return ParseDateTime(timeText, () => TimeSpan.FromHours(location.UtcOffset));
        }

        private static DateTimeOffset? ParseDateTime(string timeText, Func<TimeSpan> utcOffsetFunc)
        {
            if (string.IsNullOrWhiteSpace(timeText))
                return null;

            timeText = timeText.Trim();

            // TODO: Support water year
            var dateTime = DateTime.ParseExact(timeText, SupportedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
            var utcOffset = utcOffsetFunc();

            var dateTimeOffset = new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), utcOffset);

            return dateTimeOffset;
        }

        private static readonly string[] SupportedDateFormats =
        {
            "yyyy",
            "yyyy-MM",
            "yyyy-MM-dd",
            "yyyy-MM-ddThh:mm",
            "yyyy-MM-ddThh:mm:ss",
            "yyyy-MM-ddThh:mm:ss.fff",
        };

        private void AddLocation(DataSet dataSet, LocationDataServiceResponse location)
        {
            CreateSingleRowTable(dataSet, "Location", new List<(string ColumnName, Type ColumnType, object RowValue)>
            {
                ("Indentifier", typeof(string), location.Identifier), // Yay spelling!
                ("Identifier", typeof(string), location.Identifier),
                ("Name", typeof(string), location.LocationName),
                ("Description", typeof(string), location.Description),
                ("Elevation", typeof(double), location.Elevation),
                ("ElevationUnits", typeof(string), location.ElevationUnits),
                ("Timezone", typeof(string), $"UTC{OffsetPattern.GeneralInvariantPattern.Format(Offset.FromHours((int)location.UtcOffset))}"),
                ("Latitude", typeof(double), location.Latitude),
                ("Longitude", typeof(double), location.Longitude),
                ("LocationType", typeof(string), location.LocationType),
                ("GeoDatum", typeof(string), location.LocationDatum?.ReferenceStandard?.ReferenceStandard),
            });

            CreateTable(dataSet, "Location_AdditionalProperties", new List<(string ColumnName, Type ColumnType)>
                {
                    ("ColumnName", typeof(string)),
                    ("Caption", typeof(string)),
                    ("Value", typeof(string)),
                },
                location.ExtendedAttributes,
                extendedAttribute => new[]
                {
                    extendedAttribute.Name,
                    extendedAttribute.Name,
                    extendedAttribute.Value,
                });

            CreateSingleRowTable(dataSet, "Location_Extension", location.ExtendedAttributes.Select(ConvertExtendedAttribute).ToList());

            CreateTable(dataSet, "Location_Remarks", new List<(string ColumnName, Type ColumnType)>
                {
                    ("Identifier", typeof(string)),
                    ("Name", typeof(string)),
                    ("FromDate", typeof(DateTime)),
                    ("ToDate", typeof(DateTime)),
                    ("Remark", typeof(string)),
                    ("RemarkText", typeof(string)),
                },
                location.LocationRemarks,
                locationRemark => new object[]
                {
                    location.Identifier,
                    location.LocationName,
                    locationRemark.FromTime?.DateTime,
                    locationRemark.ToTime?.DateTime,
                    locationRemark.Description,
                    locationRemark.Remark,
                });
        }

        private static (string ColumnName, Type ColumnType, object RowValue) ConvertExtendedAttribute(ExtendedAttribute attribute)
        {
            if (!KnownTypes.TryGetValue(attribute.Type, out var type))
            {
                type = attribute.Value.GetType();
            }

            return (attribute.Name, type, attribute.Value);
        }

        private static readonly Dictionary<string, Type> KnownTypes = new[]
            {
                typeof(bool),
                typeof(string),
                typeof(int),
                typeof(long),
                typeof(short),
                typeof(double),
                typeof(float),
                typeof(Boolean),
                typeof(DateTime),
                typeof(Decimal),
                typeof(Double),
                typeof(Single),
                typeof(Int64),
                typeof(UInt64),
                typeof(Int32),
                typeof(UInt32),
                typeof(Int16),
                typeof(UInt16),
            }
            .Distinct()
            .ToDictionary(t => t.Name, t => t, StringComparer.InvariantCultureIgnoreCase);

        private void AddMetadata(DataSet dataSet, TimeSeriesDescription timeSeriesDescription, TimeSeriesDataServiceResponse correctedData)
        {
            var interpolationTypeText = correctedData.InterpolationTypes.First().Type;
            var interpolationType = (int)Enum.Parse(typeof(InterpolationType), interpolationTypeText, true);

            var locationData = GetLocationData(timeSeriesDescription.LocationIdentifier);

            var maxGapIntervalDays = 0.0;
            var maxGapIntervalMinutes = correctedData.GapTolerances.Max(gt => gt.ToleranceInMinutes);

            if (maxGapIntervalMinutes.HasValue)
            {
                maxGapIntervalDays = maxGapIntervalMinutes.Value / (24 * 60);
            }

            var startTime = timeSeriesDescription.RawStartTime;
            var endTime = timeSeriesDescription.RawEndTime;

            CreateSingleRowTable(dataSet, "MetaData", new List<(string ColumnName, Type ColumnType, object DefaultValue)>
            {
                ("Identifier", typeof(string), timeSeriesDescription.Identifier),
                ("Label", typeof(string), timeSeriesDescription.Label),
                ("Description", typeof(string), timeSeriesDescription.Description),
                ("Comment", typeof(string), timeSeriesDescription.Comment),
                ("ParameterName", typeof(string), timeSeriesDescription.Parameter),
                ("Units", typeof(string), timeSeriesDescription.Unit),
                ("TimeZone", typeof(string), $"UTC{OffsetPattern.GeneralInvariantPattern.Format(timeSeriesDescription.UtcOffsetIsoDuration)}"),
                ("StartTime", typeof(DateTime), startTime?.DateTime),
                ("EndTime", typeof(DateTime), endTime?.DateTime),
                ("MaxGapInterval", typeof(double), maxGapIntervalDays),
                ("ParameterType", typeof(string), timeSeriesDescription.Parameter),
                ("FirstTime", typeof(double), startTime?.DateTime.ToOADate()),
                ("LastTime", typeof(double), endTime?.DateTime.ToOADate()),
                ("FirstInterpolationCode", typeof(int), interpolationType),
                ("FirstInterpolationCodeName", typeof(string), $"{interpolationType} - {interpolationTypeText}"),
                ("AncestorName1", typeof(string), "Location"),
                ("AncestorLabel1", typeof(string), locationData.LocationName),
            });
        }

        private void AddPoints(DataSet dataSet, TimeSeriesDataServiceResponse correctedData, GroupBy groupBy)
        {
            var interpolationTypeText = correctedData.InterpolationTypes.First().Type;
            var interpolationType = (int)Enum.Parse(typeof(InterpolationType), interpolationTypeText, true);

            var groupByTable = CreateTable(dataSet, "GroupBy", new List<(string ColumnName, Type ColumnType)>
            {
                ("GroupBy", typeof(int)),
                ("TimeStamp", typeof(DateTime)),
            });

            var pointsTable = CreateTable(dataSet, "CorrectedData", new List<(string ColumnName, Type ColumnType)>
            {
                ("GroupBy", typeof(int)),
                ("TimeStamp", typeof(DateTime)),
                ("Value", typeof(double)),
                ("Approval", typeof(long)),
                ("InterpolationType", typeof(long)),
                ("Grade", typeof(string)),
            });

            if (groupBy != GroupBy.None)
            {
                dataSet.Relations.Add("GroupByCorrectedData", groupByTable.Columns["GroupBy"], pointsTable.Columns["GroupBy"]);
            }

            var group = 0;
            var lastGroupId = (DateTimeOffset?) null;

            foreach (var point in correctedData.Points)
            {
                var dateTimeOffset = point.Timestamp.DateTimeOffset;
                var groupId = CreateGroupId(dateTimeOffset, groupBy);

                if (lastGroupId == null || lastGroupId != groupId)
                {
                    ++group;
                    lastGroupId = groupId;

                    var groupRow = new object[groupByTable.Columns.Count];
                    groupRow[0] = group;
                    groupRow[1] = groupId?.DateTime;

                    groupByTable.Rows.Add(groupRow);
                }

                var row = new object[pointsTable.Columns.Count];

                row[0] = group;
                row[1] = dateTimeOffset.DateTime;
                row[2] = point.Value.Numeric;
                row[3] = correctedData.Approvals.Single(a => a.StartTime <= dateTimeOffset && a.EndTime > dateTimeOffset).ApprovalLevel;
                row[4] = interpolationType;
                row[5] = correctedData.Grades.Single(g => g.StartTime <= dateTimeOffset && g.EndTime > dateTimeOffset).GradeCode;

                pointsTable.Rows.Add(row);
            }
        }

        private static DateTimeOffset? CreateGroupId(DateTimeOffset dateTimeOffset, GroupBy groupBy)
        {
            if (groupBy == GroupBy.None)
                return null;

            var year = dateTimeOffset.Year;
            var month = dateTimeOffset.Month;
            var day = dateTimeOffset.Day;

            switch (groupBy)
            {
                case GroupBy.Month:
                {
                    day = 1;
                    break;
                }

                case GroupBy.Year:
                {
                    day = 1;
                    month = 1;
                    break;
                }

                case GroupBy.Decade:
                {
                    day = 1;
                    month = 1;
                    year /= 10;
                    year *= 10;
                    break;
                }

                case GroupBy.Week:
                {
                    var startOfDay = new DateTimeOffset(year, month, day, 0, 0, 0, dateTimeOffset.Offset);

                    while (startOfDay.DayOfWeek != DayOfWeek.Monday)
                    {
                        startOfDay = startOfDay.AddDays(-1);
                    }

                    return startOfDay;
                }
            }

            return new DateTimeOffset(year, month, day, 0, 0, 0, dateTimeOffset.Offset);
        }

        private TimeSeriesDescription GetTimeSeriesDescription(string timeSeriesIdentifier)
        {
            var uniqueId = GetTimeSeriesUniqueId(timeSeriesIdentifier);

            if (uniqueId == Guid.Empty)
                return null;

            return _client.Publish
                .Get(new TimeSeriesDescriptionListByUniqueIdServiceRequest
                {
                    TimeSeriesUniqueIds = new List<Guid> {uniqueId}
                })
                .TimeSeriesDescriptions
                .Single();
        }

        private Guid GetTimeSeriesUniqueId(string timeSeriesIdentifier)
        {
            if (Guid.TryParse(timeSeriesIdentifier, out var uniqueId))
                return uniqueId;

            var location = ParseLocationIdentifier(timeSeriesIdentifier);

            var response = _client.Publish.Get(new TimeSeriesDescriptionServiceRequest { LocationIdentifier = location });

            var timeSeriesDescription = response.TimeSeriesDescriptions.FirstOrDefault(t => t.Identifier == timeSeriesIdentifier);

            return timeSeriesDescription?.UniqueId ?? Guid.Empty;
        }

        private static string ParseLocationIdentifier(string timeSeriesIdentifier)
        {
            var match = IdentifierRegex.Match(timeSeriesIdentifier);

            if (!match.Success)
                throw new ArgumentException($"Can't parse '{timeSeriesIdentifier}' as time-series identifier. Expecting <Parameter>.<Label>@<Location>");

            return match.Groups["location"].Value;
        }

        private LocationDataServiceResponse GetLocationData(string locationIdentifier)
        {
            if (_locationCache.TryGetValue(locationIdentifier, out var locationData))
                return locationData;

            locationData = _client.Publish.Get(new LocationDataServiceRequest {LocationIdentifier = locationIdentifier});

            _locationCache.Add(locationIdentifier, locationData);

            return locationData;
        }

        private readonly Dictionary<string,LocationDataServiceResponse> _locationCache = new Dictionary<string, LocationDataServiceResponse>();

        private static readonly Regex IdentifierRegex = new Regex(@"^(?<parameter>[^.]+)\.(?<label>[^@]+)@(?<location>.*)$");

        private void MergeParameterOverrides(ReportManager reportManager)
        {
            foreach (var parameterOverride in _context.ParameterOverrides)
            {
                var components = parameterOverride.Key.Split(ParameterOverrideSeparators, StringSplitOptions.RemoveEmptyEntries);

                if (components.Length != 3)
                    continue;

                var dataSetName = components[0];
                var tableName = components[1];
                var columnName = components[2];

                if (!(reportManager.DataSources[dataSetName] is DataSet dataSet)) continue;

                var table = dataSet.Tables[tableName];

                var column = table?.Columns[columnName];

                if (column == null || table.Rows.Count > 1) continue;

                var isNewRow = table.Rows.Count == 0;

                var row = isNewRow
                    ? table.NewRow()
                    : table.Rows[0];

                row[columnName] = Convert.ChangeType(parameterOverride.Value, column.DataType);

                if (isNewRow)
                    table.Rows.Add(row);
            }
        }

        private static readonly char[] ParameterOverrideSeparators = {'.'};

        private void RenderReport()
        {
            if (string.IsNullOrWhiteSpace(_context.OutputPath))
                throw new ExpectedException("No -OutputPath was specified.");

            var reportManager = CreateReportManager();

            var reportSlot = CreateReportSlot(reportManager);

            // TODO: Switch output type based upon Path.GetExtension(_context.OutputPath)
            var exportFilter = new PdfExportFilter();

            Log.Info($"Rendering report ...");

            var errorCount = 0;

            reportSlot.ExceptionMode = ExceptionMode.Fail;
            reportSlot.RenderCompleted += (sender, args) =>
            {
                if (sender is InlineReportSlot)
                {
                    if (errorCount > 0)
                        Log.Warn($"Render complete with {errorCount} errors.");
                    else
                        Log.Info($"Render complete.");
                }
            };
            reportSlot.RenderCanceled += (sender, args) =>
            {
                if (sender is InlineReportSlot)
                {
                    Log.Info($"Render canceled.");
                }
            };
            reportSlot.RenderingError += (sender, args) =>
            {
                if (sender is InlineReportSlot)
                {
                    Log.Error($"Render error detected", args.Exception);
                    ++errorCount;

                    args.Handled = true;
                }
            };
            
            var document = reportSlot.RenderDocument();

            Log.Info($"Exporting to PDF ...");

            const bool doNotShowDialog = false;
            exportFilter.Export(document, _context.OutputPath, doNotShowDialog);

            Log.Info($"Exported to '{_context.OutputPath}'.");
        }

        private void UploadReport()
        {
            if (string.IsNullOrWhiteSpace(_context.UploadedReportLocation))
                return;

            var location = GetLocationData(_context.UploadedReportLocation);

            var reportTitle = !string.IsNullOrWhiteSpace(_context.UploadedReportTitle)
                ? _context.UploadedReportTitle
                : Path.GetFileNameWithoutExtension(_context.OutputPath);

            Log.Info($"Uploading external report '{reportTitle}' to {location.Identifier} ...");

            _client.Acquisition.PostFileWithRequest(_context.OutputPath, new PostReportAttachment
            {
                Title = reportTitle,
                LocationUniqueId = location.UniqueId
            });

            Log.Info($"Upload of external report complete.");
        }
    }
}
