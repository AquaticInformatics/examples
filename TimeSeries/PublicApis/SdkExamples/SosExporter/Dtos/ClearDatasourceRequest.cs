using ServiceStack;

namespace SosExporter.Dtos
{
    [Route("/admin/datasource/clear", HttpMethods.Post)]
    public class ClearDatasourceRequest : IReturnVoid
    {
    }
}
