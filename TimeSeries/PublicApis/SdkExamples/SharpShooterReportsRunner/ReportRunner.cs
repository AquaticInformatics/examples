using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
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

            reportSlot.DesignTemplate();
        }

        private InlineReportSlot CreateReportSlot(ReportManager reportManager)
        {
            var reportSlot = new InlineReportSlot {DocumentStream = LoadReportTemplate()};

            reportManager.Reports.Add(reportSlot);

            return reportSlot;
        }

        private string LoadReportTemplate()
        {
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

            Log.Info($"Loading data for {_context.TimeSeries.Count + _context.ExternalDataSets.Count} time-series ...");

            AddDataSets(reportManager, CreateCompatibilityDataSet(), CreateCommonDataSet());
            AddDataSets(reportManager, CreateExternalDataSets().ToArray());
            AddDataSets(reportManager, CreateAllTimeSeriesDataSets().ToArray());

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
            _context.ReportParameters.Select(pair => new object[]{pair.Key, pair.Value}).ToArray());
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

            var correctedData = _client.Publish.Get(new TimeSeriesDataCorrectedServiceRequest
            {
                TimeSeriesUniqueId = timeSeriesDescription.UniqueId,
                QueryFrom = ParseDateTime(timeSeriesDescription, timeSeries.QueryFrom),
                QueryTo = ParseDateTime(timeSeriesDescription, timeSeries.QueryTo),
                Unit = timeSeries.OutputUnitId,
                IncludeGapMarkers = true
            });

            AddLocation(dataSet, timeSeriesDescription.LocationIdentifier);

            AddMetadata(dataSet, timeSeriesDescription, correctedData);

            AddPoints(dataSet, correctedData, timeSeries.GroupBy);

            return dataSet;
        }

        private static DateTimeOffset? ParseDateTime(TimeSeriesDescription timeSeriesDescription, string timeText)
        {
            if (string.IsNullOrWhiteSpace(timeText))
                return null;

            timeText = timeText.Trim();

            // TODO: Support water year
            var dateTime = DateTime.ParseExact(timeText, SupportedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);

            return new DateTimeOffset(dateTime, timeSeriesDescription.UtcOffsetIsoDuration.ToTimeSpan());
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

        private void AddLocation(DataSet dataSet, string locationIdentifier)
        {
            var location = GetLocationData(locationIdentifier);

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
                location.ExtendedAttributes.Select(extendedAttribute => new[]
                {
                    extendedAttribute.Name,
                    extendedAttribute.Name,
                    extendedAttribute.Value,
                }).ToArray());

            CreateTable(dataSet, "Location_Remarks", new List<(string ColumnName, Type ColumnType)>
                {
                    ("Identifier", typeof(string)),
                    ("Name", typeof(string)),
                    ("FromDate", typeof(DateTime)),
                    ("ToDate", typeof(DateTime)),
                    ("Remark", typeof(string)),
                    ("RemarkText", typeof(string)),
                },
                location.LocationRemarks.Select(locationRemark => new object[]
                {
                    location.Identifier,
                    location.LocationName,
                    locationRemark.FromTime?.DateTime,
                    locationRemark.ToTime?.DateTime,
                    locationRemark.Description,
                    locationRemark.Remark,
                }).ToArray());
        }

        private void AddMetadata(DataSet dataSet, TimeSeriesDescription timeSeriesDescription, TimeSeriesDataServiceResponse correctedData)
        {
            var interpolationTypeText = correctedData.InterpolationTypes.First().Type;
            var interpolationType = (int)Enum.Parse(typeof(InterpolationType), interpolationTypeText, true);

            var timeSeriesInfo = _client.Provisioning.Get(new GetTimeSeries {TimeSeriesUniqueId = timeSeriesDescription.UniqueId});

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
                ("AncestorLabel1", typeof(string), timeSeriesInfo.LocationName),
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
            return _client.Publish.Get(new LocationDataServiceRequest { LocationIdentifier = locationIdentifier });
        }

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

            reportSlot.ExceptionMode = ExceptionMode.Fail;
            reportSlot.RenderCompleted += (sender, args) =>
            {
                if (sender is InlineReportSlot)
                {
                    Log.Info($"Render complete.");
                }
            };
            reportSlot.RenderCanceled += (sender, args) =>
            {
                if (sender is InlineReportSlot)
                {
                    Log.Info($"Render cancelled.");
                }
            };
            reportSlot.RenderingError += (sender, args) =>
            {
                if (sender is InlineReportSlot)
                {
                    Log.Error($"Render error detected", args.Exception);

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
            if (string.IsNullOrWhiteSpace(_context.UploadedReportLocationIdentifier))
                return;

            var location = GetLocationData(_context.UploadedReportLocationIdentifier);

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
