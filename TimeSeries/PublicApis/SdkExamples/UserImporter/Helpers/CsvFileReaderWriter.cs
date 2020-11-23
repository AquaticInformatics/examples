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
            _log = LogManager.GetLogger($"{typeof(CsvFileReaderWriter<TRecordType>).Name}<{typeof(TRecordType).Name}>");

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            _csvFileName = fileName;
        }

        public IEnumerable<TRecordType> ReadRecords(bool ignoreFirstLine)
        {
            if (!File.Exists(_csvFileName))
                throw new ExpectedException($"CSV file '{_csvFileName}' does not exist.");

            using (var stream = File.Open(_csvFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream))
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

            _log.Error($"{ _errors.Length} invalid lines in file '{_csvFileName}'. Line {_errors[0].LineNumber}, error: {_errors[0].ExceptionInfo.Message}");
        }
    }
}
