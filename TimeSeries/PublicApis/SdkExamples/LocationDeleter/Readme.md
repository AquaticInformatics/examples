# LocationDeleter

![Delete all the things](./delete-all-the-things.jpg)

Download the [latest LocationDeleter.exe release here](../../../../../../releases/latest)

The `LocationDeleter` tool is a standalone .NET console utility for deleting locations, time-series, and/or field visits from AQTS app servers.

The `LocationDeleter` tool is not intended to be used on production systems. This tool exists to help administrators and integrators when configuring systems during initial setup, or when working with test systems to validate items created via the Provisioning API.

## Features

- Can delete locations by identifier (case-insensitive) or by unique ID (for 201x app servers only). Publish API wildcard pattern matching is supported.
- Can delete time-series by identifier or by unique ID.
- Can delete field visits before or after a given date.
- Everything gets logged to `LocationDeleter.log`
- Can delete multiple locations in one command.
- The tools tells you how many time-series, rating models, and/or field visits will be deleted if you proceed with the delete operation.
- The `/DryRun=true` or `/N` shortcut will not delete anything, but will give you a summary of what would have been deleted.
- By default, you must confirm each delete operation at the console. Use `/y` or `/SkipConfirmation=true` to disable the prompt.
- If you set `/RecreateLocations=true`, then each deleted location will be recreated with the same name, properties, and extended attributes. This is helpful to quickly 'reset' a location back to an empty state during testing. Automatically recreating locations is not supported for AQTS 3.X app servers.

## You've backed up your system AND tested your restore procedure, right?

Of course you have. Good for you!

As a reminder to those in a hurry:

- An untested restore procedure is equivalent to no-backup at all. Assume the worst.
- For AQTS 3.X systems, you only need to test your database restore procedure.
- For AQTS 201x systems, you need test your **combined database and BLOB storage** restore procedure. If you only test your database restore procedure, you may lose any attachments in BLOB storage.

The actions performed by `LocationDeleter` are permanent, so if you lose something important, that's on you, not us.

## Requirements

- The .NET 4.7 runtime is required, which is pre-installed on all Windows 10 and Windows Server 2016 systems, and on nearly all up-to-date Windows 7 and Windows Server 2008 systems.
- No installer is needed. It is just a single .EXE which can be run from any folder.

## Logged output

All activity is logged in `LocationDelete.log` files, created in the same directory as the EXE.
Log output from previous runs is also retained, so this log should serve as a useful reference for what was deleted.

## Use the `/DryRun=True` or `/N` option so see what would get deleted

The `/DryRun=True` option is useful for safely confirming what would get deleted if you said "yes" to all the confirmation prompts.

So `LocationDeleter * /N` will end up logging all the locations, and a summary of time-series, field visits, rating models, and attachments per location, without changing any data.

```cmd
C:\> LocationDeleter /Server=doug-vm2012r2 * /N

09:57:17.081 INFO  - Connecting LocationDeleter v1.0.0.0 to doug-vm2012r2 ...
09:57:20.373 INFO  - Connected to doug-vm2012r2 (2018.1.98.0)
09:57:20.995 INFO  - DryRun: SchmidtKits - Schmidtsilano has 14 time-series (5 derived, 9 basic), 1 threshold, 0 rating models rating models, 9 field visits, 0 sensors and gauges, and 30 attachments.
09:57:21.044 INFO  - DryRun: RiverSchmiver - Muh Creek has 9 time-series (3 derived, 6 basic), 0 thresholds, 1 rating model rating models, 1 field visit, 0 sensors and gauges, and 0 attachments.
...
09:57:26.845 INFO  - Dry run completed. 272 locations would have been deleted, including 220 time-series (11 derived, 209 basic), 1 rating model, 2 thresholds, 14 field visits and 43 attachments
```

## Operations supported on 3.X systems

Only location deletion is supported on 3.X systems, from 3.8 thru 3.10.

- Time-series deletion is not supported.
- Field visit deletion is not supported.
- Location recreation is not supported.

