# Sonde File Importer

This Windows console app converts a sonde csv file to the Samples observations csv format and imports it to AQUARIUS Samples. 
The importer will also zip up the files in the Success folder if the total file number exceeds the threshold you specify.

### Configure the settings
   Configuration settings are in the Config.ini file. You must specify the Samples base URL, authentication token and the default UTC offset. 
   You may want to change the default `SondeFileFolder` or other settings in the config file.

### Run the importer
   Once you have configured the settings properly, drop your sonde files in the `SondeFileFolder` and double click `SondeFileImporter.exe` to launch the importer.
   You can also set up a Windows scheduled task to run it periodically. There are no additional arguments needed when you run `SondeFileImporter.exe`.

### Check the results
   If the import succeeds, the original sonde file and the converted observation csv file will be moved to Success folder. 
   If the import fails, you can find out the error messages in a csv file under the `Failed` folder.
   Note that currently the importer will not do a partial import. If there is even one invalid item in your csv data, the whole file will fail to be imported. You can fix the issues in your sonde file and import it again.
   You should also check the log file under the `Logs` folder for more information on each import run.

### Example data files
  You will find the following example files under `ExampleDataFiles` folder:
  1. "Sonde file example.csv"
  2. "Sonde file example.Converted.csv": This is the Samples observation csv file converted from the sonde csv file.
 