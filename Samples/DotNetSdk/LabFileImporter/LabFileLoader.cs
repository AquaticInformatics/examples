using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Aquarius.Samples.Client.ServiceModel;
using ExcelDataReader;
using ExcelDataReader.Exceptions;
using log4net;

namespace LabFileImporter
{
    public class LabFileLoader
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        private ImportFormat Format { get; set; }

        private class ImportFormat
        {
            public int PropertyStartColumn { get; set; }
            public Dictionary<string, int> CommonColumns { get; set; }
        }

        private const string SiteCode = "SiteCode";
        private const string DateSampleCollected = "DateSampleCollected";
        private const string TimeSampleCollected = "TimeSampleCollected";
        private const string SampleMatrix = "SampleMatrix";
        private const string QcType = "QCType";
        private const string SiteAlias = "SiteAlias";

        private static readonly ImportFormat IndividualFileFormat = new ImportFormat
        {
            PropertyStartColumn = 'M' - 'A',
            CommonColumns = new (string ColumnName, char ColumnIndex)[]
                {
                    (SiteCode, 'K'),
                    (DateSampleCollected, 'H'),
                    (TimeSampleCollected, 'L'),
                    (SampleMatrix, 'J'),
                    (QcType, 'G'),
                }
                .ToDictionary(
                    column => column.ColumnName,
                    column => column.ColumnIndex - 'A')
        };

        private static readonly ImportFormat BulkFormat = new ImportFormat
        {
            PropertyStartColumn = 'W' - 'A',
            CommonColumns = new (string ColumnName, char ColumnIndex)[]
                {
                    (SiteCode, 'U'),
                    (SiteAlias, 'C'),
                    (DateSampleCollected, 'J'),
                    (TimeSampleCollected, 'V'),
                    (SampleMatrix, 'T'),
                    (QcType, 'G'),
                }
                .ToDictionary(
                    column => column.ColumnName,
                    column => column.ColumnIndex - 'A')
        };

        private class Property
        {
            public string PropertyId { get; set; }
            public string Unit { get; set; }
            public string Method { get; set; }
        }

        private List<Property> Properties { get; } = new List<Property>();

        private string Path { get; set; }
        private int RowNumber { get; set; }
        private TimeSpan UtcOffset { get; set; }

