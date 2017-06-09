using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ServiceStack.Logging;
using UserImporter.Records;

namespace UserImporter
{
    public class UserRecordValidator
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static List<UserRecord> ValidateUserList(List<UserRecord> userRecords)
        {
            Log.Info("Validating the users read from csv ...");
            var userDict = new Dictionary<string, UserRecord>();

            foreach (var record in userRecords)
            {
                if (string.IsNullOrEmpty(record.Username) ||
                    string.IsNullOrEmpty(record.FirstName) ||
                    string.IsNullOrEmpty(record.LastName) ||
                    string.IsNullOrEmpty(record.Email))
                {
                    Log.ErrorFormat("User record for {0} is not fully formed. Skipping.", record.Username);
                    continue;
                }

                if (record.AuthenticationType == AuthenticationType.Unknown)
                {
                    Log.ErrorFormat("User record for {0} does not have a valid AuthenticationType. Skipping.", record.Username);
                    continue;
                }
                
                if (record.AuthenticationType == AuthenticationType.Credentials &&
                    string.IsNullOrEmpty(record.Password))
                {
                    Log.ErrorFormat("User record for {0} is Credentials, but no password provided. Skipping.", record.Username);
                    continue;
                }
                
                if (record.AuthenticationType == AuthenticationType.ActiveDirectory &&
                        string.IsNullOrEmpty(record.UserPrincipalName))
                {
                    Log.ErrorFormat("User record for {0} is ActiveDirectory, but no UserPrincipleName provided. Skipping.", record.Username);
                    continue;
                }
                
                if (record.AuthenticationType == AuthenticationType.OpenIdConnect &&
                           string.IsNullOrEmpty(record.SujectIdentifier))
                {
                    Log.ErrorFormat(
                        "User record for {0} is OpenIdConnect, but no SubjectIdentifier provided. Skipping.",
                        record.Username);
                    continue;
                }          

                if (userDict.ContainsKey(record.Username))
                {
                    Log.ErrorFormat("Duplicate username detected: {0}. Skipping.", record.Username);
                    continue;
                }

                userDict.Add(record.Username, record);
            }

            Log.InfoFormat("Validation finished. {0} user records remain.", userDict.Count);

            return userDict.Values.ToList();
        }
    }
}
