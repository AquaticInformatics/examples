using FileHelpers;
using UserImporter.Helpers;

namespace UserImporter.Records
{
    [DelimitedRecord(",")]
    public class UserRecord
    {
        [FieldQuoted(MultilineMode.NotAllow)]
        private readonly string _username;

        [FieldQuoted(MultilineMode.NotAllow)]
        private readonly string _firstName;

        [FieldQuoted(MultilineMode.NotAllow)]
        private readonly string _lastName;

        [FieldQuoted(MultilineMode.NotAllow)]
        private readonly string _email;

        [FieldQuoted(MultilineMode.NotAllow), FieldConverter(typeof(CsvBoolConverter))]
        private readonly bool _active;

        [FieldQuoted(MultilineMode.NotAllow), FieldConverter(typeof(CsvBoolConverter))]
        private readonly bool _canLaunchRdt;

        [FieldQuoted(MultilineMode.NotAllow)]
        private readonly string _authenticationType;

        [FieldQuoted(MultilineMode.NotAllow)]
        private readonly string _password;

        [FieldQuoted(MultilineMode.NotAllow)]
        private readonly string _userPrincipalName;

        [FieldQuoted(MultilineMode.NotAllow)]
        private readonly string _subjectIdentifier;

        public UserRecord() { }
        
        public UserRecord(string username, string firstName, string lastName, string email, bool active,
            bool canLaunchRdt, string authenticationType, string password, string userPrincipalName, string subjectIdentifier)
        {
            _username = username;
            _firstName = firstName;
            _lastName = lastName;
            _email = email;
            _password = password;
            _active = active;
            _canLaunchRdt = canLaunchRdt;
            _authenticationType = authenticationType;
            _userPrincipalName = userPrincipalName;
            _subjectIdentifier = subjectIdentifier;
        }

        public string Username { get { return _username;} }

        public string FirstName { get { return _firstName; } }

        public string LastName { get { return _lastName; } }

        public string Email { get { return _email; } }

        public bool Active { get { return _active;} }

        public bool CanLaunchRdt { get { return _canLaunchRdt; } }

        public string AuthenticationType { get { return _authenticationType; } }

        public string Password { get { return _password; } }

        public string UserPrincipalName { get { return _userPrincipalName; } }

        public string SujectIdentifier { get { return _subjectIdentifier; } }
    }
}
