using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using log4net;
using ServiceStack;
using ServiceStack.Text;
using ServiceStack.Text.Common;
using SosExporter.Dtos;
using SosExporter.Ogc;
using InterpolationType = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.InterpolationType;

namespace SosExporter
{
    public class SosClient : ISosClient
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static ISosClient CreateConnectedClient(string hostUrl, string username, string password)
        {
            var client = new SosClient(hostUrl, username, password);

            client.Connect();

            return client;
        }

        public int MaximumPointsPerObservation { get; set; }

        private string HostUrl { get; }
        private string Username { get; }
        private string Password { get; }
        private JsonServiceClient JsonClient { get; set; }
        private GetCapabilitiesResponse Capabilities { get; set; }

        private SosClient(string hostUrl, string username, string password)
        {
            HostUrl = hostUrl.TrimEnd('/');
            Username = username;
            Password = password;

            ConfigureJsonOnce();
        }

        public static void ConfigureJsonOnce()
        {
            JsConfig<DateTimeOffset>.DeSerializeFn = JavaDateTimeOffsetDeserializer;
        }

        private static readonly DateTimeOffset FixedResultTime = DateTimeOffset.MinValue; // In Java, this was "0000-01-01T00:00:00.000Z", which is 1 year early than .NET can represent

        private static DateTimeOffset JavaDateTimeOffsetDeserializer(string value)
        {
            if (value.StartsWith("0000-"))
                return DateTimeOffset.MinValue;

            return DateTimeSerializer.ParseDateTimeOffset(value);
        }

        public void Dispose()
        {
            Disconnect();
        }

        private JsConfigScope SosScope()
        {
            return JsConfig.With(emitCamelCaseNames: true);
        }

        public void Connect()
        {
            JsonClient?.Dispose();

            JsonClient = new JsonServiceClient(HostUrl);

            GetCapabilities();

            CreateAuthenticatedSession();
        }

        public void Disconnect()
        {
            if (JsonClient == null) return;

            ReleaseAuthenticatedSession();
            JsonClient = null;
        }

        private Action<HttpWebRequest> UseJsonClientCookies => req => req.CookieContainer = JsonClient.CookieContainer;

        private void GetCapabilities()
        {
            using (SosScope())
            {
                Capabilities = JsonClient.Post(new GetCapabilitiesRequest
                {
                    Sections = new List<string> { "Contents" },
                    Request = "GetCapabilities",
                    Service = "SOS"
                });
            }
        }

        private void CreateAuthenticatedSession()
        {
            var login = new NameValueCollection
            {
                {"username", Username},
                {"password", Password}
            };

            $"{HostUrl}/j_spring_security_check"
                .PostToUrl(
                    login.ToFormUrlEncoded(),
                    MimeTypes.Xml,
                    UseJsonClientCookies);

            EnableTransactionalOperations();
        }

        private void ReleaseAuthenticatedSession()
        {
            DisableTransactionalOperations();

            $"{HostUrl}/j_spring_security_logout"
                .GetStringFromUrl(requestFilter: UseJsonClientCookies);
        }

        public void ClearDatasource()
        {
            using (SosScope())
            {
                Log.Info("Clearing the SOS database ...");
                JsonClient.Post(new ClearDatasourceRequest());
            }
        }

        public void DeleteDeletedObservations()
        {
            using (SosScope())
            {
                Log.Info("Deleting stale observations ...");
                JsonClient.Post(new DeleteDeletedObservationsRequest());
            }
        }

        private static readonly string[] TransactionalOperations = {"DeleteSensor", "InsertSensor", "InsertObservation"};

        public void EnableTransactionalOperations()
        {
            using (SosScope())
            {
                foreach (var operation in TransactionalOperations)
                {
                    ConfigureOperation(operation, true);
                }
            }
        }

        public void DisableTransactionalOperations()
        {
            using (SosScope())
            {
                foreach (var operation in TransactionalOperations)
                {
                    ConfigureOperation(operation, false);
                }
            }
        }

        private void ConfigureOperation(string operation, bool active)
        {
            JsonClient.Post(new ConfigureOperationRequest
            {
                Service = Capabilities.Service,
                Version = Capabilities.Version,
                Operation = operation,
                Active = active
            });
        }

