using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using NodaTime;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using static ReflectedSeriesAggregator.AquariusClientHelperExt;
using MonitoringMethod = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.MonitoringMethod;
using Parameter = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.Parameter;
using PostReflectedTimeSeries = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.PostReflectedTimeSeries;
using TimeSeriesPoint = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.TimeSeriesPoint;

namespace ReflectedSeriesAggregator
{
    public class Work
    {
        List<LocationDescription> _allLocationDescriptions;
        List<Parameter> _allParameters;
        List<MonitoringMethod> _allMonitoringMethods;
        ILogger _logger { get; }
        Settings _settings;
        IAquariusClient _client;

        public Work(ILogger logger, Settings settings)
        {
            _logger = logger;
            _settings = settings;
        }

        public void Run()
        {
            var stopWatch = Stopwatch.StartNew();
            _logger.Information("Running...");

            try
            {
                ConnectClient();
                Initialise();
                ValidateAndFix();

                _logger.Debug("Building aggregate source list.");
                var workItems = BuildWorkItems();

                _logger.Information($"Found {workItems.Count} aggregate(s)...");
                foreach (var workItem in workItems)
                {
                    try
                    {
                        ProcessWorkItem(workItem);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"{ex.Message}", ex);
                    }
                }
                _logger.Information($"Process Commplete. Time Taken: {stopWatch.Elapsed}");
            }
            catch (Exception ex)
            {
                _logger.Fatal($"{ex.Message}", ex);
                _logger.Fatal($"Process Failed. Time Taken: {stopWatch.Elapsed}");
            }
            finally
            {
                _client?.Dispose();
            }
        }

        TimeSeriesReadings AggregateReadings(WorkItem workItem)
        {
            TimeSeriesReadings readings = new TimeSeriesReadings();
            foreach (var tsd in workItem.GroupedTimeSeriesSourceDescriptions)
            {
                _logger.Debug($"Reading series: '{tsd.Identifier}'");
                var sourcePoints = _client.Publish.Get(new TimeSeriesDataCorrectedServiceRequest
                {
                    TimeSeriesUniqueId = tsd.UniqueId,
                    GetParts = "PointsOnly"
                }).Points;

                if (sourcePoints.Count == 0)
                    _logger.Information($"No points found for series: '{tsd.Identifier}'");
                else
                    _logger.Information($"Read {sourcePoints.Count} point(s) in the range {sourcePoints.Min(p => p.Timestamp.DateTimeOffset)} to {sourcePoints.Max(p => p.Timestamp.DateTimeOffset)} from series: '{tsd.Identifier}'");

                foreach (var sourcePoint in sourcePoints)
                {
                    int secondsAdded = 0;
                    bool ok = true;

                    TimeSeriesReading newReading = null;

                    do
                    {
                        newReading = new TimeSeriesReading(tsd.Identifier, sourcePoint.Timestamp.DateTimeOffset.AddSeconds(secondsAdded), sourcePoint.Value.Numeric);
                        ok = readings.Add(newReading, false);
                        if (!ok)
                        {
                            if (_settings.AdjustDuplicateTimestamps)
                                secondsAdded++;
                            else
                            {
                                string message = $"Duplicate found.  Winning reading: '{readings.GetReading(newReading.Timestamp)}' Discarding reading: '{newReading}'.";
                                _logger.Warning(message);
                                break;
                            }
                        }
                    } while (!ok);

                    if (secondsAdded > 0)
                    {
                        string message = $"Reading {tsd.Identifier} has been adjusted by {secondsAdded} second(s).";
                        _logger.Warning(message);
                    }
                }
            }
            return readings;
        }
        List<WorkItem> BuildWorkItems()
        {
            List<WorkItem> workItems = new List<WorkItem>();

            foreach (var rawTag in _settings.Tags.Distinct())
            {
                BuildWorkItems_GetTagAndLocationIdentifier(rawTag, out string searchTag, out string aggregateLocationIdentifier);

                List<TimeSeriesDescription> availableTimeseries = _client.GetTimeSeriesDescriptionsByTag(searchTag);
                List<Parameter> filterParameters = BuildWorkItems_GetFilterParameters(availableTimeseries);

                if (_settings.Aggregate)
                    _logger.Debug($"{filterParameters.Count} Filter Parameter(s): {string.Join(", ", filterParameters.Select(p => p.DisplayName))}");
                else
                    _logger.Information($"{filterParameters.Count} Filter Parameter(s): {string.Join(", ", filterParameters.Select(p => p.DisplayName))}");

                foreach (var filterParameter in filterParameters)
                {
                    List<List<string>> filterLabels = BuildWorkItems_GetFilterLabels(filterParameter, availableTimeseries);

                    foreach (var filterLabel in filterLabels)
                    {
                        var filteredTimeSeries = new List<TimeSeriesDescription>();
                        foreach (var timeSeriesDescription in availableTimeseries)
                        {
                            if (IsSeriesIncluded(filterParameter, filterLabel, timeSeriesDescription))
                                filteredTimeSeries.Add(timeSeriesDescription);
                        }

                        if (filteredTimeSeries.Count == 0)
                        {
                            string message = $"Parameter '{filterParameter.DisplayName}', Filter Label(s): '{string.Join(", ", filterLabels.Select(l => string.Join("|", l)))}. No aggregate series found. IGNORING'";
                            if (_settings.Aggregate)
                                _logger.Debug(message);
                            else
                                _logger.Information(message);
                            continue;
                        }
                        else
                        {
                            string message = $"Parameter: '{filterParameter.DisplayName}', Filter Label(s): '{string.Join(", ", filterLabels.Select(l => string.Join("|", l)))}'. Found {filteredTimeSeries.Count} aggregates(s).";
                            if (_settings.Aggregate)
                                _logger.Debug(message);
                            else
                                _logger.Information(message);
                        }

                        string aggregateLabel = null;
                        if (filterLabel.Count == 1)
                            aggregateLabel = _settings.AggrSeriesLabelSingle.Replace("{Label}", filterLabel[0]);
                        else
                            aggregateLabel = _settings.AggrSeriesLabelMulti;

                        workItems.Add(new WorkItem
                        {
                            Parameter = filterParameter,
                            Labels = filterLabel,
                            Tag = searchTag,
                            Publish = _settings.Publish,
                            TargetLocationIdentifier = aggregateLocationIdentifier,
                            TargetLabel = aggregateLabel,
                            GroupedTimeSeriesSourceDescriptions = filteredTimeSeries
                        });
                    }
                }
            }
            return workItems;
        }

