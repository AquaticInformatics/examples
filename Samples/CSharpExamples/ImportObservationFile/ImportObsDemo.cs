/*
 * The example below uses RestSharp, a popular .NET library to upload an observation file.
 * The example code is for demo only.
 * The author is not responsible for any consequence of using the code.
 */

using ImportObservationFile.ServiceModel;
using RestSharp;

namespace ImportObservationFile;

public static class ImportObsDemo
{
    private const string FileType = "SIMPLE_CSV";
    private const string HeaderNameLocation = "Location";
    private static readonly string DefaultTimezoneOffsetIfNoUtcOffsetSetInCsv = "-08:00";

    public static void Run(string url, string token, string csvFileFullPath)
    {
        var clientOptions = new RestClientOptions(url)
        {
            FollowRedirects = false //Disable auto redirect on your client to get a more consistent response behavior.
        };

        using var client = new RestClient(clientOptions);

        //Create a request, add query parameters and headers:
        const string resourcePart = "/api/v2/observationimports";
        var request = new RestRequest(resourcePart, Method.Post);

        request.AddParameter("fileType", FileType, ParameterType.QueryString);
        request.AddParameter("timeZoneOffset", DefaultTimezoneOffsetIfNoUtcOffsetSetInCsv, ParameterType.QueryString);
        request.AddParameter("linkFieldVisitsForNewObservations", "true", ParameterType.QueryString);

        //Auth token is added in the header:
        var headers = new Dictionary<string,string>
        {
            {"Content-Type", "multipart/form-data"}, //Must use multipart mime type for file import.
            {"Accept", "application/json, text/plain, */*"},
            {"Authorization", $"Token {token}"}
        };

        request.AddHeaders(headers);

        //Add the file:
        request.AddFile("file", csvFileFullPath);

        //Make the POST request:
        var response = client.Execute(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed: POST {resourcePart}:{response.StatusCode}.");
        }

        //Status Url is in the header:
        var statusUrl = response.GetHeaderValue(HeaderNameLocation); 
        if (string.IsNullOrWhiteSpace(statusUrl))
        {
            throw new Exception("Location header not found in the response headers.");
        }

        Console.WriteLine($"Status url: {statusUrl}");

        var resultUri = CheckStatusForResultUntilCompletedOrTimedOut(statusUrl, token, client);
        var resultSummaryJsonText = GetResultSummary(resultUri, token, client);

        //You can now parse the json and act accordingly based on the import result.
        Console.WriteLine(resultSummaryJsonText);
    }

    private static Uri CheckStatusForResultUntilCompletedOrTimedOut(string statusUrl, string token, RestClient client)
    {
        var statusRequest = new RestRequest(statusUrl, Method.Get);
        var statusRequestHeaders = new Dictionary<string, string>
        {
            {"Content-Type", "application/json"},
            {"Accept", "application/json"},
            {"Authorization", $"Token {token}"}
        };

        statusRequest.AddHeaders(statusRequestHeaders);

        //ImportStatus is a DTO that makes it convenient to consume the returned message:
        RestResponse<ImportStatus>? statusResponse;

        do
        {
            statusResponse = client.ExecuteGet<ImportStatus>(statusRequest);

            Console.WriteLine($"{statusResponse.Data}");
            Thread.Sleep(TimeSpan.FromSeconds(1));

        } while (statusResponse.IsSuccessStatusCode && //When completed, statusResponse.StatusCode will be System.Net.HttpStatusCode.RedirectMethod.
                 (statusResponse.Data?.ImportProcessorTransactionStatus == "PENDING" ||
                  statusResponse.Data?.ImportProcessorTransactionStatus == "IN_PROGRESS"));

        var resultUrl = statusResponse.GetHeaderValue(HeaderNameLocation);

        if (string.IsNullOrWhiteSpace(resultUrl))
        {
            throw new Exception("Location header not found in the response headers.");
        }

        return new Uri(resultUrl);
    }

    private static string? GetResultSummary(Uri resultUri, string token, RestClient client)
    {
        var resultRequest = new RestRequest(resultUri, Method.Get);
        var statusRequestHeaders = new Dictionary<string, string>
        {
            {"Content-Type", "application/json"},
            {"Accept", "application/json"},
            {"Authorization", $"Token {token}"}
        };

        resultRequest.AddHeaders(statusRequestHeaders);
 
        var resultResponse = client.Execute(resultRequest);

        //Depending on the result of the import, the status code could be Ok,Conflict.
        //But the import summary will be a JSON string in the response.Content.
        Console.WriteLine($"Result status code:{resultResponse.StatusCode}");

       return resultResponse.Content;
    }
}
