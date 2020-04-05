using Aquarius.Samples.Client.ServiceModel;
using ServiceStack;

namespace SondeFileSynchronizer.SamplesDtos
{
    [Route("v2/observationimports", "POST")]
    public class PostObservationImports : IReturn<ObservationImportSummary>, IReturn
    {
        // ReSharper disable InconsistentNaming - Samples url case sensitive.
        public string fileType { get; set; }

        public string timeZoneOffset { get; set; }

        public bool? linkFieldVisitsForNewObservations { get; set; }
    }
}