        List<List<string>> BuildWorkItems_GetFilterLabels(Parameter filterParameter, List<TimeSeriesDescription> timeSeriesDescriptions)
        {
            List<List<string>> fileterLabels = new List<List<string>>();

            if (_settings.Labels.Count > 0)
                foreach (var label in _settings.Labels.Distinct())
                {
                    if (string.IsNullOrEmpty(label))
                        continue;

                    fileterLabels.Add(label.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Distinct().ToList());
                }
            else
            {
                foreach (var label in timeSeriesDescriptions.Where(t => (t.ParameterId == filterParameter.ParameterId)).Select(tsd => tsd.Label).Distinct())
                    fileterLabels.Add(new List<string> { label });
            }

            return fileterLabels;
        }

        List<Parameter> BuildWorkItems_GetFilterParameters(List<TimeSeriesDescription> timeSeriesDescriptions)
        {
            var filterParameters = new List<Parameter>();
            if (_settings.ParameterIds.Count > 0)
            {
                foreach (var parameterId in _settings.ParameterIds)
                {
                    if (string.IsNullOrWhiteSpace(parameterId))
                        continue;

                    var parameter = _allParameters.FirstOrDefault(p => (parameterId == p.ParameterId) || (parameterId == p.DisplayName) || (parameterId == p.Identifier));
                    if (parameter != null)
                        filterParameters.Add(parameter);
                }
            }
            else
                filterParameters.AddRange(
                    timeSeriesDescriptions
                        .Select(tsd => tsd.ParameterId).Distinct()
                        .Select(pId => _allParameters.First(p => p.ParameterId == pId)));

            return filterParameters;
        }

        void BuildWorkItems_GetTagAndLocationIdentifier(string rawTag, out string searchTag, out string aggregateLocationIdentifier)
        {
            // Extract tag and aggr location identifier
            if (rawTag.Contains("@"))
            {
                var tagLocId = rawTag.Split("@".ToCharArray(), 2);
                searchTag = tagLocId[0];
                aggregateLocationIdentifier = tagLocId[1];
            }
            else
                searchTag = aggregateLocationIdentifier = rawTag;
        }

