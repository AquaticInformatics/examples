using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using Aquarius.Samples.Client;
using Aquarius.Samples.Client.ServiceModel;
using Aquarius.TimeSeries.Client;
using ServiceStack;
using SondeFileSynchronizer.Config;

namespace SondeFileSynchronizer.ServiceClient
{
    public class ImportClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ISamplesClient _samplesClient;
        private readonly AuthenticationHeaderValue _authHeaderValue;
        private readonly Context _context;

        public ImportClient(Context context)
        {
            _context = context;

            _httpClient = GetInitializedHttpClient();
            _samplesClient = SamplesClient.CreateConnectedClient(_context.SamplesApiBaseUrl, _context.SamplesAuthToken);
            _authHeaderValue = new AuthenticationHeaderValue("Token", _context.SamplesAuthToken);
        }

        private HttpClient GetInitializedHttpClient()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            return new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan,
                DefaultRequestHeaders =
                {
                    UserAgent =
                    {
                        new ProductInfoHeaderValue(
                            Assembly.GetExecutingAssembly().GetName().Name,
                            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version)
                    }
                }
            };
        }

        public string PostImportDryRunForStatusUrl(FileInfo csvFileInfo)
        {
            var importRequest = new PostObservationsDryRunV2
            {
                FileType = "SIMPLE_CSV",
                LinkFieldVisitsForNewObservations = false,
                TimeZoneOffset = _context.DefaultUtcOffset
            };

            using (var stream = new MemoryStream(File.ReadAllBytes(csvFileInfo.FullName)))
            {
                _samplesClient.PostFileWithRequest(stream, csvFileInfo.Name, importRequest);
                return _samplesClient.LocationResponseHeader;
            }
        }

        public string PostImportForStatusUrl(FileInfo csvFileInfo)
        {
            var importRequest = new PostObservationImportV2
            {
                FileType = "SIMPLE_CSV",
                LinkFieldVisitsForNewObservations = false,
                TimeZoneOffset = _context.DefaultUtcOffset
            };

            using (var stream = new MemoryStream(File.ReadAllBytes(csvFileInfo.FullName)))
            {
                _samplesClient.PostFileWithRequest(stream, csvFileInfo.Name, importRequest);
                return _samplesClient.LocationResponseHeader;
            }
        }

        public ImportStatus GetImportStatusUntilComplete(string statusUrl)
        {
            var result = _samplesClient.Client.RequestAndPollUntilComplete(
                client => GetImportStatus(statusUrl),
                (client, response) => GetImportStatus(statusUrl),
                polledStatus => polledStatus.IsImportFinished);

            if (!result.IsImportFinished)
                throw new Exception($"Unexpected append status={result.HttpStatusCode}");

            return result;
        }

        public ImportStatus GetImportStatus(string statusUrl)
        {
            using (var response = GetResponseByUrl(statusUrl))
            {
                return new ImportStatus
                {
                    HttpStatusCode = response.StatusCode,
                    ResultUri = response.Headers.Location
                };
            }
        }

        private HttpResponseMessage GetResponseByUrl(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
            request.Headers.Authorization = _authHeaderValue;

            var getTask = _httpClient.SendAsync(request);
            return getTask.Result;
        }

        private string GetContent(HttpResponseMessage response)
        {
            if (response?.Content == null)
            {
                return string.Empty;
            }

            using (var stream = response.Content.ReadAsStreamAsync().Result)
            using (var streamReader = new StreamReader(stream))
            {
                return streamReader.ReadToEnd();
            }
        }

        public ImportResultResponse GetResult(string resultUrl)
        {
            using (var response = GetResponseByUrl(resultUrl))
            {
                var content = GetContent(response);
                return content.FromJson<ImportResultResponse>();
            }
        }

        public string GetContentWithoutAuthorizationHeader(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
            var result = _httpClient.SendAsync(request).Result;

            return GetContent(result);
        }

        public void Dispose()
        {
            using (_httpClient) { }
            using (_samplesClient) { }
        }
    }
}
