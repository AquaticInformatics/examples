using ServiceStack;

namespace SosExporter.Dtos
{
    [Route("/admin/operations/json", HttpMethods.Post)]
    public class ConfigureOperationRequest : IReturnVoid
    {
        public string Service { get; set; }
        public string Version { get; set; }
        public string Operation { get; set; }
        public bool Active { get; set; }
    }
}