        bool CreateAggregateTimeSeries(WorkItem workItem, out GetOrCreateTimeSeriesResponse response)
        {
            response = null;

            var location = _allLocationDescriptions.FirstOrDefault(l => l.Identifier == workItem.TargetLocationIdentifier);
            if (location == null)
            {
                _logger.Error($"Unable to find location Identifier '{workItem.TargetLocationIdentifier}'");
                return false;
            }

            var methodCode = (_allMonitoringMethods.FirstOrDefault(m => m.ParameterId == workItem.Parameter.ParameterId)?.MethodCode) ?? _allMonitoringMethods.First(m => m.ParameterId == null).MethodCode;
            var gapTolerance = AquariusClientHelperExt.NoGaps;

            // Publish check
            bool publish = workItem.Publish & location.Publish;
            response = _client.GetOrCreateTimeSeries(location, workItem.Parameter, workItem.TargetLabel, methodCode, publish, gapTolerance);
            if (response.IsNew)
            {
                if (workItem.Publish == publish)
                    _logger.Information($"Created aggregate series: '{response.SeriesIdentifier}' with Publish={publish}");
                else
                    _logger.Warning($"Created target series: '{response.SeriesIdentifier}' with Publish={publish} because its locations is set to Publish={location.Publish}.");
            }

            return true;
        }
        void ConnectClient()
        {
            _logger.Information($"Making a client connection to {_settings.Server} with user {_settings.Username}...");
            _client = AquariusClient.CreateConnectedClient(_settings.Server, _settings.Username, _settings.Password);
            _logger.Debug($"Connection successfull.");
        }

        void Initialise()
        {
            _logger.Information("Initialising...");
            // Use this to lookup the unique id from loc identifier
            _logger.Debug("Fetching all location descriptions.");
            _allLocationDescriptions = _client.GetLocationDescriptions();
            _logger.Debug("Fetching all parameters.");
            _allParameters = _client.GetParameters();
            _logger.Debug("Fetching all monitoring methods.");
            _allMonitoringMethods = _client.GetMonitoringMethods();
        }
        bool IsSeriesIncluded(Parameter filterParameter, List<string> filterLabel, TimeSeriesDescription tsd)
        {
            if (tsd.TimeSeriesType != "Reflected" || tsd.Comment != "Time-Series created by AQUARIUS Samples Connector")
                return false;

            if (filterLabel.Any() && !filterLabel.Contains(tsd.Label))
                return false;

            if ((filterParameter != null) && (filterParameter.ParameterId != tsd.ParameterId))
                return false;

            return true;
        }
        void ProcessWorkItem(WorkItem workItem)
        {
            _logger.Information($"{new string('-', 80)}");

            if (!_settings.Aggregate)
            {
                _logger.Information(workItem.ToFullString());
                return;
            }

            if (!CreateAggregateTimeSeries(workItem, out GetOrCreateTimeSeriesResponse aggregateTsDetails))
                return;

            TimeSeriesReadings readings = AggregateReadings(workItem);
            if (readings.IsEmpty)
            {
                _logger.Information("No points to export.");
                return;
            }
            WriteSeries(aggregateTsDetails, readings);
        }
        void ValidateAndFix()
        {
            List<Parameter> parameters = new List<Parameter>();
            List<string> invalidParameters = new List<string>();
            List<string> duplicateParameters = new List<string>();

            foreach (var parameterId in _settings.ParameterIds)
            {
                var parameter = _allParameters.FirstOrDefault(p => (parameterId == p.ParameterId) || (parameterId == p.DisplayName));
                if (parameter == null)
                    invalidParameters.Add(parameterId);
                else if (!parameters.Contains(parameter))
                    parameters.Add(parameter);
                else if (!duplicateParameters.Contains(parameterId))
                    duplicateParameters.Add(parameterId);

            }
            if (invalidParameters.Count > 0)
                _logger.Error($"The following setting parameters: '{string.Join(", ", invalidParameters)}' cannot be found and will be ignored.");
            if (duplicateParameters.Count > 0)
                _logger.Warning($"Duplicate setting paramaters found: '{string.Join(", ", duplicateParameters)}' and will be ignored.");

            _settings.ParameterIds = parameters.Select(p => p.ParameterId).ToList();
        }
        void WriteSeries(GetOrCreateTimeSeriesResponse aggregateTsDetails, TimeSeriesReadings readings)
        {
            var timeseriesPoints = readings.ToList().Select(r => new TimeSeriesPoint() { Type = PointType.Point, Time = Instant.FromDateTimeOffset(r.Timestamp), Value = r.Value, GradeCode = r.Grade }).ToList();

            var writeResponse = _client.Acquisition.Post(new PostReflectedTimeSeries
            {
                UniqueId = aggregateTsDetails.UniqueId,
                Points = timeseriesPoints,
                TimeRange = new Interval(Instant.MinValue, Instant.MaxValue)
            });

            _logger.Information($"{readings.Count} point(s) written to series: {aggregateTsDetails.SeriesIdentifier}");
        }
    }
}