        public void DeleteSensor(TimeSeriesDataServiceResponse timeSeries)
        {
            var xml = TransformXmlTemplate(@"XmlTemplates\DeleteSensor.xml", timeSeries);

            try
            {
                Log.Info($"Deleting sensor for '{CreateProcedureUniqueId(timeSeries)}' ...");
                PostPox(xml);
            }
            catch (WebServiceException exception)
            {
                var fieldError = exception.GetFieldErrors().FirstOrDefault();

                if (fieldError?.ErrorCode == "InvalidParameterValue" && fieldError.FieldName == "procedure")
                {
                    // Silently ignore "Time-series not found" when deleting
                    return;
                }

                throw;
            }
        }

        public SensorInfo FindExistingSensor(TimeSeriesDataServiceResponse timeSeries)
        {
            var procedureUniqueId = CreateProcedureUniqueId(timeSeries);

            return Capabilities.Contents.FirstOrDefault(c => c.Procedure.TrueForAll(s => s == procedureUniqueId));
        }

        public InsertSensorResponse InsertSensor(TimeSeriesDataServiceResponse timeSeries)
        {
            var xml = TransformXmlTemplate(@"XmlTemplates\InsertSensor.xml", timeSeries);

            Log.Info($"Inserting sensor for '{CreateProcedureUniqueId(timeSeries)}' ...");
            var responseXml = PostPox(xml);

            return FromXml<InsertSensorResponse>(responseXml);
        }

