using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ExcelDataReader;
using ExcelDataReader.Exceptions;
using Humanizer;
using log4net;

namespace ExcelCsvExtractor
{
    public class Splitter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const string SheetNamePattern = "SheetName";
        public const string ExcelPathPattern = "ExcelPath";

        public Context Context { get; set; }

        public void Run()
        {
            Log.Info($"Loading '{Context.ExcelPath}' ...");

            try
            {
                using (var stream = new FileStream(Context.ExcelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var dataSetConfig = new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = tableReader =>
                            new ExcelDataTableConfiguration
                            {
                                UseHeaderRow = false, // We are just gonna dump each row of each sheet. We don't know where the headers, if any, actually live within a sheet
                            }
                    };

                    var dataSet = reader.AsDataSet(dataSetConfig);

                    Split(dataSet);
                }

            }
            catch (HeaderException exception)
            {
                throw new ExpectedException($"'{Context.ExcelPath}' is not a valid Excel file: {exception.Message}");
            }
        }

        private void Split(DataSet dataSet)
        {
            var tables = dataSet
                .Tables
                .Cast<DataTable>()
                .ToList();

            Log.Info($"{"sheet".ToQuantity(tables.Count)} loaded: {string.Join(", ", tables.Select(t => t.TableName))}");

            for (var i = 0; i < tables.Count; ++i)
            {
                var table = tables[i];

                if (!ShouldSplit(table, i))
                    continue;

                ExtractCsv(table);
            }
        }

        private bool ShouldSplit(DataTable table, int tableIndex)
        {
            if (!Context.Sheets.Any())
                return true;

            return Context.Sheets.Any(name =>
                    name.Equals(table.TableName, StringComparison.InvariantCultureIgnoreCase)
                    || int.TryParse(name, out var index) && index == tableIndex + 1);
        }

        private void ExtractCsv(DataTable table)
        {
            var patterns = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { SheetNamePattern, table.TableName },
                { ExcelPathPattern, Path.ChangeExtension(Context.ExcelPath, null) }
            };

            var outputPath = string.IsNullOrWhiteSpace(Context.OutputPath)
                ? Path.ChangeExtension(Context.ExcelPath, $".{table.TableName}.csv")
                : PatternRegex.Replace(Context.OutputPath, match =>
                {
                    var patternName = match.Groups["pattern"].Value;

                    if (patterns.TryGetValue(patternName, out var replacement))
                        return replacement;

                    throw new ExpectedException($"'{patternName}' is not a known pattern. Must be one of {string.Join(", ", patterns.Keys.Select(p => $"{{{p}}}"))}");
                });

            if (!Context.Overwrite && File.Exists(outputPath))
            {
                Log.Warn($"Skipping existing file '{outputPath}' ...");
                return;
            }

            Log.Info($"Saving '{outputPath}' ...");

            using (var writer = File.CreateText(outputPath))
            {
                foreach (var row in table.Rows.Cast<DataRow>())
                {
                    var columns = row
                        .ItemArray
                        .Select((c, i) => row.IsNull(i) ? string.Empty : CsvEscapedColumn(FormatCell(c)))
                        .ToList();

                    if (Context.TrimEmptyColumns)
                    {
                        for (var i = columns.Count - 1; i > 0; --i)
                        {
                            if (!string.IsNullOrWhiteSpace(columns[i]))
                                break;

                            columns.RemoveAt(i);
                        }
                    }

                    writer.WriteLine(string.Join(", ", columns));
                }
            }
        }

        private static readonly Regex PatternRegex = new Regex(@"\{(?<pattern>\w+)\}");

        private string FormatCell(object cell)
        {
            if (cell is DateTime dateTime)
                return dateTime.ToString(Context.DateTimeFormat ?? "O");

            return $"{cell}";
        }

        private static string CsvEscapedColumn(string text)
        {
            return !CharactersRequiringEscaping.Any(text.Contains)
                ? text
                : $"\"{text.Replace("\"", "\"\"")}\"";
        }

        private static readonly char[] CharactersRequiringEscaping =
        {
            ',',
            '"',
            '\n',
            '\r'
        };
    }
}
