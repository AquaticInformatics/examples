using System.Collections.Generic;
using Aquarius.Samples.Client.ServiceModel;
using ServiceStack;

namespace ObservationReportExporter.ExtraApis.Samples
{
    [Route("/v1/exchangeconfigurations", HttpMethods.Get)]
    public class GetExchangeConfigurations : IReturn<SearchResultsExchangeConfigurations>
    {
    }

    public class SearchResultsExchangeConfigurations
    {
        public int TotalCount { get; set; }
        public List<ExchangeConfiguration> DomainObjects { get; set; } = new List<ExchangeConfiguration>();

    }

    public class ExchangeConfiguration
    {
        public string Id { get; set; }
        public string CustomId { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }

        public List<SamplingLocationMapping> SamplingLocationMappings { get; set; } = new List<SamplingLocationMapping>();
    }

    public class SamplingLocationMapping
    {
        public string Id { get; set; }
        public SamplingLocation SamplingLocation { get; set; }
        public string ExternalLocation { get; set; }
    }
}
