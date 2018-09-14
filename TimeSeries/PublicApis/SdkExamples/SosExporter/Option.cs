using System;

namespace SosExporter
{
    public class Option
    {
        public string Key { get; set; }
        public string Description { get; set; }
        public Action<string> Setter { get; set; }
        public Func<string> Getter { get; set; }

        public string UsageText()
        {
            if (string.IsNullOrEmpty(Key) && string.IsNullOrEmpty(Description))
                return string.Empty; // Omits a blank line

            var key = string.IsNullOrEmpty(Key) ? SeparatorLine : "-" + Key.PadRight(KeyWidth);

            var defaultValue = Getter != null ? Getter() : string.Empty;

            if (!string.IsNullOrEmpty(defaultValue))
                defaultValue = $" [default: {defaultValue}]";

            return $"{key} {Description}{defaultValue}";
        }

        private const int KeyWidth = 27;
        private static readonly string SeparatorLine = string.Empty.PadRight(KeyWidth + 1, '=');
    }
}