Any attempt to perform a non-supported operations on a 3.X system will stop with an error message.

```
10:39:46.093 ERROR - Field visit deletion is not supported for AQTS 3.10.905.0
```

## Run from within your favourite shell: CMD.EXE, Powershell, or bash

The `LocationDeleter.exe` tool is shell-agnostic, integrating well with CMD.EXE, Powershell, or bash.

Command line options can be set using a `/Option=Value` or `-option=value` syntax.

The `/Option=Value` syntax works best from `cmd.exe` shells, while the `-option=value` syntax works best from Powershell or Bash shells. Option keywords are always matched case-insensitively, to be as wrist-friendly as possible.

Some examples in this document will be run from CMD.EXE, and others will be run from bash, so you can get an idea of the different syntaxes.

## Use the `@filename.ext` syntax to store many options in a file, for easy re-use

Typing many options on a single command line can be tedious and unwieldy, so the tool also supports an `@optionsFile.ext` syntax, allowing you to save options in a simple text file. The filename that follows the `@` symbol can be any filename.

- One line per option.
- Blank lines and leading/trailing whitespace is ignored.
- Comment lines begin with a `#` or `//` marker

You can combine many `/option=value`, `-option=value`, and `@optionsFile` options on a single command line. This flexibility of specifying options can come in handy for repeating many complex tasks with minimal typing.

## Command line options

The full `/help` screen is shown below:

```
Deletes locations, time-series, and/or field visits from an AQTS server.

usage: LocationDeleter [-option=value] [@optionsFile] [location] [time-series] ...

Supported -option=value settings (/option=value works too):

  -Server               The AQTS app server.
  -Username             AQTS username. [default: admin]
  -Password             AQTS credentials. [default: admin]
  -SkipConfirmation     When true, skip the confirmation prompt. '/Y' is a shortcut for this option. [default: False]
  -DryRun               When true, don't make any changes. '/N' is a shortcut for this option. [default: False]
  -RecreateLocations    When true, recreate the location with the same properties. [default: False]
  -Location             Locations to delete.
  -TimeSeries           Time-series to delete.
  -VisitsBefore         Delete all visits in matching locations before and including this date.
  -VisitsAfter          Delete all visits in matching locations after and including this date.

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.

Location deletion:
=================
Locations can be specified by either location identifier or location unique ID.
Location identifiers are matched case-insensitively.
Publish API wildcard expansion of '*' is supported. '02*' will match locations beginning with '02'.

Time-series deletion:
=====================
Time-series can specified by identifier or by time-series unique ID.

Field-visit deletion:
=====================
When the /VisitsBefore= or /VisitsAfter= options are given, all the visits falling within the time-range will be deleted.
If no locations are specified when deleting field visits, visits from all locations will be considered.
```

# Confirming that stuff you are about to delete

The tool will tell you how many items owned by the location are going to be deleted if you proceed with deleting the location.

In order to proceed with the deletion, you will need to retype the location identifier in the console (case-insensitive, trimmed of leading/trailing whitespace).

Any other response, including an empty line, will skip deleting the location.

You can bypass confirmation with either the `/SkipConfirmation=true` or `/Y` shortcut.

When deleting a time-series, you must retype the time-series identifier to confirm the deletion.
When deleting field visits, you must retype the identifier of the location owning the field visits to be deleted.

In this run, typing "nope" to the prompt didn't match the "SchmidtKits" location identifier, so that location was not deleted.

```cmd
P:\doug.schmidt\LocationDeleter> LocationDeleter.exe -server=doug-vm2012r2 SchmidtKits

17:07:51.916 INFO  - Connecting LocationDeleter v1.0.0.0 to doug-vm2012r2 ...
17:07:53.084 INFO  - Connected to doug-vm2012r2 (2018.1.98.0)
17:07:53.504 WARN  - SchmidtKits - Schmidtsilano has 14 time-series, 1 threshold, 0 rating models, 9 field visits, 0 sensors, and 30 attachments.
17:07:53.504 WARN  - Are you sure you want to delete 'SchmidtKits'?
17:07:53.504 WARN  - Type the identifier of the location to confirm deletion.
nope
17:08:22.924 INFO  - Skipped deletion of location 'SchmidtKits'
17:08:23.181 INFO  - Deleted 0 of 1 location.
```

