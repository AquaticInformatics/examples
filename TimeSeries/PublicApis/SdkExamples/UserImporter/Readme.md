# UserImporter

The `UserImporter` example app can be used to import a number of AQTS users from a CSV file.

All three AQTS authentication methods are supported:
- Aquarius credentials
- Active Directory authentication
- OpenId Connect authentication

The basic logic performed by the app is:
- Connect to the AQTS server
- Retrieve the current list of users in the system
- Read the CSV to get the user records to create/modify
- New users will be added to AQTS
- Existing users will be updated. This includes changing the authentication mode for an existing user.

Example CSV format consumed by the `UserImporter` app is:
```csv
Username, FirstName, LastName, Email,            Active, CanLaunchRdt, AuthenticationType, Password, UserPrincipalName, SubjectIdentifier,     CanConfigureSystem
fredcred, fred,      cred,     fred@derf.com,    true,   true,         Credentials,        sekret,   ,                  ,                      false
fredwin,  fred,      win,      fred@win.com,     true,   true,         ActiveDirectory,    ,         fred@win.com,      ,                      false
fredopen, fred,      openid,   "fred@gmail.com", true,   true,         OpenIdConnect,      ,         ,                  113611963171978735131, false
```

Key concepts demonstrated:
- Using the `GET /Provisioning/v1/users` operation to get the currently configured users from a system.
- Using the `POST /Provisioning/v1/users/{authenticationType}` operations to create users with a specific authentication type
- Using the `PUT /Provisioning/v1/users/{authenticationType}/{userUniqueId}` operations to update existing users properties
- Using the `PUT /Provisioning/v1/users/{userUniqueId}/{authenticationType}` operations to change an existing user from one authentication type to another
