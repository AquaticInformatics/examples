using System;
using System.Collections.Generic;
using System.IO;
using FileHelpers;
using ServiceStack.Logging;

namespace UserImporter.Helpers
{
    public class CsvFileReaderWriter<TRecordType>
        where TRecordType : class
    {
        private readonly ILog _log;
        private readonly string _csvFileName;

        private bool HasErrors => _errors != null && _errors.Length > 0;
        private ErrorInfo[] _errors;

        public CsvFileReaderWriter(string fileName)
        {
            _log = LogManager.GetLogger(string.Format("{0}<{1}>", typeof(CsvFileReaderWriter<TRecordType>).Name, typeof(TRecordType).Name));

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            _csvFileName = fileName;
        }

        public IEnumerable<TRecordType> ReadRecords(bool ignoreFirstLine)
        {
            using (var reader = new StreamReader(_csvFileName))
            {
                using (var recordEngine = new FileHelperAsyncEngine<TRecordType>())
                {
                    if (ignoreFirstLine)
                        recordEngine.Options.IgnoreFirstLines = 1;

                    recordEngine.ErrorMode = ErrorMode.SaveAndContinue;

                    recordEngine.BeginReadStream(reader);

                    foreach (var record in recordEngine)
                    {
                        yield return record;
                    }

                    _errors = recordEngine.ErrorManager.Errors;
                    LogErrorCount();
                }
            }
        }

        public void WriteRecords(List<TRecordType> records, bool ignoreFirstList)
        {
            var engine = new FileHelperEngine<TRecordType>();
            
            if(ignoreFirstList)
                engine.HeaderText = engine.GetFileHeader();

            engine.WriteFile(_csvFileName, records);
        } 


        private void LogErrorCount()
        {
            if (!HasErrors) return;

            _log.ErrorFormat("{0} invalid lines in file '{3}'. Line {1}, error: {2}", _errors.Length, _errors[0].LineNumber, _errors[0].ExceptionInfo.Message, _csvFileName);

        }

    }
}
