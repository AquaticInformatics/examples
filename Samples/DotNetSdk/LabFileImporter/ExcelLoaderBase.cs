using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using ExcelDataReader;
using ExcelDataReader.Exceptions;

namespace LabFileImporter
{
    public abstract class ExcelLoaderBase
    {
        protected string Path { get; set; }
        protected string SheetName { get; set; }
        protected int SkipRows { get; set; }
        protected bool UseHeaderRow { get; set; } = true;

        protected IEnumerable<TEntity> LoadAll<TEntity>(string path, string sheetName, Func<DataRow,TEntity> factoryFunc) where TEntity : class
        {
            Path = path;
            SheetName = sheetName;

            using (var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = CreateReaderFromStream(stream))
            {
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    FilterSheet = (tableReader, sheetIndex) => string.IsNullOrWhiteSpace(SheetName) || tableReader.Name == SheetName,
                    ConfigureDataTable = tableReader => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = UseHeaderRow,
                        
                        ReadHeaderRow = rowReader =>
                        {
                            for (var skipRows = SkipRows; skipRows > 0; --skipRows)
                            {
                                rowReader.Read();
                            }
                        }
                    }
                });

                if (!string.IsNullOrWhiteSpace(SheetName) && !dataSet.Tables.Contains(SheetName))
                    throw new ExpectedException($"'{Path}' does not contain a worksheet named '{SheetName}'");

                foreach (var table in dataSet.Tables.Cast<DataTable>())
                {
                    if (!string.IsNullOrWhiteSpace(SheetName) && table.TableName != SheetName)
                        continue;

                    foreach (var row in table.Rows.Cast<DataRow>())
                    {
                        yield return factoryFunc(row);
                    }
                }
            }
        }

        private IExcelDataReader CreateReaderFromStream(Stream stream)
        {
            try
            {
                return ExcelReaderFactory.CreateReader(stream);
            }
            catch (HeaderException exception)
            {
                throw new ExpectedException($"Can't read '{Path}' as an Excel file: {exception.Message}");
            }
        }

        protected string GetString(DataRow row, string columnName)
        {
            var index = GetColumnIndex(row, columnName);

            if (index >= 0 && index < row.Table.Columns.Count)
                return Convert.ToString(row[index]).Trim();

            throw new ExpectedException($"{GetColumnId(row, columnName)} not found in '{Path}'.");
        }

        protected string GetColumnId(DataRow row, string columnName)
        {
            return $"{GetRowId(row)} Column '{columnName}'";
        }

        protected string GetRowId(DataRow row)
        {
            var rowIndex = row.Table.Rows.IndexOf(row);

            rowIndex += SkipRows + (UseHeaderRow ? 1 : 0);

            return $"'{Path}'({SheetName}): Row {rowIndex + 1}";
        }

        private int GetColumnIndex(DataRow row, string columnName)
        {
            if (row.Table.Columns.Contains(columnName))
                return row.Table.Columns.IndexOf(columnName);

            if (int.TryParse(columnName, out var index))
                return index;

            index = 0;
            foreach (var c in columnName.ToUpperInvariant())
            {
                index += c - 'A';
            }

            return index;
        }

        protected string GetNullableString(DataRow row, string columnName)
        {
            var text = GetString(row, columnName);

            return string.IsNullOrEmpty(text)
                ? null
                : text;
        }

        protected int GetInt(DataRow row, string columnName)
        {
            return GetNullableInt(row, columnName) ?? throw new InvalidOperationException();
        }

        protected int? GetNullableInt(DataRow row, string columnName)
        {
            var text = GetString(row, columnName);

            if (string.IsNullOrEmpty(text))
                return null;

            if (int.TryParse(text, out var value))
                return value;

            throw new ExpectedException($"{GetColumnId(row, columnName)} has an invalid integer value '{text}'");
        }

        protected double GetDouble(DataRow row, string columnName)
        {
            return GetNullableDouble(row, columnName) ?? throw new InvalidOperationException();
        }

        protected double? GetNullableDouble(DataRow row, string columnName)
        {
            var text = GetString(row, columnName);

            if (string.IsNullOrEmpty(text))
                return null;

            if (double.TryParse(text, out var value))
                return value;

            throw new ExpectedException($"{GetColumnId(row, columnName)} has an invalid double value '{text}'");
        }

        protected decimal GetDecimal(DataRow row, string columnName)
        {
            return GetNullableDecimal(row, columnName) ?? throw new InvalidOperationException();
        }

        protected decimal? GetNullableDecimal(DataRow row, string columnName)
        {
            var text = GetString(row, columnName);

            if (string.IsNullOrEmpty(text))
                return null;

            if (decimal.TryParse(text, out var value))
                return value;

            throw new ExpectedException($"{GetColumnId(row, columnName)} has an invalid decimal value '{text}'");
        }

        protected TEnum GetEnum<TEnum>(DataRow row, string columnName) where TEnum : struct
        {
            return GetNullableEnum<TEnum>(row, columnName) ?? throw new InvalidOperationException();
        }

        protected TEnum? GetNullableEnum<TEnum>(DataRow row, string columnName) where TEnum : struct
        {
            var text = GetString(row, columnName);

            if (string.IsNullOrEmpty(text))
                return null;

            if (Enum.TryParse<TEnum>(text, true, out var value))
                return value;

            throw new ExpectedException($"{GetColumnId(row, columnName)} has an invalid {typeof(TEnum).Name} value '{text}'. Must be one of {string.Join(", ", Enum.GetNames(typeof(TEnum)))}");
        }

        protected bool GetBoolean(DataRow row, string columnName)
        {
            return GetNullableBoolean(row, columnName) ?? throw new InvalidOperationException();
        }

        protected bool? GetNullableBoolean(DataRow row, string columnName)
        {
            var text = GetString(row, columnName);

            if (string.IsNullOrEmpty(text))
                return null;

            if (BooleanText.TryGetValue(text, out var value))
                return value;

            throw new ExpectedException($"{GetColumnId(row, columnName)} has an invalid boolean value '{text}'");
        }

        private static readonly Dictionary<string, bool> BooleanText =
            new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"true", true},
                {"yes", true},
                {"y", true},
                {"1", true},
                {"false", false},
                {"no", false},
                {"n", false},
                {"0", false},
            };
    }
}
