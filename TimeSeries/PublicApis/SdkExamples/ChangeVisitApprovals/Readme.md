# ChangeVisitApprovals

Download the [latest ChangeVisitApprovals.exe release here](../../../../../../releases/latest)

The `ChangeVisitApprovals` tool is a standalone .NET console utility for setting the approval level of existing field visits on an AQTS app server.

## Features

- Can set visit approvals by approval name (eg. "Working") or by numerical approval level (eg. 900)
- By default, all visits in all locations will be set to the specified approval level.
- Can set visit approvals in specific locations by identifier (case-insensitive) or by unique ID (for 201x app servers only). Publish API wildcard pattern matching is supported.
- Can limit the time-range of visits using `/VisitsBefore=` and/or `/VisitsAfter=` options.
- Everything gets logged to `ChangeVisitApprovals.log`
- The tools tells you how many visits will be changed if you proceed with the operation.
- The `/DryRun=true` or `/N` shortcut will not change anything, but will give you a summary of what would have been change.
- By default, you must confirm each operation at the console. Use `/y` or `/SkipConfirmation=true` to disable the prompt.

## Requirements

- The .NET 4.7 runtime is required, which is pre-installed on all Windows 10 and Windows Server 2016 systems, and on nearly all up-to-date Windows 7 and Windows Server 2008 systems.
- No installer is needed. It is just a single .EXE which can be run from any folder.

## Logged output

All activity is logged in `ChangeVisitApprovals.log` files, created in the same directory as the EXE.
Log output from previous runs is also retained, so this log should serve as a useful reference for what was changed.

## Use the `/DryRun=True` or `/N` option so see what would get changed

The `/DryRun=True` option is useful for safely confirming what would get changed if you said "yes" to all the confirmation prompts.

So `ChangeVisitApprovals * /N` will end up logging a summary of field visits per location, without changing any data.

```cmd
C:\> ChangeVisitApprovals /Server=doug-vm2012r2 * /N /ApprovalName=Approved

09:57:17.081 INFO  - Connecting ChangeVisitApprovals v1.0.0.0 to doug-vm2012r2 ...
09:57:20.373 INFO  - Connected to doug-vm2012r2 (2018.1.98.0)
09:57:20.995 INFO  - DryRun: 332 field visits at location 'TMFC1' from 1939-03-30 8:00:00 AM to 2018-04-02 8:00:00 AM
09:57:21.044 INFO  - DryRun: 260 field visits at location 'TRKC1' from 1930-03-30 8:00:00 AM to 2018-03-30 8:00:00 AM
...
09:57:26.845 INFO  - Dry run completed. 6421 field visits would have been changed from 64 locations.
```

## Run from within your favourite shell: CMD.EXE, Powershell, or bash

The `ChangeVisitApprovals.exe` tool is shell-agnostic, integrating well with CMD.EXE, Powershell, or bash.

Command line options can be set using a `/Option=Value` or `-option=value` syntax.

The `/Option=Value` syntax works best from `cmd.exe` shells, while the `-option=value` syntax works best from Powershell or Bash shells. Option keywords are always matched case-insensitively, to be as wrist-friendly as possible.

Some examples in this document will be run from CMD.EXE, and others will be run from bash, so you can get an idea of the different syntaxes.

## Use the `@filename.ext` syntax to store many options in a file, for easy re-use

