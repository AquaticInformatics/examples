using System;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using UserImporter.Records;

namespace UserImporter
{
    public static class UserMapper
    {
        public static PutCredentialsUser GetCredentialsPutUserFromRecord(UserRecord userRecord, Guid uniqueIdentifier)
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
                UniqueId = uniqueIdentifier
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

        public static PutOpenIdConnectUser GetOpenIdConnectPutUserFromRecord(UserRecord userRecord, Guid uniqueIdentifier)
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
                SubjectIdentifier = userRecord.SubjectIdentifier,
                UniqueId = uniqueIdentifier
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
                SubjectIdentifier = userRecord.SubjectIdentifier
            };

            return user;
        }

        public static PutActiveDirectoryUser GetActiveDirectoryPutUserFromRecord(UserRecord userRecord, Guid uniqueIdentifier)
        {
            var user = new PutActiveDirectoryUser
            {
                Active = userRecord.Active,
                CanConfigureSystem = userRecord.CanConfigureSystem,
                CanLaunchRatingDevelopmentToolbox = userRecord.CanLaunchRdt,
                Email = userRecord.Email,
                FirstName = userRecord.FirstName,
                LastName = userRecord.LastName,
                LoginName = userRecord.Username,
                UserPrincipalName = userRecord.UserPrincipalName,
                UniqueId = uniqueIdentifier
            };

            return user;
        }

        public static PostActiveDirectoryUser GetActiveDirectoryPostUserFromRecord(UserRecord userRecord)
        {
            var user = new PostActiveDirectoryUser
            {
                Active = userRecord.Active,
                CanConfigureSystem = userRecord.CanConfigureSystem,
                CanLaunchRatingDevelopmentToolbox = userRecord.CanLaunchRdt,
                Email = userRecord.Email,
                FirstName = userRecord.FirstName,
                LastName = userRecord.LastName,
                LoginName = userRecord.Username,
                UserPrincipalName = userRecord.UserPrincipalName
            };

            return user;
        }
    }
}
