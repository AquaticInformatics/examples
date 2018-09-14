using System;
using System.Xml.Serialization;

namespace SosExporter.Dtos
{
    [Serializable]
    [XmlRoot("InsertSensorResponse", Namespace = "http://www.opengis.net/swes/2.0")]
    public class InsertSensorResponse
    {
        [XmlElement("assignedProcedure")] public string AssignedProcedure { get; set; }
        [XmlElement("assignedOffering")] public string AssignedOffering { get; set; }
    }
}
