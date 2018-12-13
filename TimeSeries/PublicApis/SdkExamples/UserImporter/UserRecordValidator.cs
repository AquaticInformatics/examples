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
                    Log.Error($"User record for '{record.Username}' is not fully formed. Skipping.");
                    continue;
                }

                if (record.AuthenticationType == AuthenticationType.Unknown)
                {
                    Log.Error($"User record for '{record.Username}' does not have a valid AuthenticationType. Skipping.");
                    continue;
                }
                
                if (record.AuthenticationType == AuthenticationType.Credentials &&
                    string.IsNullOrEmpty(record.Password))
                {
                    Log.Error($"User record for '{record.Username}' is {record.AuthenticationType}, but no Password provided. Skipping.");
                    continue;
                }
                
                if (record.AuthenticationType == AuthenticationType.ActiveDirectory &&
                        string.IsNullOrEmpty(record.UserPrincipalName))
                {
                    Log.Error($"User record for '{record.Username}' is {record.AuthenticationType}, but no UserPrincipleName provided. Skipping.");
                    continue;
                }
                
                if (record.AuthenticationType == AuthenticationType.OpenIdConnect &&
                           string.IsNullOrEmpty(record.SubjectIdentifier))
                {
                    Log.Error($"User record for '{record.Username}' is {record.AuthenticationType}, but no SubjectIdentifier provided. Skipping.");
                    continue;
                }          

                if (userDict.ContainsKey(record.Username))
                {
                    Log.Error($"Duplicate username detected: '{record.Username}'. Skipping.");
                    continue;
                }

                userDict.Add(record.Username, record);
            }

            Log.Info($"Validation finished. {userDict.Count} user records remain.");

            return userDict.Values.ToList();
        }
    }
}
