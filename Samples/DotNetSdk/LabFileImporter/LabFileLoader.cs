using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExcelDataReader;
using ExcelDataReader.Exceptions;

namespace LabFileImporter
{
    public class LabFileLoader// : ExcelLoaderBase
    {
        public Context Context { get; set; }

        private bool BulkMode { get; set; }

        private class Property
        {
            public string PropertyId { get; set; }
            public string Unit { get; set; }
            public string PQL { get; set; }
            public string Method { get; set; }
        }

        private List<Property> Properties { get; } = new List<Property>();

        public IEnumerable<ObservationV2> Load(string path)
        {
            if (!File.Exists(path))
                throw new ExpectedException($"The file '{path}' does not exist.");

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = CreateReaderFromStream(path, stream))
            {
                var currentLine = 0;
                var isHeader = true;
                while (reader.Read())
                {
                    ++currentLine;

                    if (!isHeader)
                    {
                        var observation = LoadRow(reader);

                        if (observation != null)
                            yield return observation;

                        continue;
                    }

                    var columns = Enumerable
                        .Range(0, reader.FieldCount)
                        .Select(i => reader.IsDBNull(i) ? string.Empty : reader.GetValue(i).ToString())
                        .ToList();

                    if (!columns.Any())
                        continue;

                    var propertyValues = columns
                        .Skip('M' - 'A')
                        .ToList();

                    switch (currentLine)
                    {
                        case 1: // Main column
                            Properties.AddRange(propertyValues.Select(s => new Property{ PropertyId = s}));
                            break;

                        case 3: // Unit
                            for (var i = 0; i < propertyValues.Count; ++i)
                            {
                                Properties[i].Unit = propertyValues[i];
                            }
                            break;

                        case 4: // PQL
                            for (var i = 0; i < propertyValues.Count; ++i)
                            {
                                Properties[i].PQL = propertyValues[i];
                            }
                            break;

                        case 5: // Method
                            for (var i = 0; i < propertyValues.Count; ++i)
                            {
                                Properties[i].Method = propertyValues[i];
                            }
                            break;

                        case 6:
                            if (!columns[0].Equals("unity water internal", StringComparison.InvariantCultureIgnoreCase))
                            {
                                // We are done, this is the single file format
                                BulkMode = false;
                                isHeader = false;
                                var observation = LoadRow(reader);

                                if (observation != null)
                                    yield return observation;
                            }

                            break;

                        case 7:
                            isHeader = false;
                            BulkMode = true;
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


        private ObservationV2 LoadRow(IExcelDataReader reader)
        {
            return BulkMode
                ? LoadBulkRow(reader)
                : LoadSingleLocationRow(reader);
        }

        private ObservationV2 LoadBulkRow(IExcelDataReader reader)
        {
            return null;
        }

        private ObservationV2 LoadSingleLocationRow(IExcelDataReader reader)
        {
            return null;
        }
    }
}
