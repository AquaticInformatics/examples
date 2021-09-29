using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aquarius.Samples.Client.ServiceModel;
using Microsoft.VisualBasic.FileIO;
using ServiceStack.Logging;

namespace NWFWMDLabFileImporter
{
    public class LabFileLoader
    {
        private ILog Log { get; }
        private Context Context { get; }
        private Dictionary<string, AnalysisMethod> AnalysisMethods { get; }
        private Dictionary<string, SamplingLocation> SamplingLocations { get; }
        private Dictionary<string, ObservedProperty> ObservedProperties { get; }
        private Dictionary<string, Unit> Units { get; }
        private TimeSpan UtcOffset { get; }

        public LabFileLoader(ILog log, Context context, List<AnalysisMethod> analysisMethods,
            List<SamplingLocation> samplingLocations, List<ObservedProperty> observedProperties, List<Unit> units)
        {
            Log = log;
            Context = context;

            AnalysisMethods = analysisMethods
                .ToDictionary(
                    analysisMethod => analysisMethod.MethodId,
                    analysisMethod => analysisMethod);

            SamplingLocations = samplingLocations
                .ToDictionary(
                    samplingLocation => samplingLocation.CustomId,
                    samplingLocation => samplingLocation);

            ObservedProperties = observedProperties
                .ToDictionary(
                    GetParamCode,
                    observedProperty => observedProperty,
                    StringComparer.InvariantCultureIgnoreCase);

            Units = units
                .ToDictionary(
                    unit => unit.CustomId,
                    unit => unit,
                    StringComparer.InvariantCultureIgnoreCase);

            UtcOffset = Context.UtcOffset.ToTimeSpan();
        }

        private string GetParamCode(ObservedProperty observedProperty)
        {
            return observedProperty.CustomId.Split('-')[0].Trim();
        }

        private string Path { get; set; }
        private long LineNumber { get; set; }
        private Dictionary<string,int> Columns { get; set; }
        private string[] Fields { get; set; }

        public IEnumerable<ObservationV2> Load(string path)
        {
            if (!File.Exists(path))
                throw new ExpectedException($"The file '{path}' does not exist.");

            Path = path;

            using (var reader = CreateReader(path))
            {
                LineNumber = 0;

                while (!reader.EndOfData)
                {
                    Fields = reader.ReadFields();
                    LineNumber = reader.LineNumber;

                    if (Fields == null)
                        continue;

                    if (Columns == null)
                    {
                        ParseHeader();
                        continue;
                    }

                    var observation = ParseRow();

                    if (observation != null)
                        yield return observation;
                }
            }
        }

        private TextFieldParser CreateReader(string path)
        {
            if (!File.Exists(path))
                throw new ExpectedException($"File '{path}' not found.");

            var firstLine = File.ReadAllLines(path).FirstOrDefault() ?? string.Empty;

            var delimiter = firstLine.StartsWith("request_id|", StringComparison.InvariantCultureIgnoreCase)
                ? "|"
                : firstLine.StartsWith("request_id\t", StringComparison.InvariantCultureIgnoreCase)
                    ? "\t"
                    : null;

            return new TextFieldParser(path)
            {
                Delimiters = new[] { delimiter },
                HasFieldsEnclosedInQuotes = true,
                TextFieldType = FieldType.Delimited,
                TrimWhiteSpace = true
            };
        }

        private void ParseHeader()
        {
            Columns = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);

            for (var i = 0; i < Fields.Length; ++i)
            {
                Columns.Add(Fields[i], i);
            }
        }

        private ObservationV2 ParseRow()
        {
            var sampleId = GetColumn("SAMPLE_ID");
            var result = GetColumn("RESULT");
            var valueQualifier = GetColumn("VALUE_QUAL");
            var labMinimumDetectionLevel = GetColumn("MDL");
            var labMinimumReportingLevel = GetColumn("PQL");

            var observedDateTime = ParseDateTimeOffset(GetColumn("SAMPLE_DATE"), GetColumn("SAMPLE_TIME"));
            var analyzedDateTime = ParseDateTimeOffset(GetColumn("ANALYSIS_DATE"), GetColumn("ANALYSIS_TIME"));

            if (observedDateTime < Context.StartTime)
                return null;

            if (observedDateTime > Context.EndTime)
                return null;

            var isNonDetect = valueQualifier?.Contains("U") ?? false;

            var (propertyId, unitId) = LookupObservedProperty(GetColumn("PARAM_CODE"), GetColumn("PARAM_NAME"), GetColumn("UOM"));

            return new ObservationV2
            {
                LocationID = LookupLocation(GetColumn("FIELD_ID")),
                ObservedPropertyID = propertyId,
                ObservedDateTime = $"{observedDateTime:yyyy-MM-ddTHH:mm:ss.fffzzz}",
                AnalyzedDateTime = $"{analyzedDateTime:yyyy-MM-ddTHH:mm:ss.fffzzz}",
                DataClassification = $"{DataClassificationType.LAB}",
                ResultValue = isNonDetect ? string.Empty : result,
                ResultUnit = unitId,
                ResultStatus = Context.LabResultStatus,
                ResultGrade = Context.ResultGrade,
                Medium = Context.DefaultMedium,
                LabFromLaboratory = Context.DefaultLaboratory,
                ActivityName = sampleId,
                LabMDL = labMinimumDetectionLevel,
                LabMRL = labMinimumReportingLevel,
                LabDetectionCondition = isNonDetect ? Context.NonDetectCondition : string.Empty,
                LabSpecimenName = sampleId,
                LabAnalysisMethod = LookupAnalysisMethod(GetColumn("ANALYSIS_Method")),
                LabComment = GetColumn("LAB_COMMENTS"),
                LabQualityFlag = valueQualifier,
                EARequestID = GetColumn("REQUEST_ID"),
                EASampler = GetColumn("SAMPLER"),
                EACollectionAgency = Context.DefaultCollectionAgency,
            };
        }

