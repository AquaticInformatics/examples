using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;

namespace ReflectedSeriesAggregator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Read app settings
            var builder = new ConfigurationBuilder()
                   .SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("appsettings.json");
            var configuration = builder.Build();

            // Configure & create serilog logger using above configuration
            using (var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger())
            {
                try
                {
                    // Parse command line
                    if (!ArgHandler.Parse(args, logger, out Settings settings))
                        Environment.Exit(0);

                    // Do some work
                    new Work(logger, settings).Run();
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    logger.Fatal(ex.Message, ex);
                    Environment.Exit(-1);
                }
            }
        }
    }
}