        public void InsertObservation(string assignedOffering, LocationDataServiceResponse location, LocationDescription locationDescription, TimeSeriesDataServiceResponse timeSeries, TimeSeriesDescription timeSeriesDescription)
        {
            var xmlTemplatePath = IsSpatialLocationDefined(location)
                ? @"XmlTemplates\InsertObservation.xml"
                : @"XmlTemplates\InsertObservationWithNoGeoSpatial.xml";

            const string pointTokenSeparator = ";";
            const string pointBlockSeparator = "@";

            var observablePropertyFieldName = SanitizeIdentifier($" {timeSeries.Parameter}_{timeSeries.Label}".Replace(" ", "_")); // TODO: Figure this mapping out

            var substitutions = CreateSubstitutions(timeSeries)
                .Concat(new Dictionary<string, string>
                {
                    {"{__offeringUri__}", assignedOffering},
                    {"{__featureOfInterestName__}", location.LocationName},
                    {"{__featureOfInterestLatitude__}", $"{location.Latitude}"},
                    {"{__featureOfInterestLongitude__}", $"{location.Longitude}"},
                    {"{__observablePropertyUnit__}", timeSeries.Unit},
                    {"{__observablePropertyFieldName__}", observablePropertyFieldName},
                    {"{__resultTime__}", $"{FixedResultTime:O}"},
                    {"{__pointTokenSeparator__}", pointTokenSeparator},
                    {"{__pointBlockSeparator__}", pointBlockSeparator},
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var procedureUniqueId = substitutions[ProcedureUniqueIdKey];

            for (var insertedPoints = 0; insertedPoints < timeSeries.Points.Count; )
            {
                var points = timeSeries.Points
                    .Skip(insertedPoints)
                    .Take(MaximumPointsPerObservation)
                    .ToList();

                substitutions["{__phenomenonStartTime__}"] = $"{points.First().Timestamp.DateTimeOffset:O}";
                substitutions["{__phenomenonEndTime__}"] = $"{points.Last().Timestamp.DateTimeOffset:O}";
                substitutions["{__pointCount__}"] = $"{points.Count}";
                substitutions["{__pointValues__}"] = string.Join(pointBlockSeparator, points.Select(p => $"{p.Timestamp.DateTimeOffset:O}{pointTokenSeparator}{p.Value.Display}"));

                insertedPoints += points.Count;

                var xml = TransformXmlTemplate(xmlTemplatePath, substitutions);

                Log.Info($"Posting {points.Count} data points to '{procedureUniqueId}' ...");
                PostPox(xml);
            }
        }

        private static bool IsSpatialLocationDefined(LocationDataServiceResponse location)
        {
            return // ReSharper disable once CompareOfFloatsByEqualityOperator
                (location.Latitude != 0
                 // ReSharper disable once CompareOfFloatsByEqualityOperator
                 && location.Longitude != 0);
        }

        private static string TransformXmlTemplate(string path, TimeSeriesDataServiceResponse timeSeries)
        {
            return TransformXmlTemplate(path, CreateSubstitutions(timeSeries));
        }

        private const string ProcedureUniqueIdKey = "{__procedureUniqueId__}";

        private static Dictionary<string, string> CreateSubstitutions(TimeSeriesDataServiceResponse timeSeries)
        {
            var timeSeriesIdentifier = CreateProcedureUniqueId(timeSeries);

            return new Dictionary<string, string>
            {
                {ProcedureUniqueIdKey, timeSeriesIdentifier},
                {"{__featureOfInterestId__}", SanitizeIdentifier(timeSeries.LocationIdentifier)},
                {"{__observablePropertyName__}", SanitizeIdentifier($"{timeSeries.Parameter}_{timeSeries.Label}")},
            };
        }

        private static string CreateProcedureUniqueId(TimeSeriesDataServiceResponse timeSeries)
        {
            return SanitizeIdentifier($"{timeSeries.Parameter}.{timeSeries.Label}@{timeSeries.LocationIdentifier}_{InterpolationTypeSuffix[timeSeries.InterpolationTypes.First().Type]}");
        }

        // From ca/ai/gaia/ds/mappers/InterpolationTypeNameMapper.java
        private static readonly Dictionary<string, string> InterpolationTypeSuffix =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                {InterpolationType.DiscreteValues.ToString(), "Discrete"},
                {InterpolationType.InstantaneousTotals.ToString(), "InstantTotal"},
                {InterpolationType.InstantaneousValues.ToString(), "Instantaneous"},
                {InterpolationType.PrecedingConstant.ToString(), "AveragePrec"},
                {InterpolationType.PrecedingTotals.ToString(), "TotalPrec"},
                {InterpolationType.SucceedingConstant.ToString(), "AverageSucc"},
            };

        private static string SanitizeIdentifier(string text)
        {
            return InvalidIdentifierCharsRegex.Replace(text, "_");
        }

        private static readonly Regex InvalidIdentifierCharsRegex = new Regex(@"[,()]");

        private static string TransformXmlTemplate(string path, Dictionary<string, string> substitutions)
        {
            var xml = EmbeddedResource.LoadEmbeddedXml(path);

            foreach (var substitution in substitutions)
            {
                xml = xml.Replace(substitution.Key, System.Security.SecurityElement.Escape(substitution.Value));
            }

            return xml;
        }

        private string PostPox(string xml)
        {
            var responseXml = $"{HostUrl}/service/pox"
                .PostXmlToUrl(xml, UseJsonClientCookies);

            if (!responseXml.EndsWith("ExceptionReport>"))
                return responseXml; // Success!

            var report = FromXml<ExceptionReport>(responseXml);

            var firstException = report.Exception.FirstOrDefault();

            if (firstException == null)
                throw new InvalidOperationException(
                    $"Can't find an OGC exception nested within XML={responseXml}");

            var message = string.Join("\n", firstException.ExceptionText);

            var exception = new WebServiceException(message)
            {
                StatusCode =
                    firstException.exceptionCode.StartsWith("Invalid", StringComparison.InvariantCultureIgnoreCase)
                        ? (int) HttpStatusCode.BadRequest
                        : (int) HttpStatusCode.InternalServerError,

                StatusDescription = firstException.exceptionCode,
                ResponseBody = responseXml,
                ResponseDto = new OgcResponseStatus
                {
                    ResponseStatus = new ResponseStatus
                    {
                        Errors = new List<ResponseError>
                        {
                            new ResponseError
                            {
                                ErrorCode = firstException.exceptionCode,
                                FieldName = firstException.locator,
                                Message = message
                            }
                        }
                    }
                }
            };

            throw exception;
        }

        private class OgcResponseStatus : IHasResponseStatus
        {
            public ResponseStatus ResponseStatus { get; set; }
        }

        private T FromXml<T>(string xml) where T : new()
        {
            using (var reader = new StringReader(xml))
            {
                var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
                return (T) xmlSerializer.Deserialize(reader);
            }
        }
    }
}