        public IEnumerable<ObservationV2> Load(string path)
        {
            if (!File.Exists(path))
                throw new ExpectedException($"The file '{path}' does not exist.");

            Path = path;

            UtcOffset = Context.UtcOffset.ToTimeSpan();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = CreateReaderFromStream(path, stream))
            {
                RowNumber = 0;

                var headers = new List<List<string>>();

                while (reader.Read())
                {
                    ++RowNumber;

                    if (Format != null)
                    {
                        foreach (var observation in LoadRow(reader).Where(obs => obs != null))
                        {
                            yield return observation;
                        }

                        continue;
                    }

                    var columns = Enumerable
                        .Range(0, reader.FieldCount)
                        .Select(i => reader.IsDBNull(i) ? string.Empty : reader.GetValue(i).ToString().Trim())
                        .ToList();

                    headers.Add(columns);

                    switch (RowNumber)
                    {
                        case 6:
                            if (!columns[0].Equals(Context.BulkImportIndicator, StringComparison.InvariantCultureIgnoreCase))
                            {
                                // We are done, this is the single file format
                                CreateProperties(IndividualFileFormat, headers);

                                foreach (var observation in LoadRow(reader).Where(obs => obs != null))
                                {
                                    yield return observation;
                                }
                            }

                            break;

                        case 7:
                            CreateProperties(BulkFormat, headers);
                            break;
                    }
                }
            }
        }
        private IExcelDataReader CreateReaderFromStream(string path, Stream stream)
        {
            try
            {
                return ExcelReaderFactory.CreateReader(stream);
            }
            catch (HeaderException exception)
            {
                throw new ExpectedException($"Can't read '{path}' as an Excel file: {exception.Message}");
            }
        }

        private void CreateProperties(ImportFormat format, List<List<string>> headers)
        {
            if (headers.Count < 5)
                throw new ExpectedException($"Unknown file format. Only {headers.Count} header rows encountered.");

            var columnCount = headers.First().Count;

            for (var i = format.PropertyStartColumn; i < columnCount; ++i)
            {
                var propertyId = headers[0][i];
                var unitId = i < headers[2].Count ? headers[2][i] : null;
                var method = i < headers[4].Count ? headers[4][i] : null;

                if (string.IsNullOrWhiteSpace(propertyId))
                {
                    LogErrorOrThrow($"{RowAndCellId(1, i)}: No property Id found");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(unitId))
                {
                    Log.Warn($"{RowAndCellId(3,i)}: No unit found for '{propertyId}'. Ignoring entire {ColumnId(i)} column.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(method))
                {
                    Log.Warn($"{RowAndCellId(5, i)}: No method found for '{propertyId}'. Ignoring entire {ColumnId(i)} column.");
                    continue;
                }

                Properties.Add(new Property
                {
                    PropertyId = propertyId,
                    Unit = unitId,
                    Method = method,
                });
            }

            Format = format;
        }




        private IEnumerable<ObservationV2> LoadRow(IExcelDataReader reader)
        {
            return Properties
                .Select((property, i) => LoadResult(reader, Format, property, i));
        }

        private ObservationV2 LoadResult(IExcelDataReader reader, ImportFormat format, Property property, int propertyIndex)
        {
            var columnIndex = format.PropertyStartColumn + propertyIndex;

            if (reader.IsDBNull(columnIndex))
                return null;

            var dateTimeOffset = GetNullableDateTimeOffset(reader, format.CommonColumns[DateSampleCollected], format.CommonColumns[TimeSampleCollected]);

            if (!dateTimeOffset.HasValue)
                return null;

            if (dateTimeOffset < Context.StartTime)
                return null;

            if (dateTimeOffset > Context.EndTime)
                return null;

            var siteCode = GetNullableString(reader, format.CommonColumns[SiteCode]);

            if (string.IsNullOrEmpty(siteCode) && format.CommonColumns.TryGetValue(SiteAlias, out var siteAliasIndex))
            {
                siteCode = GetNullableString(reader, siteAliasIndex);
            }

            if (!string.IsNullOrEmpty(siteCode) && Context.LocationAliases.TryGetValue(siteCode, out var aliasedLocationId))
            {
                siteCode = aliasedLocationId;
            }

            if (string.IsNullOrWhiteSpace(siteCode))
                return null;

            var isFieldResult = property.Method?.StartsWith(Context.FieldResultPrefix, StringComparison.InvariantCultureIgnoreCase) ?? false;

            var sampleMatrix = GetNullableString(reader, format.CommonColumns[SampleMatrix]);
            var (resultValue, resultGrade, mrl) = GetResult(reader.GetValue(columnIndex));

            var observedProperty = property.PropertyId;
            var unitId = property.Unit;

            var alias = new Context.AliasedProperty
            {
                PropertyId = observedProperty,
                UnitId = unitId
            };

            if (Context.ObservedPropertyAliases.TryGetValue(alias.Key, out var aliasedProperty))
            {
                observedProperty = aliasedProperty.PropertyId;
                unitId = aliasedProperty.UnitId;
            }

            var dateTimeText = $"{dateTimeOffset:yyyy-MM-ddTHH:mm:ss.fffzzz}";

            return new ObservationV2
            {
                LocationID = siteCode,
                ObservedPropertyID = observedProperty,
                ObservedDateTime = dateTimeText,
                AnalyzedDateTime = dateTimeText,
                DataClassification = isFieldResult ? $"{DataClassificationType.FIELD_RESULT}" : $"{DataClassificationType.LAB}",
                ResultValue = resultValue,
                ResultUnit = unitId,
                ResultStatus = isFieldResult ? Context.FieldResultStatus : Context.LabResultStatus,
                ResultGrade = resultGrade,
                Medium = sampleMatrix,
                LabSampleID = isFieldResult ? null : $"{dateTimeOffset:ddMMMyyyy}-{siteCode}",
                LabSpecimenName = isFieldResult ? null : Context.LabSpecimenName,
                LabAnalysisMethod = isFieldResult ? null : property.Method,
                LabDetectionCondition = isFieldResult ? null : string.IsNullOrEmpty(mrl) ? null : Context.NonDetectCondition,
                LabMRL = isFieldResult ? null : mrl,
                LabFromLaboratory = isFieldResult ? null : Context.DefaultLaboratory,
                QCType = isFieldResult ? null : GetNullableString(reader, format.CommonColumns[QcType]),
            };
        }

        private (string ResultValue, string ResultGrade, string MRL) GetResult(object value)
        {
            if (value is double numericResult)
                return ($"{numericResult}", Context.ResultGrade, null);

            var text = value.ToString();

            var match = EstimatedRegex.Match(text);

            if (match.Success)
                return (match.Groups["number"].Value, Context.EstimatedGrade, null);

            match = NonDetectRegex.Match(text);

            if (match.Success)
                return (null, Context.ResultGrade, match.Groups["number"].Value);

            return (text, Context.ResultGrade, null);
        }

        private static readonly Regex EstimatedRegex = new Regex(@"^\s*(?<number>[0-9.+\-]+)\s+est\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex NonDetectRegex = new Regex(@"^\s*[<>]\s*(?<number>[0-9.+\-]+)\s*$", RegexOptions.IgnoreCase);

        private string GetNullableString(IExcelDataReader reader, int columnIndex)
        {
            if (reader.IsDBNull(columnIndex))
                return null;

            return reader.GetValue(columnIndex).ToString().Trim();
        }

        private DateTimeOffset? GetNullableDateTimeOffset(IExcelDataReader reader, int dateColumnIndex, int timeColumnIndex)
        {
            if (reader.IsDBNull(dateColumnIndex) || reader.IsDBNull(timeColumnIndex))
                return null;

            var dateColumnType = reader.GetFieldType(dateColumnIndex);

            if (dateColumnType != typeof(DateTime))
            {
                LogErrorOrThrow($"{CellId(dateColumnIndex)}: '{reader.GetValue(dateColumnIndex)}' is not a valid Date.");

                return null;
            }

            var date = reader.GetDateTime(dateColumnIndex);

            var timeColumnType = reader.GetFieldType(timeColumnIndex);

            if (timeColumnType != typeof(DateTime))
            {
                LogErrorOrThrow($"{CellId(timeColumnIndex)}: '{reader.GetValue(timeColumnIndex)}' is not a valid Time.");

                return null;
            }

            var time = reader.GetDateTime(timeColumnIndex);

            var dateTime = date.Date + time.TimeOfDay;

            return new DateTimeOffset(dateTime, UtcOffset);
        }

        private void LogErrorOrThrow(string message)
        {
            if (Context.StopOnFirstError)
                throw new ExpectedException(message);

            Log.Warn($"Skipping: {message}");
        }

        private string CellId(int columnIndex)
        {
            return RowAndCellId(RowNumber, columnIndex);
        }

        private string RowAndCellId(int rowNumber, int columnIndex)
        {
            return $"'{System.IO.Path.GetFileName(Path)}':{ColumnId(columnIndex)}{rowNumber}";
        }

        private string ColumnId(int columnIndex)
        {
            const int letters = 'Z' - 'A' + 1;
            var extraLetters = (char)(columnIndex / letters);
            var offset = (char)(columnIndex % letters);

            return extraLetters > 0
                ? $"{(char)('A' + extraLetters)}{(char)('A' + offset)}"
                : $"{(char)('A' + offset)}";
        }
    }
}
