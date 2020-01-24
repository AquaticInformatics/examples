using System.Text.RegularExpressions;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using UserImporter.Records;

namespace UserImporter
{
    public static class UserMapper
    {
        public static PutCredentialsUser GetCredentialsPutUserFromRecord(UserRecord userRecord, User existingUser)
        {
            var postUser = new PutCredentialsUser
            {
                Active = userRecord.Active,
                CanConfigureSystem = userRecord.CanConfigureSystem,
                CanLaunchRatingDevelopmentToolbox = userRecord.CanLaunchRdt,
                Email = userRecord.Email,
                FirstName = userRecord.FirstName,
                LastName = userRecord.LastName,
                LoginName = userRecord.Username,
                Password = userRecord.Password,
                UniqueId = existingUser.UniqueId
            };

            return postUser;
        }

        public static PostCredentialsUser GetCredentialsPostUserFromRecord(UserRecord userRecord)
        {
            var postUser = new PostCredentialsUser
            {
                Active = userRecord.Active,
                CanConfigureSystem = userRecord.CanConfigureSystem,
                CanLaunchRatingDevelopmentToolbox = userRecord.CanLaunchRdt,
                Email = userRecord.Email,
                FirstName = userRecord.FirstName,
                LastName = userRecord.LastName,
                LoginName = userRecord.Username,
                Password = userRecord.Password,
            };

            return postUser;
        }

        public static PutOpenIdConnectUser GetOpenIdConnectPutUserFromRecord(UserRecord userRecord, User existingUser)
        {         
            var user = new PutOpenIdConnectUser
            {
                Active = userRecord.Active,
                CanConfigureSystem = userRecord.CanConfigureSystem,
                CanLaunchRatingDevelopmentToolbox = userRecord.CanLaunchRdt,
                Email = userRecord.Email,
                FirstName = userRecord.FirstName,
                LastName = userRecord.LastName,
                LoginName = userRecord.Username,
                Identifier = userRecord.SubjectIdentifier,
                UniqueId = existingUser.UniqueId
            };

            return user;
        }

        public static PostOpenIdConnectUser GetOpenIdConnectPostUserFromRecord(UserRecord userRecord)
        {
            var user = new PostOpenIdConnectUser
            {
                Active = userRecord.Active,
                CanConfigureSystem = userRecord.CanConfigureSystem,
                CanLaunchRatingDevelopmentToolbox = userRecord.CanLaunchRdt,
                Email = userRecord.Email,
                FirstName = userRecord.FirstName,
                LastName = userRecord.LastName,
                LoginName = userRecord.Username,
                Identifier = userRecord.SubjectIdentifier
            };

            return user;
        }

        public static PutActiveDirectoryUser GetActiveDirectoryPutUserFromRecord(UserRecord userRecord, User existingUser)
        {
            var (userPrincipalName, activeDirectorySid) = ParseSidOrUpn(userRecord.UserPrincipalName);

            var user = new PutActiveDirectoryUser
            {
                Active = userRecord.Active,
                CanConfigureSystem = userRecord.CanConfigureSystem,
                CanLaunchRatingDevelopmentToolbox = userRecord.CanLaunchRdt,
                Email = userRecord.Email,
                FirstName = userRecord.FirstName,
                LastName = userRecord.LastName,
                LoginName = userRecord.Username,
                UserPrincipalName = userPrincipalName,
                ActiveDirectorySid = activeDirectorySid,
                UniqueId = existingUser.UniqueId
            };

            return user;
        }

        public static PostActiveDirectoryUser GetActiveDirectoryPostUserFromRecord(UserRecord userRecord)
        {
            var (userPrincipalName, activeDirectorySid) = ParseSidOrUpn(userRecord.UserPrincipalName);

            var user = new PostActiveDirectoryUser
            {
                Active = userRecord.Active,
                CanConfigureSystem = userRecord.CanConfigureSystem,
                CanLaunchRatingDevelopmentToolbox = userRecord.CanLaunchRdt,
                Email = userRecord.Email,
                FirstName = userRecord.FirstName,
                LastName = userRecord.LastName,
                LoginName = userRecord.Username,
                UserPrincipalName = userPrincipalName,
                ActiveDirectorySid = activeDirectorySid,
            };

            return user;
        }

        private static (string UserPrincipalName, string ActiveDirectorySid) ParseSidOrUpn(string text)
        {
            var isSid = SidRegex.IsMatch(text);
            var userPrincipalName = isSid ? null : text;
            var activeDirectorySid = isSid ? text : null;

            return (userPrincipalName, activeDirectorySid);
        }

        private static readonly Regex SidRegex = new Regex(@"^S(-\d+)+$");
    }
}
