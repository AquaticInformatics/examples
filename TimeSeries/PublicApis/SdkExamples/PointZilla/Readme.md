# PointZilla

`PointZilla` is a tool for quickly appending points to a time-series in an AQTS 201x system.
 
Points can be specified from:
- Command line parameters (useful for appending a single point)
- Function generators: linear, saw-tooth, or sine-wave signals. Useful for just getting *something* into a time-series
- CSV files (including CSV exports from AQTS Springboard)
- Points retrieved live from other AQTS systems.

## Rough notes

/Server=
/Username=(def. admin)
/Password=(def. admin)

/Wait=true|false (def. true)
/AppendTimeout=TimeSpan? (def. null)

/TimeRange=Interval? (for overwrite/reflected append. def. null = use start/end of generated points, "MinInstant" and "MaxInstant" are supported)

/NumberOfPoints=0 (0 means derive from periods)
/NumberOfPeriods=1
/StartTime=Instant? (def null = UtcNow)
/PointInterval=TimeSpan (def 1 minute)
/FunctionType=Linear|SawTooth|SineWave (def. Sine)
/FunctionOffset=0
/FunctionPhase=0
/FunctionScalar=1.0
/FunctionPeriod=1440

/Csv=file
/CsvTimeField=(def. 1) - Field index of 0 means "don't use". number:Format. Assume ISO8601 if format omitted.
/CsvTimeFormat=ISO8601
/CsvValueField=(def. 3)
/CsvGradeField=(def. 5)
/CsvQualifierField=(def. 6)
/CsvComment=#
/CsvSkipLines=int (def. 0)
/CsvIgnoreInvalidRows=true
/CsvRealign=false (true will adjust to /StartTime)

Multiple /CSV=file will parse multiple files (useful for combining points)
Skip any CSV row that doesn't parse (If a time doesn't parse, or a value doesn't parse, then skip it. This will skip the header by default, even when /CsvSkipLines is zero)
 
/CreateMode=None|Basic|Reflected (def. None)
/Command=Auto|Append|Overwrite|Reflected (def. Auto = Append or Reflected, depending on time-series type.)
/Grade=gradecode (apply to all points)
/Qualifier=list (apply to all points)
/TimeSeries=identifierOrGuid
/SourceTimeSeries=[server:[username:password:]]identifierOrGuid

PointZilla /options [command] [identifierOrGuid] [value] [csvFile]

- Use function generator if no CSV or explicit point values are defined
- Repeat CSV points by NumberOfPeriods
- 