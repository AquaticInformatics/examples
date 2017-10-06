# PluginTester - No AQTS Server needed!

The `PluginTester.exe` console app allows you to run your field data plugin outside of the AQUARIUS Time-Series server environment, for easier debugging and validation.

- Can be run from CMD.EXE, PowerShell, or a bash shell.
- Can be run from within Visual Studio, to allow step-by=step debugging of you plugin.
- An exit code of 0 means the file was successfully parsed by the plugin.
- An exit code of 1 means "something went wrong".
- Everything gets logged to `PluginTester.log`
- Any appended results from the plugin can be saved to a JSON file.

## Usage

```
Parse a file using a field data plugin, logging the results.

usage: PluginTester [-option=value] ...

Supported -option=value settings (/option=value works too):

  -Plugin               Path to the plugin assembly to debug
  -Data                 Path to the data file to be parsed
  -Location             Optional location identifier context
  -Json                 Optional path to write the appended results as JSON
```

### Logging

The tester uses `log4net` to log to both the console and to the `PluginTester.log` file.

Log statements from the tester itself are easily distinguished from log statements from the plugin being tested.

### Using PluginTester for integration tests

You can leverage two features of `PluginTester` to build an automated test suite for your plugin.

1. An exit code of 0 means "The plugin parsed the file".

Any other exit code means something went wrong. Use the exit code to determine if the file was parsed.

```sh
$ PluginTester.exe -Plugin=MyPlugin.dll -Data=data.csv -Json=results.json || echo "Did not parser data.csv"
```

2. Saving the appended results to JSON should always yield the identical output.

```sh
#!/bin/bash

# Helper function
exit_abort () {
    [ ! -z "$1" ] && echo ERROR: "$1"
    echo
    echo 'ABORTED!'
    echo
    exit $ERRCODE
}

PluginTester=../some/path/PluginTester.exe
PluginPath=some/other/path/MyPlugin.dll
DataPath=data.csv
JsonPath=results.json
ExpectedResultsPath=some/path/expected.json

$PluginTester -Plugin=$pluginPath -Data=$DataPath -Json=$JsonPath || exit_abort "Can't parse $DataPath"
cmp $JsonPath $ExpectedResultsPath || exit_abort "Expected output did not match."
```

### Debugging from Visual Studio

Use the `PluginTest.exe` to debug your plugin from within Visual Studio.

1. Open your plugin's **Properties** page
2. Select the **Debug** tab
3. Select **Start external program:** as the start action and browse to `PluginTester.exe`
4. Enter the **Command line arguments:** to launch your plugin

```
/Plugin=<yourPluginAssembly>.dll /Data=a\path\to\sometestfile.ext
```

The `/Plugin=` argument can be the filename of your plugin assembly, without any folder. The default working directory for a start action is the bin folder containing your plugin.

5. Set a breakpoint in your plugin's `ParseFile()` methods.
6. Select your plugin project in Solution Explorer and select **"Debug | Start new instance"**
7. Now you're debugging your plugin!

### Limitations

The tester doesn't fully emulate the plugin framework. It simply exercises the `IFieldDataPlugin` interface and collects the data your plugin tries to append.

- The AQTS framework will perform extensive validation on the data being appended. But the tester doesn't (and can't) perform any of that validation.

#### My plugin seems to run fine in the tester. Why won't my plugin work on AQTS?

When `PluginTester` says "Yup" but AQTS says "Nope" to a file, usually that means a data validation error. Check the `FieldDataPluginFramework.log` on your AQTS server for details.

If the log file doesn't contain an explaination why the data won't upload:
- Use `PluginTester /Json=path.json` option to save the appended data in JSON format.
- Send the JSON file to the SupportTeam @ AquaticInformatics and we'll take a deeper look.

