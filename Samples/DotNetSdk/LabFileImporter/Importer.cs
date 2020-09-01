using System.Collections.Generic;
using System.IO;
using System.Linq;
using Humanizer;

namespace LabFileImporter
{
    public class Importer
    {
        public Context Context { get; set; }

        public void Import()
        {
            Validate();

            var observations = LoadAll()
                .ToList();
        }

        private void Validate()
        {
            if (!Context.Files.Any())
                throw new ExpectedException($"No files to import. Try setting a /{nameof(Context.Files).Singularize()}= option.");
        }

        private IEnumerable<ObservationV2> LoadAll()
        {
            return Context
                .Files
                .SelectMany(LoadAllObservations);
        }

        private IEnumerable<ObservationV2> LoadAllObservations(string path)
        {
            return new LabFileLoader
                {
                    Context = Context
                }
                .Load(path);
        }

        private static readonly string[] CsvHeaders = new[]
        {
            "Observation ID",
            "Location ID",
            "Observed Property ID",
            "Observed DateTime",
            "Analyzed DateTime",
            "Depth",
            "Depth Unit",
            "Data Classification",
            "Result Value",
            "Result Unit",
            "Result Status",
            "Result Grade",
            "Medium",
            "Activity ID",
            "Activity Name",
            "Collection Method",
            "Field: Device ID",
            "Field: Device Type",
            "Field: Comment",
            "Lab: Specimen Name",
            "Lab: Analysis Method",
            "Lab: Detection Condition",
            "Lab: Limit Type",
            "Lab: MDL",
            "Lab: MRL",
            "Lab: Quality Flag",
            "Lab: Received DateTime",
            "Lab: Prepared DateTime",
            "Lab: Sample Fraction",
            "Lab: From Laboratory",
            "Lab: Sample ID",
            "Lab: Dilution Factor",
            "Lab: Comment",
            "QC: Type",
            "QC: Source Sample ID",
        };
    }
}
