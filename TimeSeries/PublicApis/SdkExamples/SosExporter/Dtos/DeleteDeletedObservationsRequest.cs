using ServiceStack;

namespace SosExporter.Dtos
{
    [Route("/admin/datasource/deleteDeletedObservations", HttpMethods.Post)]
    public class DeleteDeletedObservationsRequest : IReturnVoid
    {
    }
}
