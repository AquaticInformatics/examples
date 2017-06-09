using System;
using System.Collections.Generic;
using FileHelpers;
using UserImporter.Records;

namespace UserImporter.Helpers
{
    public class CsvAuthenticationTypeConverter : ConverterBase
    {
        public override object StringToField(string from)
        {
            return Convert(from);
        }

        public static AuthenticationType Convert(string from)
        {
            AuthenticationType authenticationType;
            if (Enum.TryParse(from, true, out authenticationType) && authenticationType != AuthenticationType.Unknown)
                return authenticationType;

            if (Aliases.TryGetValue(from, out authenticationType))
                return authenticationType;

            throw new ConvertException(from, typeof(AuthenticationType), "Input string '" + from + "'is not a valid authentication type");
        }

        private static readonly Dictionary<string, AuthenticationType> Aliases =
            new Dictionary<string, AuthenticationType>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"Credentials", AuthenticationType.Credentials},
                {"Active Directory", AuthenticationType.ActiveDirectory},
                {"OpenID Connect", AuthenticationType.OpenIdConnect},
            };
    }
}
