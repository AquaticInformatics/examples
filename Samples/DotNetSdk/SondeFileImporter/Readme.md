# Sonde File Importer

This Windows console app converts a sonde csv file to the Samples observations csv format and imports to your Samples account. 
If the import succeeds, the original sonde file and the converted observation csv file will be moved to Success folder. 
If the import fails, you can find out the error messages in a csv file under the Failed folder.
The importer will also zip up the files in the Success folder if the total file number exceeds the threshold you specify.

## How to Use
   You can run this tool manually in a Windows command prompt or set up a Windows scheduled task to run it automatically.

## Configuration
   Configuration settings are in the Config.ini file. You must specify the Samples base URL, authentication token and the default UTC offset. 
   You may want to set up other optional settings in the config file.

## Check the Results
   You should check the Failed folder for files that failed to be importer. Open the error file and check for the error message.
   You can also open the log file under the Logs for more information on each import run.
