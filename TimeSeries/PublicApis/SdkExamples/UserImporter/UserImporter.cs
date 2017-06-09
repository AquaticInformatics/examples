using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using ServiceStack;
using ServiceStack.Logging;
using UserImporter.Helpers;
using UserImporter.Records;

namespace UserImporter
{
    public class UserImporter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly UserSyncContext _context;
        
        public UserImporter(UserSyncContext context)
        {
            _context = context;
        }

        public void Run()
        {
            var users = ReadUsersFromCsv();
            users = UserRecordValidator.ValidateUserList(users);
            
            ImportUsersToAquarius(users);
        }

        private List<UserRecord> ReadUsersFromCsv()
        {
            Log.Info("Reading users from csv file..");

            var reader = new CsvFileReaderWriter<UserRecord>(_context.UsersFile);
            var users = reader.ReadRecords(true).ToList();

            Log.InfoFormat("Successfully read {0} users from csv file.", users.Count);

            return users;
        }

        private void ImportUsersToAquarius(List<UserRecord> users)
        {
            if (users.Count < 1)
            {
                Log.Info("No users to import..");
                return;
            }     
            
            var createCount = 0;
            var updateCount = 0;


            using (var client = AquariusClient.CreateConnectedClient(_context.ApiUrl, _context.Username, _context.Password))
            {
                var aqUsers = GetAqUsers(client);

                Dictionary<string, User> aqUserDictionary = aqUsers.ToDictionary(x => x.LoginName, user => user);

                Log.Info("Importing users into Aquarius...");

                foreach (var user in users)
                {
                    if (aqUserDictionary.ContainsKey(user.Username))
                    {
                        if (UpdateUser(client, user, aqUserDictionary[user.Username]))
                            updateCount++;
                    }
                    else
                    {
                        if (CreateUser(client, user))
                            createCount++;
                    }
                }
            }

            Log.InfoFormat("Successfully created {0} users and updated {1} users.",createCount, updateCount);
        }

        private List<User> GetAqUsers(IAquariusClient client)
        {
            var aqUsers = new List<User>();

            try
            {
                Log.Info("Retrieving existing users from Aquarius...");
                var response = client.ProvisioningClient.Get(new GetUsers());

                Log.InfoFormat("Successfully retrieved {0} current users from Aquarius", response.Results.Count);

                aqUsers = response.Results;
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Failed to retrieve users from Aquarius. Error: {0}", e.Message);
            }
            

            return aqUsers;
        }

        private bool CreateUser(IAquariusClient client, UserRecord userRecord)
        {
            try
            {
                var user = CreateUserByAuthenticationType[userRecord.AuthenticationType](userRecord);

                var createdUser = client.ProvisioningClient.Post(user);
                
                Log.DebugFormat("Created {0} user: {1}", createdUser.AuthenticationType, userRecord.Username);
                return true;
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Failed to create user {0}. Error: {1}", userRecord.Username, e.Message);
                return false;
            }
        }

        private static readonly Dictionary<AuthenticationType, Func<UserRecord, IReturn<User>>>
            CreateUserByAuthenticationType = new Dictionary<AuthenticationType, Func<UserRecord, IReturn<User>>>
            {
                { AuthenticationType.Credentials, UserMapper.GetCredentialsPostUserFromRecord },
                { AuthenticationType.ActiveDirectory, UserMapper.GetActiveDirectoryPostUserFromRecord },
                { AuthenticationType.OpenIdConnect, UserMapper.GetOpenIdConnectPostUserFromRecord },
            };

        private bool UpdateUser(IAquariusClient client, UserRecord userRecord, User aquariusUser)
        {
            try
            {
                var aquariusUserAuthenticationType =
                    CsvAuthenticationTypeConverter.Convert(aquariusUser.AuthenticationType);

                var user = UpdateUserByAuthenticationType[userRecord.AuthenticationType](userRecord, aquariusUser.UniqueId);

                if (userRecord.AuthenticationType != aquariusUserAuthenticationType)
                {
                    SwitchUserAuthenticationMode(client, userRecord, aquariusUser.UniqueId);
                }

                var updatedUser = client.ProvisioningClient.Put(user);

                Log.DebugFormat("Updated {0} user: {1}", updatedUser.AuthenticationType, userRecord.Username);
                return true;
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Failed to update user {0}. Error: {1}", userRecord.Username, e.Message);
                return false;
            }
        }

        private static readonly Dictionary<AuthenticationType, Func<UserRecord, Guid, IReturn<User>>>
            UpdateUserByAuthenticationType = new Dictionary<AuthenticationType, Func<UserRecord, Guid, IReturn<User>>>
            {
                { AuthenticationType.Credentials, UserMapper.GetCredentialsPutUserFromRecord },
                { AuthenticationType.ActiveDirectory, UserMapper.GetActiveDirectoryPutUserFromRecord },
                { AuthenticationType.OpenIdConnect, UserMapper.GetOpenIdConnectPutUserFromRecord },
            };

        private void SwitchUserAuthenticationMode(IAquariusClient client, UserRecord userRecord, Guid uniqueId)
        {
            try
            {
                var userAuth = SwitchUserAuthenticationModeByAuthenticationType[userRecord.AuthenticationType](userRecord, uniqueId);

                var changedUser = client.ProvisioningClient.Put(userAuth);

                Log.DebugFormat("Updated user authentiation to {0} for user: {1}", changedUser.AuthenticationType, userRecord.Username);
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Failed to update users authentication type {0}. Error: {1}", userRecord.Username, e.Message);
            } 
        }

        private static readonly Dictionary<AuthenticationType, Func<UserRecord, Guid, IReturn<User>>>
            SwitchUserAuthenticationModeByAuthenticationType =
                new Dictionary<AuthenticationType, Func<UserRecord, Guid, IReturn<User>>>
                {
                    {
                        AuthenticationType.Credentials, (userRecord, uniqueId) => new PutCredentialsAuth
                        {
                            Password = userRecord.Password,
                            UniqueId = uniqueId
                        }
                    },
                    {
                        AuthenticationType.ActiveDirectory, (userRecord, uniqueId) => new PutActiveDirectoryAuth
                        {
                            UserPrincipalName = userRecord.UserPrincipalName,
                            UniqueId = uniqueId
                        }
                    },
                    {
                        AuthenticationType.OpenIdConnect, (userRecord, uniqueId) => new PutOpenIdConnectAuth
                        {
                            SubjectIdentifier = userRecord.SujectIdentifier,
                            UniqueId = uniqueId
                        }
                    },
                };
    }
}
