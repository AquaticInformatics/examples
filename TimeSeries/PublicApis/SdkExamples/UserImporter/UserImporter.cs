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

        private readonly UserImporterContext _context;
        
        public UserImporter(UserImporterContext context)
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
            Log.Info($"Reading users from '{_context.UsersFile}' ...");

            var reader = new CsvFileReaderWriter<UserRecord>(_context.UsersFile);
            var users = reader.ReadRecords(true).ToList();

            Log.Info($"Successfully read {users.Count} users from csv file.");

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


            using (var client = AquariusClient.CreateConnectedClient(_context.Server, _context.Username, _context.Password))
            {
                Log.Info($"Connected to {(client.Provisioning as JsonServiceClient)?.BaseUri} ({client.ServerVersion}) as user='{_context.Username}'.");

                var aqUsers = GetAqUsers(client);

                Dictionary<string, User> aqUserDictionary = aqUsers.ToDictionary(x => x.LoginName, user => user);

                Log.Info("Importing users into Aquarius ...");

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

            Log.Info($"Successfully created {createCount} users and updated {updateCount} users.");
        }

        private List<User> GetAqUsers(IAquariusClient client)
        {
            Log.Info("Retrieving existing users from Aquarius ...");
            var response = client.Provisioning.Get(new GetUsers());

            Log.Info($"Successfully retrieved {response.Results.Count} current users from Aquarius");

            return response.Results;
        }

        private bool CreateUser(IAquariusClient client, UserRecord userRecord)
        {
            try
            {
                var user = CreateUserByAuthenticationType[userRecord.AuthenticationType](userRecord);

                var createdUser = client.Provisioning.Post(user);
                
                Log.Debug($"Created {createdUser.AuthenticationType} user: '{userRecord.Username}'");
                return true;
            }
            catch (Exception exception)
            {
                Log.Error($"Failed to create user '{userRecord.Username}'. Error={exception.Message}");
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

                var updatedUser = client.Provisioning.Put(user);

                Log.Debug($"Updated {updatedUser.AuthenticationType} user: '{userRecord.Username}'");
                return true;
            }
            catch (Exception exception)
            {
                Log.Error($"Failed to update user '{userRecord.Username}'. Error={exception.Message}");
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

                var changedUser = client.Provisioning.Put(userAuth);

                Log.Debug($"Changed user '{userRecord.Username}' to authentication type {changedUser.AuthenticationType}.");
            }
            catch (Exception exception)
            {
                Log.Error($"Failed to change user '{userRecord.Username}' to authentication type {userRecord.AuthenticationType}. Error: {exception.Message}");
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
                            SubjectIdentifier = userRecord.SubjectIdentifier,
                            UniqueId = uniqueId
                        }
                    },
                };
    }
}
