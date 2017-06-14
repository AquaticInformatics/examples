using FileHelpers;
using UserImporter.Helpers;
#pragma warning disable 649

namespace UserImporter.Records
{
    [DelimitedRecord(",")]
    public class UserRecord
    {
        [FieldTrim(TrimMode.Both), FieldQuoted(QuoteMode.OptionalForBoth, MultilineMode.NotAllow)]
        private string _username;

        [FieldTrim(TrimMode.Both), FieldQuoted(QuoteMode.OptionalForBoth, MultilineMode.NotAllow)]
        private string _firstName;

        [FieldTrim(TrimMode.Both), FieldQuoted(QuoteMode.OptionalForBoth, MultilineMode.NotAllow)]
        private string _lastName;

        [FieldTrim(TrimMode.Both), FieldQuoted(QuoteMode.OptionalForBoth, MultilineMode.NotAllow)]
        private string _email;

        [FieldTrim(TrimMode.Both), FieldQuoted(QuoteMode.OptionalForBoth, MultilineMode.NotAllow), FieldConverter(typeof(CsvBoolConverter))]
        private bool _active;

        [FieldTrim(TrimMode.Both), FieldQuoted(QuoteMode.OptionalForBoth, MultilineMode.NotAllow), FieldConverter(typeof(CsvBoolConverter))]
        private bool _canLaunchRdt;

        [FieldTrim(TrimMode.Both), FieldQuoted(QuoteMode.OptionalForBoth, MultilineMode.NotAllow), FieldConverter(typeof(CsvAuthenticationTypeConverter))]
        private AuthenticationType _authenticationType;

        [FieldTrim(TrimMode.Both), FieldQuoted(QuoteMode.OptionalForBoth, MultilineMode.NotAllow)]
        private string _password;

        [FieldTrim(TrimMode.Both), FieldQuoted(QuoteMode.OptionalForBoth, MultilineMode.NotAllow)]
        private string _userPrincipalName;

        [FieldTrim(TrimMode.Both), FieldQuoted(QuoteMode.OptionalForBoth, MultilineMode.NotAllow)]
        private string _subjectIdentifier;

        public string Username => _username;

        public string FirstName => _firstName;

        public string LastName => _lastName;

        public string Email => _email;

        public bool Active => _active;

        public bool CanLaunchRdt => _canLaunchRdt;

        public AuthenticationType AuthenticationType => _authenticationType;

        public string Password => _password;

        public string UserPrincipalName => _userPrincipalName;

        public string SujectIdentifier => _subjectIdentifier;
    }
}
