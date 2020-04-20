using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using SondeFileImporter.Config;
using SondeFileImporter.FileManagement;

namespace SondeFileImporter.Transform
{
    public class SondeFileConverter
    {
        private static readonly string SamplesEmbeddedFilePath =
            MethodBase.GetCurrentMethod().DeclaringType?.Namespace + ".SamplesFileTemplate.csv";

        private readonly Context _context;
        private readonly SondeFileManager _fileMan;

        public SondeFileConverter(Context context)
        {
            _context = context;
            _fileMan = new SondeFileManager(_context);
        }

        public FileInfo ToSamplesObservationFile(FileInfo sondeFileInfo)
        {
            var targetTable = FileHelper.ParseDataTableFromEmbeddedCsvFile(SamplesEmbeddedFilePath);
            var sourceTable = FileHelper.ParseDataTableFromFileInfo(sondeFileInfo);

            AddConvertedSondeDataRowsToSamplesDataTable(sourceTable, targetTable);

            var samplesFile = new FileInfo(_fileMan.GetConvertedSamplesFilePath(sondeFileInfo));

            FileHelper.ForceWriteDataTableAsCsvFile(samplesFile, targetTable);

            return samplesFile;
        }

        private void AddConvertedSondeDataRowsToSamplesDataTable(DataTable sourceTable, DataTable targetTable)
        {
            var existingPropertyIdMappings = GetPropertyIdMappingUsedInFile(sourceTable.ColumnNameList());

            foreach (DataRow row in sourceTable.Rows)
            {
                foreach (var existingPropertyIdMapping in existingPropertyIdMappings)
                {
                    var newRow = targetTable.NewRow();
                    PopulateFieldsByColumns(row, newRow);
                    PopulatePropertyValuesWithUnits(existingPropertyIdMapping, row, newRow);

                    targetTable.Rows.Add(newRow);
                }
            }
        }

        private Dictionary<string, string> GetPropertyIdMappingUsedInFile(IReadOnlyList<string> columns)
        {
            return _context.HeaderPropertyIdMap
                .Where(kvp => columns.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private void PopulateFieldsByColumns(DataRow row, DataRow newRow)
        {
            foreach (var kvp in _context.HeaderMap)
            {
                var sondeHeader = kvp.Key;
                var samplesHeader = kvp.Value;
                string sondeValue;

                if (sondeHeader.Contains(","))
                {
                    var columnNames = sondeHeader.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    sondeValue = row.GetCombinedValues(columnNames);
                }
                else
                {
                    sondeValue = GetSondeValueFromRowOrMapping(row, sondeHeader);
                }

                newRow[samplesHeader] = sondeValue;
            }
        }

        private static string GetSondeValueFromRowOrMapping(DataRow row, string sondeHeader)
        {
            return sondeHeader.StartsWith(@"""")
                ? sondeHeader.Replace(@"""", "")
                : row.GetStringValue(sondeHeader);
        }

        private void PopulatePropertyValuesWithUnits(KeyValuePair<string, string> kvp,
            DataRow row, DataRow newRow)
        {
            var sondeHeader = kvp.Key;
            var propertyId = kvp.Value;
            var resultValue = row.GetStringValue(sondeHeader);
            var resultUnit = GetResultUnitByPropertyId(propertyId);

            newRow[ColumnConstants.PropertyId] = propertyId;
            newRow[ColumnConstants.ResultValue] = resultValue;
            newRow[ColumnConstants.ResultUnit] = resultUnit;
        }

        private string GetResultUnitByPropertyId(string propertyId)
        {
            if (!_context.PropertyIdUnitMap.TryGetValue(propertyId, out string unit))
            {
                throw new ConfigException($"Missing a unit mapping for property ID '{propertyId}'" +
                                          $" under '{ConfigNames.PropertyIdUnitMappingSection}' in the config file.");
            }

            return unit;
        }
    }
}
