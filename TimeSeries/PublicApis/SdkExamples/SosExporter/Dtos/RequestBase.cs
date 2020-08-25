using System.Collections.Generic;

namespace SosExporter.Dtos
{
    public abstract class RequestBase
    {
        protected RequestBase(string request)
        {
            Request = request;
        }

        public string Service { get; set; } = "SOS";
        public string Version { get; set; } = "2.0.0";
        public string Request { get; set; }
    }

    public abstract class ResponseBase
    {
        public string Request { get; set; }
        public string Version { get; set; }
        public string Service { get; set; }
        public List<ResponseErrorContext> Exceptions { get; set; }
    }

    public class ResponseErrorContext
    {
        public string Code { get; set; }
        public string Locator { get; set; }
        public string Text { get; set; }
    }
}
