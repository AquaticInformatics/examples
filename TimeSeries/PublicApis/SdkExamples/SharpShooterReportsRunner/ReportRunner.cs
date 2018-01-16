using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using log4net;
using PerpetuumSoft.Reporting.Components;
using PerpetuumSoft.Reporting.Export.Pdf;
using PerpetuumSoft.Reporting.Rendering;

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

            var table = dataSet.Tables.Add("Parameters");

            var properties = new List<(string ColumnName, Type ColumnType, object DefaultValue)>
            {
                ("Name", typeof(string), string.Empty),
                ("Description", typeof(string), string.Empty),
                ("Comment", typeof(string), string.Empty),
                ("ShowMessages", typeof(bool), false),
                ("LocIDs", typeof(long[]), new long[0]),
                ("FolderID", typeof(long), 0),
                ("ClientService", typeof(object), null),
            };

            foreach (var property in properties)
            {
                table.Columns.Add(property.ColumnName, property.ColumnType);
            }

            var row = new object[table.Columns.Count];
            for (var i = 0; i < properties.Count; ++i)
            {
                var property = properties[i];
                row[i] = property.DefaultValue;
            }

            table.Rows.Add(row);

            CreateDummyGlobalSettingsTable(dataSet);

            return dataSet;
        }

        private void CreateDummyGlobalSettingsTable(DataSet dataSet)
        {
            var table = dataSet.Tables.Add("GlobalSettings");

            table.Columns.Add("SettingGroup", typeof(string));
            table.Columns.Add("SettingKey", typeof(string));
            table.Columns.Add("SettingValue", typeof(string));
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
            var table = dataSet.Tables.Add("CommandLineParameters");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Value", typeof(string));

            foreach (var pair in _context.ReportParameters)
            {
                var row = new object[table.Columns.Count];
                row[0] = pair.Key;
                row[1] = pair.Value;

                table.Rows.Add(row);
            }
        }

        private void AddSdkConnection(DataSet dataSet)
        {
            var table = dataSet.Tables.Add("Connections");

            table.Columns.Add("AquariusClient", typeof(IAquariusClient));

            var row = new object[table.Columns.Count];
            row[0] = _client;
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

            var dataSet = new DataSet(dataSetName);

            // TimeSeriesInfo

            var response = _client.Publish.Get(new TimeSeriesDataCorrectedServiceRequest
            {
                TimeSeriesUniqueId = timeSeriesDescription.UniqueId,
            });

            // Points
            // Metadata

            return dataSet;
        }

        private TimeSeriesDescription GetTimeSeriesDescription(string timeSeriesIdentifier)
        {
            var uniqueId = GetTimeSeriesUniqueId(timeSeriesIdentifier);

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

            if (timeSeriesDescription == null)
                throw new ArgumentException($"Can't find '{timeSeriesIdentifier}' at location '{location}'");

            return timeSeriesDescription.UniqueId;
        }

        private static string ParseLocationIdentifier(string timeSeriesIdentifier)
        {
            var match = IdentifierRegex.Match(timeSeriesIdentifier);

            if (!match.Success)
                throw new ArgumentException($"Can't parse '{timeSeriesIdentifier}' as time-series identifier. Expecting <Parameter>.<Label>@<Location>");

            return match.Groups["location"].Value;
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

                if (column == null || table.Rows.Count != 1) continue;

                var row = table.Rows[0];

                row[columnName] = Convert.ChangeType(parameterOverride.Value, column.DataType);
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

            Log.Info($"Rendering to '{_context.OutputPath}' ...");

            reportSlot.ExceptionMode = ExceptionMode.Fail;
            reportSlot.RenderCompleted += (sender, args) =>
            {
                if (sender is InlineReportSlot)
                {
                    Log.Info($"Render complete");
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

            const bool doNotShowDialog = false;
            exportFilter.Export(document, _context.OutputPath, doNotShowDialog);

            Log.Info($"Rendered to '{_context.OutputPath}'.");
        }
    }
}
