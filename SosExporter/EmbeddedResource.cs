using System.IO;
using System.Reflection;
using ServiceStack;

namespace SosExporter
{
    public class EmbeddedResource
    {
        public static byte[] LoadEmbeddedResource(string path)
        {
            // ReSharper disable once PossibleNullReferenceException
            var resourceName = $"{MethodBase.GetCurrentMethod().DeclaringType.Namespace}.{path.Replace('\\', '.')}";

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new ExpectedException($"Can't load '{resourceName}' as embedded resource.");

                return stream.ReadFully();
            }
        }

        public static string LoadEmbeddedXml(string path)
        {
            using (var stream = new MemoryStream(LoadEmbeddedResource(path)))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
