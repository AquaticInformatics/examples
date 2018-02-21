using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using Aquarius.Samples.Client;
using Aquarius.Samples.Client.ServiceModel;
using log4net;
using NodaTime;

namespace ObservationValidator
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static void Main()
        {
            var result = new ValidationResult();

            try
            {
                Environment.ExitCode = 1;

                var context = GetConfig();
                Log.Info($"Starting validation on {context.SamplesApiBaseUrl}.");

                var runStartTime = DateTimeOffset.Now.ToUniversalTime();

                Validate(context, result);

                LastRunTimeKeeper.WriteDateTimeOffsetToFile(runStartTime);

                Environment.ExitCode = 0;
                Log.Info("Finished validation.");
            }
            catch (Exception ex)
            {
                Log.Error("The program ended with an exception:", ex);
            }
            finally
            { 
                ReportValidationResult(result);
            }
        }

        private static void Validate(Context context, ValidationResult result)
        {
            var rules = RuleReader.ReadFromDefaultFile();

            if(!rules.Any())
            {
                throw new ArgumentException("No rules are found.");
            }

            Log.Info($"Got {rules.Count} rules from the file.");
            var validator = new Validator(rules);

            Log.Debug($"Connecting to server with URL {context.SamplesApiBaseUrl}.");

            using (var client = SamplesClient.CreateConnectedClient(context.SamplesApiBaseUrl, context.SamplesAuthToken))
            {
                var largeEnoughToGetAllObservationsInNamedSpecimens = 10000;
                var queryFromTime = GetLastRunStartTimeAtMinuteLevel(context.LastRunStartTimeUtc);

                Log.Info($"Examining observations modified from {queryFromTime}.");

                foreach (var specimen in client.LazyGet<Specimen, GetSpecimens, SearchResultSpecimen>(new GetSpecimens()).DomainObjects)
                {
                    if (result.ProcessedSpecimenNames.Contains(specimen.Name))
                        continue;
                    result.ProcessedSpecimenCount++;

                    var observationResponse = client.Get(new GetObservations
                    {
                        SpecimenName = specimen.Name, 
                        QualityControlTypes = new List<string> { "NORMAL"},
                        DataClassifications = new List<string> { "LAB"},
                        StartModificationTime = queryFromTime,
                        Limit = largeEnoughToGetAllObservationsInNamedSpecimens
                    });

                    var observations = observationResponse.DomainObjects;
                    result.ExaminedObservationsCount += observations.Count;

                    var invalidObservations = validator.GetInvalidObservations(observations);

                    var flaggedCount = PutBackFlaggedObservations(client, context, invalidObservations);
                    result.InvalidObservationsTotal += flaggedCount;

                    result.ProcessedSpecimenNames.Add(specimen.Name);
                }
            }
        }

        private static Instant? GetLastRunStartTimeAtMinuteLevel(DateTimeOffset lastRunStartTimeOffset)
        {
            var lastRunAtMinuteLevel = new DateTimeOffset(lastRunStartTimeOffset.Year, 
                lastRunStartTimeOffset.Month,
                lastRunStartTimeOffset.Day,
                lastRunStartTimeOffset.Hour,
                lastRunStartTimeOffset.Minute,
                0,
                lastRunStartTimeOffset.Offset);

            return Instant.FromDateTimeOffset(lastRunAtMinuteLevel);
        }

        private static int PutBackFlaggedObservations(ISamplesClient client, Context context, 
            List<Observation> invalidObservations)
        {
            var flag = context.Flag;
            var flaggedCount = 0;

            foreach (var invalidObservation in invalidObservations)
            {
                if(invalidObservation.LabResultDetails.QualityFlag == flag)
                    continue;

                client.Put(new PutSparseObservation
                {
                    Id = invalidObservation.Id,
                    LabResultDetails = new LabResultDetails
                    {
                        QualityFlag = flag
                    }
                });

                Log.Info($"Invalid observation:'{invalidObservation.Id}' is flagged.");
                flaggedCount++;
            }

            return flaggedCount;
        }

        private static Context GetConfig()
        {
            var appSettings = ConfigurationManager.AppSettings;
            var authToken = appSettings["authToken"];
            var samplesApiBaseUrl = appSettings["samplesApiBaseUrl"];
            var qualityFlag = appSettings["qualityFlag"];

            if(string.IsNullOrWhiteSpace(authToken))
                throw new ArgumentException("authToken is not set in the config file.");
            if(string.IsNullOrWhiteSpace(samplesApiBaseUrl))
                throw new ArgumentException("samplesApiBaseUrl is not set in the config file.");

            return new Context
            {
                SamplesApiBaseUrl = samplesApiBaseUrl,
                SamplesAuthToken = authToken,
                Flag = string.IsNullOrWhiteSpace(qualityFlag) ? Context.PredefinedFlag: qualityFlag,
                LastRunStartTimeUtc = LastRunTimeKeeper.GetLastRunStartTimeUtc()
            };
        }

        private static void ReportValidationResult(ValidationResult result)
        {
            Log.Info($"Total specimens examined: {result.ProcessedSpecimenCount}");
            Log.Info($"Total observations examined: {result.ExaminedObservationsCount}");
            Log.Info($"Invalid observations flagged: {result.InvalidObservationsTotal}");
        }
    }
}
