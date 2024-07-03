using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelToCSVProcessor
{
    public static class TableExtension
    {
        public static string ToCSV(this DataTable dtDataTable)
        {
            var builder = new StringBuilder();
            foreach (DataRow dr in dtDataTable.Rows)
            {
                for (int i = 0; i < dtDataTable.Columns.Count; i++)
                {
                    if (!Convert.IsDBNull(dr[i]))
                    {
                        string value = dr[i].ToString();
                        if (value.StartsWith("\""))
                        {
                            builder.Append(dr[i].ToString());
                        }
                        else
                        {
                            value = string.Format("\"{0}\"", value);
                            builder.Append(value);
                        }
                    }
                    if (i < dtDataTable.Columns.Count - 1)
                    {
                        builder.Append(",");
                    }
                }
                builder.Append(Environment.NewLine);
            }

            var csv = builder.ToString();
            return csv;
        }

        public static string[] ToCSVArray(this DataTable dtDataTable)
        {
            var array = new List<string>();
            
            foreach (DataRow dr in dtDataTable.Rows)
            {
                var builder = new StringBuilder();
                for (int i = 0; i < dtDataTable.Columns.Count; i++)
                {
                    if (!Convert.IsDBNull(dr[i]))
                    {
                        string value = dr[i].ToString();
                        if (value.StartsWith("\""))
                        {
                            builder.Append(dr[i].ToString());
                        }
                        else
                        {
                            value = string.Format("\"{0}\"", value);
                            builder.Append(value);
                        }
                    }
                    if (i < dtDataTable.Columns.Count - 1)
                    {
                        builder.Append(",");
                    }
                }

                array.Add(builder.ToString());
            }
            return array.ToArray();
        }

        public static string ColumnsToCSV(this DataTable dtDataTable)
        {
            var builder = new StringBuilder();
            foreach (DataRow dr in dtDataTable.Rows)
            {
                for (int i = 0; i < dtDataTable.Columns.Count; i++)
                {
                    if (!Convert.IsDBNull(dr[i]))
                    {
                        string value = dr[i].ToString();
                        if (value.StartsWith("\""))
                        {
                            builder.Append(dr[i].ToString());
                        }
                        else
                        {
                            value = string.Format("\"{0}\"", value);
                            builder.Append(value);
                        }
                    }
                    if (i < dtDataTable.Columns.Count - 1)
                    {
                        builder.Append(",");
                    }
                }
                
                break;
            }

            var csv = builder.ToString();
            return csv;
        }
    }
}
