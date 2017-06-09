using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Reflection;
using ServiceStack.Logging;
using UserImporter.Helpers;

namespace UserImporter
{
    public class UserSyncContext
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private NameValueCollection _appSettings;
        public string ApiUrl { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string UsersFile { get; private set; }

        public UserSyncContext(IEnumerable<string> arguments)
        {
            BuildAppSettings(arguments);
            GetBaseUrl();
            GetUsername();
            GetPassword();
            GetUsersFile();
            //GetRolesFile();
        }

        private void BuildAppSettings(IEnumerable<string> arguments)
        {
            _appSettings = ConfigurationManager.AppSettings;
            OverrideAppSettings(arguments);
        }

        private void OverrideAppSettings(IEnumerable<string> arguments)
        {
            foreach (var argument in arguments)
            {
                try
                {
                    var parsedArgument = CommandLineArgumentParser.Parse(argument);

                    _appSettings[parsedArgument.Key] = parsedArgument.Value;                  
                }
                catch (ArgumentException ex)
                {
                    Log.Warn(ex.Message);
                }
            }
        }

        private void GetBaseUrl()
        {
            ApiUrl = _appSettings["server"] + "/AQUARIUS/Provisioning/v1";
            Log.DebugFormat("ApiURL={0}", ApiUrl);
            ThrowExceptionIfMissing("server", ApiUrl);
        }

        private void GetUsername()
        {
            Username = _appSettings["username"];
            ThrowExceptionIfMissing("username", Username);
        }

        private void GetPassword()
        {
            Password = _appSettings["password"];
            ThrowExceptionIfMissing("password", Password);
        }

        private void GetUsersFile()
        {
            UsersFile = _appSettings["usersFile"];
            ThrowExceptionIfMissing("usersFile", UsersFile);
        }
     
        private static void ThrowExceptionIfMissing(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return;
            }

            var errorMessage = string.Format("Value for the application setting '{0}' is missing.", key);
            throw new MissingFieldException(errorMessage);
        }

        private static void ThrowExceptionIfWrong(string key, string values)
        {
            var errorMessage = string.Format("Value for the application setting '{0}' is incorrect. Must be one of {1}", key, values);
            throw new MissingFieldException(errorMessage);
        }
    }
}
