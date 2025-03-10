using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack.Logging.Log4Net;
using log4net;
using System.IO;
using System.Xml;
using System.Reflection;
using ExcelDataReader;
using ServiceStack;
using System.Data;

namespace ExcelToCSVProcessor
{
    internal class Program
    {
        private static ILog Log = null;

        static void Main(string[] args)
        {
            bool debug = false;
            Environment.ExitCode = 1;

            try
            {
                debug = args.Length == 1 && string.Compare(args[0], "DEBUG", true) == 0;

                ConfigureLogging();

                using (Stream stdin = Console.OpenStandardInput())
                {
                    using (Stream stdout = Console.OpenStandardOutput())
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            stdin.CopyTo(memoryStream);
                            memoryStream.Seek(0, SeekOrigin.Begin);

                            using (var reader = ExcelReaderFactory.CreateReader(memoryStream))
                            {
                                var spreadsheet = reader.AsDataSet();
                                var table = spreadsheet.Tables[0];

                                var worksheetColumn = new DataColumn() { DefaultValue = table.TableName };
                                table.Columns.Add(worksheetColumn);
                                worksheetColumn.SetOrdinal(0);
                                table.Rows[0][worksheetColumn] = "#SheetName#";

                                var csv = table.ToCSV();
                                var bytes = Encoding.UTF8.GetBytes(csv);

                                if (debug)
                                    foreach (var row in table.ToCSVArray())
                                        Log?.Info(row);

                                stdout.Write(bytes, 0, bytes.Length);
                                if (debug)
                                    Log?.Info($"Total row count(DEBUG): {table.Rows.Count}, Columns: {table.ColumnsToCSV()}");
                                else
                                    Log?.Info($"Total row count: {table.Rows.Count}, Columns: {table.ColumnsToCSV()}");
                            }
                        }
                    }
                }

                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Log?.Fatal(ex);
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                LogManager.Flush(5000);
            }
        }

        private static void ConfigureLogging()
        {
            using (var stream = new MemoryStream(EmbeddedResource.LoadEmbeddedResource("log4net.config")))
            using (var reader = new StreamReader(stream))
            {
                var xml = new XmlDocument();
                xml.LoadXml(reader.ReadToEnd());

                log4net.Config.XmlConfigurator.Configure(xml.DocumentElement);

                Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

                ServiceStack.Logging.LogManager.LogFactory = new Log4NetFactory();
            }
        }
    }
}