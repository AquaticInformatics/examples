using System;
using System.IO;
using System.Runtime.Serialization;
using ServiceStack;

namespace TotalDischargeExternalProcessor
{
    public class ConfigLoader
    {
        public Config Load(string path)
        {
            if (!File.Exists(path))
                return new Config();

            try
            {
                return File.ReadAllText(path)
                    .FromJson<Config>();
            }
            catch (SerializationException exception)
            {
                throw new ArgumentException($"'{path}' is not a valid {nameof(Config)} JSON file.", exception);
            }
        }
    }
}
