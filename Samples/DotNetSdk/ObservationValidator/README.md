# Samples Observation Validator

This Windows console app checks lab observations against a set of rules. A quality flag is set on observations that violate any applicable rules.

## Prerequisites
* Visual Studio 2017 to build the executable.
* .Net Framework 4.7.2 to run the app.
* An [AQUARIUS Samples](http://aquaticinformatics.com/products/aquarius-samples/) account.

## Configuration 
Open *ObservationValidator.exe.config* file with a text editor. Locate **appSettings** section and replace the **value** of the following keys:  
* authToken: 
Navigate to https://[your_account].aqsamples.com/api/ and follow the instruction to get the token.
* samplesApiBaseUrl: 
Replace *[your_account]* with your AQUARIUS Samples account name.
* qualityFlag: 
Specify the flag you want to set on observations that are determined to be invalid. The flag can be any text. If it's set to an empty string, the predefined flag "InvalidValue" will be used.

Save the config file.

## Rules
Open *ValidationRules.txt* with a text editor. Read the instructions in the file and add your rules.

## Run
### Understand the run modes
* Full: Examine all lab observations. 
The initial run will be a full run. After it finishes, a file called *LastRunStartTime.txt* is created. Any subsequent runs after that will be *Changes since* runs.
To enable a Full run again, delete or empty that file.
* Changes since: Examines lab observations modified since last run's start time.
The app will get the last run time from the file *LastRunStartTime.txt*. The run time is rounded to the minute level.

### Run the tool
Simply execute *"ObservationValidator.exe"*.
You can run it manually or set up a scheduled task.

## Check the Logs
Log files are located in the *Logs* sub-folder. Each run will generate 1 log file called *ObservationValidator.log*. Numbers of rules, specimens and observations examined are logged. 

IDs of the observations that have been tagged are also recorded. You can quickly locate the observation with the ID. 
For example: 
```
https://myAccount.aqsamples.com/labObservations/aa393556-5823-45f6-989a-4e6bf8f8df66
```
