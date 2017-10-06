using System;
using System.IO;
using System.Text;
using ManualGaugingPlugin.FileData;

namespace ManualGaugingPlugin
{
    public class FileReader
    {
        public FieldVisitRecord ReadFile(Stream fileStream)
        {
            using (var reader = CreateStreamReader(fileStream))
            {
                //TODO - Read field data from file.
                const string locationIdentifierFromFile = "ExampleLocationIdentifier";
                var startDateReadFromFile = new DateTime(2017, 09, 27, 0, 0, 0);
                var endDateReadFromFile = new DateTime(2017, 09, 27, 23, 59, 59);
                return new FieldVisitRecord(new MetricUnitSystem(), locationIdentifierFromFile, startDateReadFromFile,
                    endDateReadFromFile);
            }
        }

        private static StreamReader CreateStreamReader(Stream fileStream)
        {
            const int defaultByteBufferSize = 1024;

            //NOTE: Make sure to set leaveOpen property on StreamReader so the Stream does not close when the StreamReader closes.
            //Framework will take care of closing the Stream.
            return new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: defaultByteBufferSize, leaveOpen: true);
        }
    }
}