Typing many options on a single command line can be tedious and unwieldy, so the tool also supports [the `@optionsFile.ext` syntax](https://github.com/AquaticInformatics/examples/wiki/Common-command-line-options), allowing you to save options in a simple text file. The filename that follows the `@` symbol can be any filename.

- One line per option.
- Blank lines and leading/trailing whitespace is ignored.
- Comment lines begin with a `#` or `//` marker

You can combine many `/option=value`, `-option=value`, and `@optionsFile` options on a single command line. This flexibility of specifying options can come in handy for repeating many complex tasks with minimal typing.

## Command line options

The full `/help` screen is shown below:

```
Changes field visit approval levels on an AQTS server.

usage: ChangeVisitApprovals [-option=value] [@optionsFile] [location] ...

Supported -option=value settings (/option=value works too):

  -Server               The AQTS app server.
  -Username             AQTS username. [default: admin]
  -Password             AQTS credentials. [default: admin]
  -ApprovalLevel        Sets the target approval level by numeric value.
  -ApprovalName         Sets the target approval level by name.
  -SkipConfirmation     When true, skip the confirmation prompt. '/Y' is a shortcut for this option. [default: False]
  -DryRun               When true, don't make any changes. '/N' is a shortcut for this option. [default: False]
  -Location             Locations to examine.
  -VisitsBefore         Change all visits in matching locations before and including this date.
  -VisitsAfter          Change all visits in matching locations after and including this date.

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.

Location filtering:
=================
Locations can be specified by either location identifier or location unique ID.
Location identifiers are matched case-insensitively.
Publish API wildcard expansion of '*' is supported. '02*' will match locations beginning with '02'.

Time-range filtering:
=====================
When the /VisitsBefore= or /VisitsAfter= options are given, only the visits falling within the time-range will be changed.
```

# Confirming that visits you are about to change

The tool will tell you how many visits owned by the location are going to be changed if you proceed with the operation.

In order to proceed with the change, you will need to retype the location identifier in the console (case-insensitive, trimmed of leading/trailing whitespace).

Any other response, including an empty line, will skip changes to the location.

You can bypass confirmation with either the `/SkipConfirmation=true` or `/Y` shortcut.

In this run, typing "nope" to the prompt didn't match the "APHC1" location identifier, so that location was not changed.

```cmd
P:\doug.schmidt\ChangeVisitApprovals> ChangeVisitApprovals.exe -server=doug-vm2012r2 -approvalLevel=900 APHC1

10:26:13.723 INFO  - Connecting ChangeVisitApprovals v1.0.0.0 to doug-vm2012r2 ...
10:26:14.506 INFO  - Connected to doug-vm2012r2 (v2019.1.110.0)
10:26:14.881 INFO  - Inspecting 1 location for field visits  ...
10:26:15.516 WARN  - 254 field visits at location 'APHC1' from 1965-04-14 8:00:00 AM to 2018-04-20 8:00:00 AM
10:26:15.516 WARN  - Are you sure you want to change 254 field visits at location 'APHC1'?
10:26:15.517 WARN  - Type the identifier of the location to confirm the operation.
nope
10:26:34.014 INFO  - Skipped change of 254 field visits at location 'APHC1'
10:26:34.015 INFO  - Changed 0 field visits from 1 location.
```

In this next run, typing "csnc1" matched the "CSNC1" identifier (case-insensitive) and so the location was changed.

```cmd
P:\doug.schmidt\ChangeVisitApprovals> ChangeVisitApprovals.exe -server=doug-vm2012r2 -approvalLevel=900 -location=CSNC1

10:30:21.084 INFO  - Connecting ChangeVisitApprovals v1.0.0.0 to doug-vm2012r2 ...
10:30:21.839 INFO  - Connected to doug-vm2012r2 (v2019.1.110.0)
10:30:22.173 INFO  - Inspecting 1 location for field visits  ...
10:30:22.845 WARN  - 397 field visits at location 'CSNC1' from 1930-01-03 8:00:00 AM to 2018-04-30 8:00:00 AM
10:30:22.846 WARN  - Are you sure you want to change 397 field visits at location 'CSNC1'?
10:30:22.847 WARN  - Type the identifier of the location to confirm the operation.
csnc1
10:31:01.130 INFO  - Changed 'CSNC1' visit Start=1930-01-03 8:00:00 AM End=1930-01-03 8:00:00 AM to approval level 900 (Working)
10:31:01.203 INFO  - Changed 'CSNC1' visit Start=1930-01-30 8:00:00 AM End=1930-01-30 8:00:00 AM to approval level 900 (Working)
...
10:31:24.853 INFO  - Changed 'CSNC1' visit Start=2018-04-30 8:00:00 AM End=2018-04-30 8:00:00 AM to approval level 900 (Working)
10:31:24.853 INFO  - Changed approval level on 397 field visits at location 'CSNC1' from 1930-01-03 8:00:00 AM to 2018-04-30 8:00:00 AM successfully.
10:31:24.854 INFO  - Changed 397 field visits from 1 location.
```

## Constraining the locations examined for field visits.

Location matching leverages the Publish API's partial pattern support, where '*' is a placeholder for "match anything".

- `*` all on its own will match all locations in the system (so yeah, that can be rather scary)
- `02*` will match all locations beginning with "02"
- `*i*` will match all locations containing the letter "I".

Locations to be examined can be specified:
- Explicitly as a `/Location=identifier` option
- Implicitly without any leading `/Location=` or `-location=` prefix.
- Using either the identifier or uniqueID of the location.

It is often convenient to list each location as lines in an `@optionsFile`.

If you had a text file named `LocationsToChange.txt` with these 4 lines:

```
# Change these locations
Location1
0530824
TestLoc*
```

Then you could do a dry run to see the effect of changing all the locations in that file with a command like this:

```sh
$ ./ChangeVisitApprovals.exe -server=myserver @LocationsToChange.txt -N -ApprovalLevel=Approved
```

You'll see how many locations matched that last line's `TestLoc*` partial pattern.

## Changing field-visits by time-range

The tool can also be used to change field visit approvalss within a time-range, using either the `/VisitsBefore=date` or `/VisitsAfter=date` options.

Dates can be specified as ISO 8601 timestamps. `yyyy-mm-ddThh:mm:ss.fffffffzzz` with the following flexibility:

- The day, hour, minute, seconds, fractional seconds, and timezone are all optional.
- The day defaults to the first of the month if omitted.
- Other optional fields default to midnight when omitted.
- The `T` separating the date from time can also be space instead of a letter `T`
- If no time-zone is specified, your computer's local timezone is assumed. The timezone can be `Z` for UTC, or a `+hh:mm` or `-hh:mm`

If any locations are also specified, only visits within the matching locations will be considered for changes. If no locations are specified, visits from all locations will be considered.

Here is a dry run asking to change all the field visits before April 2017 onwards:

```sh
$ ./ChangeVisitApprovals.exe -Server=doug-vm2012r2 -VisitsAfter=2017-04 -n -ApprovalLevel=Approved
10:36:45.351 INFO  - Connecting ChangeVisitApprovals v1.0.0.0 to doug-vm2012r2 ...
10:36:46.130 INFO  - Connected to doug-vm2012r2 (v2019.1.110.0)
10:36:46.490 INFO  - Inspecting 64 locations for field visits after 2017-04-01T00:00:00.0000000-07:00 ...
10:36:46.647 INFO  - No field visits to change in location 'A1'.
10:36:46.745 INFO  - No field visits to change in location 'ALPH'.
10:36:46.837 INFO  - No field visits to change in location 'CFAX'.
10:36:46.987 INFO  - DryRun: 6 field visits at location 'APHC1' from 2017-04-26 8:00:00 AM to 2018-04-20 8:00:00 AM
...
10:36:53.117 INFO  - DryRun: 6 field visits at location 'WRGC1' from 2017-04-25 8:00:00 AM to 2018-04-30 8:00:00 AM
10:36:53.200 INFO  - Dry run completed. 120 field visits would have been changed from 64 locations.
```
