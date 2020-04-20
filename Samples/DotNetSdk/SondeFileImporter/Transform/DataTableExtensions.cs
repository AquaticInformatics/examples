using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using ServiceStack;

namespace SondeFileImporter.Transform
{
    public static class DataTableExtensions
    {
        public static string ToCsv(this DataTable table)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine(table.GetCsvHeader());

            foreach (DataRow row in table.Rows)
            {
                stringBuilder.AppendLine(row.ToCsv());
            }

            return stringBuilder.ToString();
        }

        private static string ToQuotedCsvLine(IEnumerable<string> items)
        {
            return string.Join(",", items.Select(e => e.ToCsvField()));
        }

        public static string GetCsvHeader(this DataTable table)
        {
            return ToQuotedCsvLine(table.ColumnNameList());
        }

        public static IReadOnlyList<string> ColumnNameList(this DataTable table)
        {
            return table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToList();
        }

        public static string ToCsv(this DataRow row)
        {
            var fields = row.ItemArray.Select(DataCellToString);

            return ToQuotedCsvLine(fields);
        }

        public static string GetStringValue(this DataRow row, string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException($"Column name must not be empty.", nameof(columnName));
            }

            return row[columnName].ToString();
        }

        private static string DataCellToString(object field)
        {
            var fieldType = field.GetType();
            if (fieldType == typeof(DateTime))
            {
                return ((DateTime)field).ToString("s");
            }

            if (fieldType == typeof(DateTimeOffset))
            {
                return ((DateTimeOffset)field).ToString("O");
            }

            return field.ToString();
        }

        public static string GetCombinedValues(this DataRow row, string[] columnNames)
        {
            var values = columnNames.Select(row.GetStringValue);

            return string.Join(" ", values);
        }
    }
}
