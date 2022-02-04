using ServiceStack;

namespace ObservationReportExporter.ExtraApis.TimeSeries
{
    [Route("/attachments/{Id}", HttpMethods.Delete)]
    public class DeleteAttachmentById : IReturnVoid
    {
        public string Id { get; set; }
    }
}