        private string LookupLocation(string fieldId)
        {
            if (Context.EquipmentBlankPatterns.Any(text => fieldId.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0))
                return Context.EquipmentBlankLocation;

            var leadingNumberText = fieldId.Split('-')[0];
            var locationId = leadingNumberText;

            if (Context.LocationAliases.TryGetValue(leadingNumberText, out var aliasedLocation))
            {
                locationId = aliasedLocation;
            }

            if (SamplingLocations.TryGetValue(locationId, out var samplingLocation) ||
                (int.TryParse(leadingNumberText, out var leadingNumber) &&
                 SamplingLocations.TryGetValue($"{leadingNumber:D6}", out samplingLocation)))
            {
                return samplingLocation.CustomId;
            }

            LogValidationWarning($"'{leadingNumberText}' is not a known sampling location.{BestGuess(locationId, SamplingLocations.Values, item => item.CustomId)}");

            return fieldId;
        }

        private (string PropertyId, string UnitId) LookupObservedProperty(string paramCode, string paramName, string unitOfMeasure)
        {
            var propertyId = paramCode;
            var unitId = unitOfMeasure;

            if (Context.ObservedPropertyAliases.TryGetValue(paramCode, out var aliasedProperty))
            {
                propertyId = aliasedProperty;
            }

            if (!ObservedProperties.TryGetValue(propertyId, out var observedProperty))
                LogValidationWarning($"'{paramCode} ({paramName})' is not a known observed property or alias.{BestGuess(propertyId, ObservedProperties.Values, item => item.CustomId)}");

            propertyId = observedProperty?.CustomId ?? $"{paramCode} - {paramName}";

            if (Context.UnitAliases.TryGetValue(unitOfMeasure, out var aliasedUnit))
                unitId = aliasedUnit;

            if (!Units.TryGetValue(unitId, out var unit))
                LogValidationWarning($"'{unitOfMeasure}' is not a known unit or alias.{BestGuess(unitId, Units.Values, item => item.CustomId)}");

            return (propertyId, unit?.CustomId ?? unitOfMeasure);
        }

        private string LookupAnalysisMethod(string labAnalysisMethod)
        {
            var originalMethodCode = labAnalysisMethod;

            if (Context.AnalysisMethodAliases.TryGetValue(labAnalysisMethod, out var aliasedMethod))
            {
                labAnalysisMethod = aliasedMethod;
            }

            if (!AnalysisMethods.TryGetValue(labAnalysisMethod, out var analysisMethod))
            {
                LogValidationWarning($"'{originalMethodCode}' is not a known analysis method or alias.{BestGuess(labAnalysisMethod, AnalysisMethods.Values, item => item.MethodId)}");
                return originalMethodCode;
            }

            return $"{analysisMethod.MethodId};{analysisMethod.Name};{analysisMethod.Context}";
        }

        private string BestGuess<TItem>(string target, IEnumerable<TItem> items, Func<TItem,string> selector)
        {
            var lev = new Fastenshtein.Levenshtein(target);

            var orderedItems = items
                .Select(item => (Item: item, Text: selector(item), Distance: lev.DistanceFrom(selector(item))))
                .OrderBy(tuple => tuple.Distance)
                .ToList();

            if (orderedItems.Any())
            {
                var best = orderedItems.First();

                if (best.Distance < 8)
                {
                    var guesses = orderedItems
                        .Where(tuple => tuple.Distance <= best.Distance + 1)
                        .ToList();

                    if (guesses.Count <= 4)
                        return $" Did you mean '{string.Join("' or '", guesses.Select(tuple => tuple.Text))}'";
                }
            }

            return string.Empty;
        }

        private DateTimeOffset? ParseDateTimeOffset(string dateText, string timeText)
        {
            if (DateTime.TryParseExact(dateText, DateFormats, null, DateTimeStyles.NoCurrentDateDefault, out var date)
                && TimeSpan.TryParse(timeText, null, out var timeSpan))
            {
                return new DateTimeOffset(date.Year, date.Month, date.Day, timeSpan.Hours, timeSpan.Minutes, 0, UtcOffset);
            }

            if (!string.IsNullOrEmpty(dateText) || !string.IsNullOrEmpty(timeText))
                LogValidationWarning($"'{dateText} {timeText}' is and invalid date and time.");

            return null;
        }

        private static readonly string[] DateFormats =
        {
            "d-MMM-yy",
            "d-MMM-yyyy"
        };

        private string GetColumn(string columnName)
        {
            if (!Columns.TryGetValue(columnName, out var columnIndex))
                throw new ExpectedException($"{Path} (line {LineNumber}): Column {columnName} not found");

            if (columnIndex < 0 || columnIndex >= Fields.Length)
                return null;

            return Fields[columnIndex];
        }

        private static HashSet<string> KnownWarningCategories { get; } = new HashSet<string>();

        private void LogValidationWarning(string message)
        {
            var contextMessage = $"{Path} (line {LineNumber}): {message}";

            if (Context.StopOnFirstError)
                throw new ExpectedException(contextMessage);

            if (KnownWarningCategories.Contains(message))
                return;

            KnownWarningCategories.Add(message);
            Log.Warn(contextMessage);
        }
    }
}
