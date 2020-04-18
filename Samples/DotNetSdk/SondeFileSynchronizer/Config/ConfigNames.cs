
namespace SondeFileSynchronizer.Config
{
    public static class ConfigNames
    {
        public const string SondeFileFolder = "SondeFileFolder";
        public const string ArchiveFolder = "ArchiveFolder";

        public const string SamplesApiBaseUrl = "SamplesApiBaseUrl";
        public const string SamplesAuthToken = "SamplesAuthToken";
        public const string DefaultUtcOffset = "DefaultUtcOffset";

        public const string MappingSection = "Mapping_Section";
        public const string HeaderMappingSection = "Header_" + MappingSection;
        public const string HeaderPropertyIdMappingSection = "Header_PropertyId_" + MappingSection;
        public const string PropertyIdUnitMappingSection = "PropertyId_Unit_" + MappingSection;

        public const string PlaceHolderYourAccount = "your_account";
        public const string PlaceHolderYourToken = "your_samples_auth_token";
    }
}
