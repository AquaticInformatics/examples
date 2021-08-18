﻿using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Web;
using Aquarius.Samples.Client;
using Aquarius.Samples.Client.ServiceModel;
using Aquarius.TimeSeries.Client;
using NodaTime;
using ServiceStack;

namespace LabFileImporter
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
            _samplesClient = SamplesClient.CreateConnectedClient(_context.ServerUrl, _context.ApiToken);
            _authHeaderValue = new AuthenticationHeaderValue("Token", _context.ApiToken);
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
                            ExeHelper.ExeName,
                            ExeHelper.ExeVersion)
                    }
                }
            };
        }

        private FileUploader FileUploader { get; set; }

        private void CreateUploader()
        {
            if (FileUploader != null)
                return;

            if (!(_samplesClient.Client is SamplesServiceClient client))
                return;

            FileUploader = FileUploader.Create(client.BaseUri, _context.ApiToken, client.UserAgent);
        }

        public string PostImportDryRunForStatusUrl(string filename, byte[] contentBytes)
        {
            CreateUploader();

            var importRequest = new PostObservationsDryRunV2
            {
                FileType = "SIMPLE_CSV",
                LinkFieldVisitsForNewObservations = true,
                TimeZoneOffset = FormatUtcOffset(_context.UtcOffset)
            };

            return FileUploader.UploadFile(
                $"/v2/observationimports/dryrun?fileType={importRequest.FileType}&linkFieldVisitsForNewObservations={importRequest.LinkFieldVisitsForNewObservations}&timeZoneOffset={importRequest.TimeZoneOffset}",
                contentBytes,
                filename);
        }

        private static string FormatUtcOffset(Offset offset)
        {
            return HttpUtility.UrlEncode($"{offset:m}");
        }

        public string PostImportForStatusUrl(string filename, byte[] contentBytes)
        {
            CreateUploader();

            var importRequest = new PostObservationImportV2
            {
                FileType = "SIMPLE_CSV",
                LinkFieldVisitsForNewObservations = true,
                TimeZoneOffset = FormatUtcOffset(_context.UtcOffset)
            };

            return FileUploader.UploadFile(
                $"/v2/observationimports?fileType={importRequest.FileType}&linkFieldVisitsForNewObservations={importRequest.LinkFieldVisitsForNewObservations}&timeZoneOffset={importRequest.TimeZoneOffset}",
                contentBytes,
                filename);
        }

        public ImportStatus GetImportStatusUntilComplete(string statusUrl)
        {
            var result = _samplesClient.Client.RequestAndPollUntilComplete(
                client => GetImportStatus(statusUrl),
                (client, response) => GetImportStatus(statusUrl),
                polledStatus => polledStatus.IsImportFinished);

            if (!result.IsImportFinished)
                throw new ExpectedException($"Unexpected import status={result.HttpStatusCode}");

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
