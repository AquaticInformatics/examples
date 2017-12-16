using System;
using System.IO;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.Results;
using ManualGaugingPlugin.FileData;

namespace ManualGaugingPlugin
{
    public class ManualGaugingPlugin : IFieldDataPlugin
    {
        public ParseFileResult ParseFile(Stream fileStream, IFieldDataResultsAppender fieldDataResultsAppender, ILog logger)
        {
            var fieldVisit = ReadFile(fileStream);

            try
            {
                var location = fieldDataResultsAppender.GetLocationByIdentifier(fieldVisit.LocationIdentifier);
                logger.Info($"Parsing field data for location {location.LocationIdentifier}");

                var resultsGenerator = new FieldDataResultsGenerator(fieldDataResultsAppender, location, logger);
                return GetFieldDataResults(resultsGenerator, fieldVisit);
            }
            catch (Exception)
            {
                logger.Error($"Cannot parse file, location {fieldVisit.LocationIdentifier} not found");
                return ParseFileResult.CannotParse();
            }
        }

        public ParseFileResult ParseFile(Stream fileStream, LocationInfo targetLocation,
            IFieldDataResultsAppender fieldDataResultsAppender, ILog logger)
        {
            var fieldVisit = ReadFile(fileStream);

            //Only save data if it matches the targetLocation.
            if (!targetLocation.LocationIdentifier.Equals(fieldVisit.LocationIdentifier))
            {
                return ParseFileResult.CannotParse();
            }

            var resultsGenerator = new FieldDataResultsGenerator(fieldDataResultsAppender, targetLocation, logger);
            return GetFieldDataResults(resultsGenerator, fieldVisit);
        }

        private static FieldVisitRecord ReadFile(Stream fileStream)
        {
            var reader = new FileReader();
            return reader.ReadFile(fileStream);
        }

        private static ParseFileResult GetFieldDataResults(FieldDataResultsGenerator resultsGenerator, FieldVisitRecord record)
        {
            try
            {
                resultsGenerator.GenerateFieldDataResults(record);
                return ParseFileResult.SuccessfullyParsedAndDataValid();
            }
            catch (Exception ex)
            {
                return ParseFileResult.SuccessfullyParsedButDataInvalid(ex);
            }
        }
    }
}
