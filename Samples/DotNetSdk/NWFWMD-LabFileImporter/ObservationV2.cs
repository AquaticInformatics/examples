namespace NWFWMDLabFileImporter
{
    public class ObservationV2
    {
        public string ObservationID { get; set; }
        public string LocationID { get; set; }
        public string ObservedPropertyID { get; set; }
        public string ObservedDateTime { get; set; }
        public string AnalyzedDateTime { get; set; }
        public string Depth { get; set; }
        public string DepthUnit { get; set; }
        public string DataClassification { get; set; }
        public string ResultValue { get; set; }
        public string ResultUnit { get; set; }
        public string ResultStatus { get; set; }
        public string ResultGrade { get; set; }
        public string Medium { get; set; }
        public string ActivityID { get; set; }
        public string ActivityName { get; set; }
        public string CollectionMethod { get; set; }
        public string FieldDeviceID { get; set; }
        public string FieldDeviceType { get; set; }
        public string FieldComment { get; set; }
        public string LabSpecimenName { get; set; }
        public string LabAnalysisMethod { get; set; }
        public string LabDetectionCondition { get; set; }
        public string LabLimitType { get; set; }
        public string LabMDL { get; set; }
        public string LabMRL { get; set; }
        public string LabQualityFlag { get; set; }
        public string LabReceivedDateTime { get; set; }
        public string LabPreparedDateTime { get; set; }
        public string LabSampleFraction { get; set; }
        public string LabFromLaboratory { get; set; }
        public string LabSampleID { get; set; }
        public string LabDilutionFactor { get; set; }
        public string LabComment { get; set; }
        public string QCType { get; set; }
        public string QCSourceSampleID { get; set; }
        public string EARequestID { get; set; }
        public string EASampler { get; set; }
    }
}
