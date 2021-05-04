using RestSharp;
using RestSharp.Authenticators;
using ServiceStack;
using System.Linq;

namespace LabFileImporter
{
    public class FileUploader
    {
        public static FileUploader Create(string baseUri, string apiToken, string userAgent)
        {
            return new FileUploader(baseUri, apiToken, userAgent);
        }

        private RestClient RestClient { get; set; }

        private FileUploader(string baseUri, string apiToken, string userAgent)
        {
            RestClient = new RestClient(baseUri)
            {
                Authenticator = SampleAuthenticator.Create(apiToken, userAgent)
            };
        }

        public string UploadFile(string relativeUrl, byte[] contentToUpload, string uploadedFilename)
        {
            var contentType = MimeTypes.GetMimeType(uploadedFilename);
            var request = new RestRequest(relativeUrl, Method.POST);
            request.AddFileBytes("file", contentToUpload, uploadedFilename, contentType);

            var response = RestClient.Execute(request);

            if (!response.IsSuccessful)
                throw new WebServiceException(response.ErrorMessage, response.ErrorException);

            return response
                .Headers
                .FirstOrDefault(h => h.Name == "Location")
                ?.Value?
                .ToString();
        }

        private class SampleAuthenticator : IAuthenticator
        {
            public static SampleAuthenticator Create(string apiToken, string userAgent)
            {
                return new SampleAuthenticator(apiToken, userAgent);
            }

            private string ApiToken { get; }
            private string UserAgent { get; }

            private SampleAuthenticator(string apiToken, string userAgent)
            {
                ApiToken = $"Token {apiToken}";
                UserAgent = userAgent;
            }

            public void Authenticate(RestSharp.IRestClient client, IRestRequest request)
            {
                client.UserAgent = UserAgent;

                request.AddHeader("Authorization", ApiToken);
            }
        }
    }
}
