using System.Collections.Generic;
using System.IO;
using ServiceStack.Text;

namespace LabFileImporter
{
    public class CsvWriter
    {
        public void WriteObservations(StreamWriter writer, IEnumerable<ObservationV2> observations)
        {
            CsvConfig<ObservationV2>.CustomHeadersMap = new Dictionary<string, string>
            {
                {nameof(ObservationV2.ObservationID), "Observation ID"},
                {nameof(ObservationV2.LocationID), "Location ID"},
                {nameof(ObservationV2.ObservedPropertyID), "Observed Property ID"},
                {nameof(ObservationV2.ObservedDateTime), "Observed DateTime"},
                {nameof(ObservationV2.AnalyzedDateTime), "Analyzed DateTime"},
                {nameof(ObservationV2.Depth), "Depth"},
                {nameof(ObservationV2.DepthUnit), "Depth Unit"},
                {nameof(ObservationV2.DataClassification), "Data Classification"},
                {nameof(ObservationV2.ResultValue), "Result Value"},
                {nameof(ObservationV2.ResultUnit), "Result Unit"},
                {nameof(ObservationV2.ResultStatus), "Result Status"},
                {nameof(ObservationV2.ResultGrade), "Result Grade"},
                {nameof(ObservationV2.Medium), "Medium"},
                {nameof(ObservationV2.ActivityID), "Activity ID"},
                {nameof(ObservationV2.ActivityName), "Activity Name"},
                {nameof(ObservationV2.CollectionMethod), "Collection Method"},
                {nameof(ObservationV2.FieldDeviceID), "Field: Device ID"},
                {nameof(ObservationV2.FieldDeviceType), "Field: Device Type"},
                {nameof(ObservationV2.FieldComment), "Field: Comment"},
                {nameof(ObservationV2.LabSpecimenName), "Lab: Specimen Name"},
                {nameof(ObservationV2.LabAnalysisMethod), "Lab: Analysis Method"},
                {nameof(ObservationV2.LabDetectionCondition), "Lab: Detection Condition"},
                {nameof(ObservationV2.LabLimitType), "Lab: Limit Type"},
                {nameof(ObservationV2.LabMDL), "Lab: MDL"},
                {nameof(ObservationV2.LabMRL), "Lab: MRL"},
                {nameof(ObservationV2.LabQualityFlag), "Lab: Quality Flag"},
                {nameof(ObservationV2.LabReceivedDateTime), "Lab: Received DateTime"},
                {nameof(ObservationV2.LabPreparedDateTime), "Lab: Prepared DateTime"},
                {nameof(ObservationV2.LabSampleFraction), "Lab: Sample Fraction"},
                {nameof(ObservationV2.LabFromLaboratory), "Lab: From Laboratory"},
                {nameof(ObservationV2.LabSampleID), "Lab: Sample ID"},
                {nameof(ObservationV2.LabDilutionFactor), "Lab: Dilution Factor"},
                {nameof(ObservationV2.LabComment), "Lab: Comment"},
                {nameof(ObservationV2.QCType), "QC: Type"},
                {nameof(ObservationV2.QCSourceSampleID), "QC: Source Sample ID"},
            };

            writer.WriteLine(CsvSerializer.SerializeToCsv(observations));
        }
    }
}
