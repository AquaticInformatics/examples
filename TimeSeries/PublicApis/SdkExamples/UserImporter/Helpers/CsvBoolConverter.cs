using FileHelpers;

namespace UserImporter.Helpers
{
    public class CsvBoolConverter : ConverterBase
    {
        public override object StringToField(string from)
        {
            bool b;
            if (bool.TryParse(from, out b))
                return b;

            throw new ConvertException(from, typeof(bool), "Input string '" + from + "'is not a valid true/false formatted string");
        }
    }
}