In this next run, typing "locationa" matched the "LocationA" identifier (case-insensitive) and so the location was deleted.

```cmd
P:\doug.schmidt\LocationDeleter> LocationDeleter.exe -server=doug-vm2012r2 -location=LocationA

17:09:16.968 INFO  - Connecting LocationDeleter v1.0.0.0 to doug-vm2012r2 ...
17:09:18.111 INFO  - Connected to doug-vm2012r2 (2018.1.98.0)
17:09:18.474 WARN  - LocationA - LocationA has 2 time-series, 0 thresholds, 0 rating models, 2 field visits, 0 sensors, and 0 attachments.
17:09:18.475 WARN  - Are you sure you want to delete 'LocationA'?
17:09:18.475 WARN  - Type the identifier of the location to confirm deletion.
locationa
17:09:26.215 INFO  - Deleting location 'LocationA' ...
17:09:27.932 INFO  - Deleted location 'LocationA' successfully.
17:09:28.164 INFO  - Deleted 1 of 1 location.
```

## Emptying out a location with `/RecreateLocations=true`

This option is useful on 201x app servers when repeatedly running some test scripts which require specifically configured locations to exist.

Use the `/RecreateLocations=true` option to immediately recreate any deleted locations. The same name, identifier, standard and extended attributes will be used to create the location. The recreated location will have a new unique ID.

```sh
$ LocationDeleter.exe -server=doug-vm2012r2 02kf015 -y -RecreateLocations=true
14:48:04.772 INFO  - Connected to doug-vm2012r2 (2017.4.79.0)
14:48:04.952 INFO  - Deleting location '02KF015' ...
14:48:05.426 INFO  - Deleted location '02KF015' successfully.
14:48:05.804 INFO  - Re-created '02KF015' (f5f5ff1f10874be5ba32843041dacfb3).
```

## Delete many items at once (what could go wrong?)

Location matching leverages the Publish API's partial pattern support, where '*' is a placeholder for "match anything".

- `*` all on its own will match all locations in the system (so yeah, that can be rather scary)
- `02*` will match all locations beginning with "02"
- `*i*` will match all locations containing the letter "I".

Locations to be deleted can be specified:
- Explicitly as a `/Location=identifier` option
- Implicitly without any leading `/Location=` or `-location=` prefix.
- Using either the identifier or uniqueID of the location.

It is often convenient to list each location as lines in an `@optionsFile`.

If you had a text file named `LocationsToDelete.txt` with these 4 lines:

```
# Delete these locations
Location1
0530824
TestLoc*
```

Then you could do a dry run to see the effect of deleting all the locations in that file with a command like this:

```sh
$ ./LocationDeleter.exe -server=myserver @LocationsToDelete.txt -N
```

You'll see how many locations matched that last line's `TestLoc*` partial pattern.

## Deleting time-series

The tool can also be used to delete individual time-series.

This is often useful on test systems because the Provisioning API allows you to create basic, reflected, or derived time-series, but only allows you to delete reflected time-series. If your calls to the Provisioning API configure hundreds of basic time-series incorrectly, you need some way of bulk deleting those mistakes and starting over.

Time-series to be deleted can be specified:
- Explicitly as a `/TimeSeries=identifier` or `/TimeSeries=uniqueId` option
- Implicitly as a `<Parameter>.Label@Location` identifier
- When deleting by time-series uniqueID, you must use the explicit `/TimeSeries=uniqueID` syntax, since a uniqueID on its own is assumed to be a location uniqueID.

You will need to retype the time-series identifier in order to confirm the deletion.

