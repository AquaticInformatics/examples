using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UserImporter.Records;

namespace UserImporter
{
    public class UserImporterContext
    {
        private class Option
        {
            public string Key { get; set; }
            public string Description { get; set; }
            public Action<string> Setter { get; set; }
            public Func<string> Getter { get; set; }

            public string UsageText()
            {
                var defaultValue = Getter();

                if (!string.IsNullOrEmpty(defaultValue))
                    defaultValue = $" [default: {defaultValue}]";

                return $"{Key,-20} {Description}{defaultValue}";
            }
        }

        private static readonly Regex ArgRegex = new Regex(@"^([/-])(?<key>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);

        public string Server { get; private set; } = GetAppSettingOrDefault("server", "localhost");
        public string Username { get; private set; } = GetAppSettingOrDefault("username", "admin");
        public string Password { get; private set; } = GetAppSettingOrDefault("password", "admin");
        public string UsersFile { get; private set; } = GetAppSettingOrDefault("usersFile", @".\users.csv");

        private static string GetAppSettingOrDefault(string key, string defaultValue = null)
        {
            var value = ConfigurationManager.AppSettings[key];

            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        public UserImporterContext(IEnumerable<string> arguments)
        {
            var options = new[]
            {
                new Option {Key = "Server", Setter = value => Server = value, Getter = () => Server, Description = "The AQTS app server"},
                new Option {Key = "Username", Setter = value => Username = value, Getter = () => Username, Description = "The AQTS username credentials"},
                new Option {Key = "Password", Setter = value => Password = value, Getter = () => Password, Description = "The AQTS password credentials"},
                new Option {Key = "UsersFile", Setter = value => UsersFile = value, Getter = () => UsersFile, Description = "Path to the users CSV file to import"},
            };

            var usageMessage = $"Imports users from a CSV file into an AQTS system using the Provisioning API."
                                   + $"\n\nusage: {Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)} [-option=value] ..."
                                   + $"\n\nSupported -option=value settings (/option=value works too):\n\n  -{string.Join("\n  -", options.Select(o => o.UsageText()))}"
                                   + $"\n\nExample CSV content:\n\n{CreateSampleCsvOutput()}"
                                   + $"\n\nCSV fields follow these rules:\n"
                                   + $"\n- The first line in the file is assumed to be a header line and is skipped."
                                   + $"\n- Leading/trailing space around fields is trimmed."
                                   + $"\n- String fields only require quoting if they contain commas, otherwise double-quotes are optional."
                                   + $"\n- Boolean fields accept true or false. (case-insensitive)"
                                   + $"\n- Authentication type is one of: {string.Join(", ", Enum.GetNames(typeof(AuthenticationType)).Where(name => name != "Unknown"))} (case-insensitive)"
                                   ;

            foreach (var arg in arguments)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
                    if (File.Exists(arg) && ".csv".Equals(Path.GetExtension(arg), StringComparison.InvariantCultureIgnoreCase))
                    {
                        UsersFile = arg;
                        continue;
                    }

                    throw new ExpectedException($"Unknown argument: {arg}\n\n{usageMessage}");
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                var option =
                    options.FirstOrDefault(o => o.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

                if (option == null)
                {
                    throw new ExpectedException($"Unknown -option=value: {arg}\n\n{usageMessage}");
                }

                option.Setter(value);
            }

            if (string.IsNullOrEmpty(UsersFile))
                throw new ExpectedException($"No user CSV specified.");
        }

        private static string CreateSampleCsvOutput()
        {
            return @"Username, FirstName, LastName, Email,            Active, CanLaunchRdt, AuthenticationType, Password, UserPrincipalName, SubjectIdentifier,     CanConfigureSystem
fredcred, fred,      cred,     fred@derf.com,    true,   true,         Credentials,        sekret,   ,                  ,                      false
fredwin,  fred,      win,      fred@win.com,     true,   true,         ActiveDirectory,    ,         fred@win.com,      ,                      false
fredopen, fred,      openid,   ""fred@gmail.com"", true,   true,         OpenIdConnect,      ,         ,                  113611963171978735131, false";
        }
    }
}
