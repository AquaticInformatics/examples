## ExampleFieldDataPlugins.sln

[**Download** this project folder](https://minhaskamal.github.io/DownGit/#/home?url=https:%2F%2Fgithub.com%2FAquaticInformatics%2FExamples%2Ftree%2Fmaster%2FTimeSeries%2FPublicApis%2FFieldDataPlugins)

Requires: Visual Studio 2017+ (Community Edition is fine)

The ExampleFieldDataPlugins solution includes some example plugins for the AQ-TS field data plugin framework.  The core assembly required to implement a field data plugin is installed with your AQ-TS server, located at
%Program Files%\AquaticInformatics\AQUARIUS Server\FieldDataPlugins\Library

All plugins implement the interface `Server.BusinessInterfaces.FieldDataPluginCore.IFieldDataPlugin`

### Documentation

A developer guide is available [here.](docs/AQUARIUSDeveloperGuideFieldDataPluginFramework.pdf)

### Examples

The `StageDischargePlugin` example plugin demonstrates how to write a plugin to import simple stage-discharge pairs;

The `ManualGaugingPlugin` example plugin demonstrates how to write a plugin to import point velocities;

The example plugins do not provide an implementation of how to read and parse a field data file.  The functionality is stubbed out in FileReader.cs.  It is up to you to define the structure and format of the field data file.  Example file formats are CSV, JSON, XML, TXT.  Please note that the input to a plugin is a single file; if you need to provide multiple files as input to your plugin, consider using ZIP as your field data file format.

### Logging

Every method signature in the IFieldDataPlugin interface includes a reference to a Server.BusinessInterfaces.FieldDataPluginCore.ILog object.

Log messages are written to the `FieldDataPluginFramework.log`, which can be found on the server at `%Program Data%\Aquatic Informatics\AQUARIUS\Logs`.

### Best Practices

Please consider the following best practices when writing your plugin:
- Plugins run in a sandbox environment - the framework runs plugins locally, so plugins should not access the network;
- Plugins must be thread safe;
- Plugins should only contain managed code - AI will not provide developer support for plugins containing unmanaged code;
- Plugins should be able to process a field data file in less than one second.

### Be prepared to handle any type of file content!
- When a file is uploaded to AQUARIUS Time-Series through a browser, every plugin tries to inspect the file to see if it can be parsed.
- When a user uploads `kittens.jpg`, `myreport.pdf`, or `IMG005.MOV` as an attachment, your plugin will be given a chance to parse that content.
- each plugin will try to parse it as field visit file.
- Your plugin should be robust enough to survive contact with *any type of file content* including binary data.
- Any content not understood by your plugin should cause it to `return ParseFileResult.CannotParse();` and give the next plugin a chance to parse it.

### Installing a plugin

Plugins are installed on every AQTS server in the folder %ProgramFiles%\AquaticInformatics\AQUARIUS Server\FieldDataPlugins.

Each plugin and its dependencies are contained in its own sub-folder in the FieldDataPlugins folder.  For example, your plugin, YourPlugin.dll, and its dependencies will be installed on the server in the folder %ProgramFiles%\AquaticInformatics\AQUARIUS Server\FieldDataPlugins\YourPlugin.

However, the field data plugin framework will not run a plugin until it is registered.

### Registering a plugin

Use `GET /Provisioning/v1/fielddataplugins` to get a list of registered plugins.

Use `POST /Provisioning/v1/fielddataplugins` to register a field data plugin
- `PluginFolderName` is the name of the sub-folder in the %ProgramFiles%\AquaticInformatics\AQUARIUS Server\FieldDataPlugins\ folder that contains your plugin and its dependencies;
- `AssemblyQualifiedTypeName` is described [here by MSDN](https://docs.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/specifying-fully-qualified-type-names).  An example of an assembly qualified name is, YourPlugin, CustomerProjectNamespace.YourPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null.

Use `DELETE /Provisioning/v1/fielddataplugins/{UniqueId}` to unregister a field data plugin
- The `UniqueId` is a unique string value identifying your field data plugin.  Each plugin is assigned a UniqueId when it is registered.  Use `GET /Provisioning/v1/fielddataplugins` to look-up the UniqueId of any plugin.