```cmd
$ ./LocationDeleter.exe -server=doug-vm2012r2 stage.fake@schmidtkits

15:45:55.736 INFO  - Connecting LocationDeleter v1.0.0.0 to doug-vm2012r2 ...
15:45:56.525 INFO  - Connected to doug-vm2012r2 (2018.1.98.0)
15:45:57.045 WARN  - Time-series 'Stage.Fake@SchmidtKits' (ProcessorBasic) has 16 points from 2/23/2018 9:13:48 AM -08:00 to 4/23/2018 12:43:09 PM -08:00, 1 grade, 1 approval
15:45:57.045 WARN  - Are you sure you want to delete Stage.Fake@SchmidtKits?
15:45:57.046 WARN  - Type the identifier of the time-series to confirm deletion.
 stage.fake@schmidtkits
15:46:21.298 INFO  - Deleted 1 of 1 time-series.
```

- If you have many time-series to delete, putting them in an `@optionsFile` is highly recommended.

## Deleting field-visits by time-range

The tool can also be used to delete field visits within a time-range, using either the `/VisitsBefore=date` or `/VisitsAfter=date` options.

Dates can be specified as ISO 8601 timestamps. `yyyy-mm-ddThh:mm:ss.fffffffzzz` with the following flexibility:

- The day, hour, minute, seconds, fractional seconds, and timezone are all optional.
- The day defaults to the first of the month if omitted.
- Other optional fields default to midnight when omitted.
- The `T` separating the date from time can also be space instead of a letter `T`
- If no time-zone is specified, your computer's local timezone is assumed. The timezone can be `Z` for UTC, or a `+hh:mm` or `-hh:mm`

If any locations are also specified, only visits within the matching locations will be considered for deletion. If no locations are specified, visits from all locations will be considered.

Here is a dry run asking to delete all the field visits from April 2017 onwards:

```sh
$ ./LocationDeleter.exe -Server=doug-vm2012r2 -VisitsAfter=2017-04 -n
10:40:26.701 INFO  - Connecting LocationDeleter v1.0.0.0 to doug-vm2012r2 ...
10:40:27.525 INFO  - Connected to doug-vm2012r2 (2018.1.98.0)
10:40:27.879 INFO  - Inspecting 136 locations for field visits after 2017-04-01T00:00:00.0000000-07:00 ...
10:40:29.027 INFO  - DryRun: 3 field visits at location 'SchmidtKits' from 7/1/2017 8:00:00 AM to 9/20/2017 6:00:00 PM
10:40:29.053 INFO  - No field visits to delete in location 'RiverSchmiver'.
10:40:29.074 INFO  - No field visits to delete in location '10KA009'.
...
10:40:32.133 INFO  - Dry run completed. 4 field visits would have been deleted from 136 locations.
```

Deleting visits and deleting locations are mutually exclusive operations. You can't do both in one run of the tool. When either `/VisitsBefore=` or `/VisitsAfter=` is specified, the tool switches into "only delete visits" mode.

## Delete requests which might be denied.

AQUARIUS Time-Series has many integrity checks, and will reject invalid delete requests, even when `/SkipConfirmation=true` is used. The tool will stop on the first such error encountered, since invalid requests usually mean that you are trying to delete something which can't be deleted without affecting other parts of your AQTS system. You are going to need to understand and fix that problem somehow before continuing with your delete request.

- A time-series that has derivation dependencies can't be deleted. You will first need to delete the derived time-series or rating models which depend on the time-series.

```
Code: DependentTimeSeriesException, Message: Time series is input to derivations, it cannot be deleted.
```

- A time-series with locked data can't be deleted. You will need to remove any locking approvals before deleting the time-series.

```
Code: DeleteLockedTimeSeriesException, Message: Cannot delete time series with locked data
```

- A locked field visit can't be deleted. You will need to remove any locking approvals before deleting the field visit.

```
Code: VisitLockedException, Message: Visit with Id 181453 is locked and cannot be edited.
```

### Requests to delete a location should always succeed

Unlike a time-series or field-visit delete request, which can be denied by an integrity check, asking to delete an entire location should always work.

Every object in the system owned by that location, including locked field visits, time-series with locked approvals, and all dependent derived time-series in that location will be deleted, along with the location itself.
