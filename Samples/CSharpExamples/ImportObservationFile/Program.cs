/*
 * This is a CSharp .NET 6.0 console project that shows how to call Samples API to upload csv files
 * to import observations.
 * The example code is for demo only.
 * The author is not responsible for any consequence of using the code.
 */

//Get the URL of Samples and API token:

using ImportObservationFile;

Console.WriteLine("Please enter your Samples URL (example: https://myorg.aqsamples.ca/):");
var url = Console.ReadLine();
ArgumentNullException.ThrowIfNull(url,nameof(url));

Console.WriteLine("Please enter the auth token. You can get one from https://[your_aqsamples_url/api/:");
var token = Console.ReadLine();
ArgumentNullException.ThrowIfNull(token, nameof(token));

Console.WriteLine("Please enter the full path of a valid observation csv file. Refer to the help manual to create one:");
var csvFileFullPath = Console.ReadLine();
ArgumentNullException.ThrowIfNull(csvFileFullPath, nameof(csvFileFullPath));
if (!File.Exists(csvFileFullPath)) throw new ArgumentException($"File not found: {csvFileFullPath}");

try
{
   ImportObsDemo.Run(url, token, csvFileFullPath);
}
catch (Exception e)
{
    Console.WriteLine(e);
}

Console.WriteLine("Demo finished.");
