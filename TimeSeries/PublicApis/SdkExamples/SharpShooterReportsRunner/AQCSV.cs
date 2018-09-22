using System;
using System.Collections.Generic;
using System.IO;
using CommunicationShared.Util;

namespace SharpShooterReportsRunner
{

    delegate AQCSV AQCSV_Load(string csv);
    delegate AQCSVTable AQCSVTable_Load(string csv, string name);

    /// <summary>
    /// AQUARIUS CSV DTO definition
    /// One CSV can have several tables.
    /// </summary>
    [Serializable]
    public class AQCSV
    {
        public AQCSVMetaData metaData;
        public AQCSVDataSet[] dataSet;

        /// <summary>
        /// Load csv.
        /// e.g. Load(csv, true, new string[]{"ExpandedPoints", "OriginalPoints"});
        /// </summary>
        /// <param name="csv">csv string used to load into AQCSV instance.</param>
        /// <param name="withMetaHader">If csv with metadata, this parameter should be true, otherwise set to false.</param>
        /// <param name="tablesRecordCount">string array, items used to show the number of records for each table.</param>
        /// <returns></returns>
        public static AQCSV Load(string csv, bool withMetaHader, string[] tablesRecordCount)
        {
            AQCSV ret = new AQCSV();
            if (string.IsNullOrEmpty(csv))
            {
                return ret;
            }
            if (tablesRecordCount == null || tablesRecordCount.Length <1)
            {
                return ret;
            }
            ret.metaData = new AQCSVMetaData();
            ret.dataSet = new AQCSVDataSet[tablesRecordCount.Length];
            int[] tablePoints = new int[tablesRecordCount.Length];
            int[] tablePointsRead = new int[tablesRecordCount.Length];
            for (int i = 0; i < tablesRecordCount.Length; i++)
            {
                ret.dataSet[i] = new AQCSVDataSet();
                tablePoints[i] = tablePointsRead[i] = 0;
            }
            using (StringReader reader = new StringReader(csv))
            {
                string line = null;
                int state = 0; //0, meta header, 1, meta data, 2 data1 header, 3, data 1, 4 data2 header, 5, data 2 ....
                int dataSection = 0;
                List<string[]> dateItems = new List<string[]>();
                if (!withMetaHader) state = 2;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length > 0)
                    {
                        if (state == 0)//meta header
                        {
                            ret.metaData.headers = CSVUtil.splitCCVLine(line);
                            ret.metaData.columns = ret.metaData.headers.Length;
                            state = 1;
                        }
                        else if (state == 1)//meta data
                        {
                            ret.metaData.datas = CSVUtil.splitCCVLine(line);
                            state = 2;
                            for (int i = 0; i < tablesRecordCount.Length; i++)
                            {
                                tablePoints[i] = int.Parse(ret.metaData[tablesRecordCount[i]]);
                            }
                        }
                        else
                        {
                            if (state % 2 == 0)//data header, oven state
                            {
                                ret.dataSet[dataSection].headers = CSVUtil.splitCCVLine(line);
                                ret.dataSet[dataSection].columns = ret.dataSet[0].headers.Length;
                                state++;
                            }
                            else//data, odd data
                            {
                                string[] dataItem = CSVUtil.splitCCVLine(line);
                                dateItems.Add(dataItem);
                                tablePointsRead[dataSection]++;
                                if (tablePointsRead[dataSection] == tablePoints[dataSection])
                                {
                                    ret.dataSet[dataSection].datas = dateItems.ToArray();
                                    dateItems.Clear();
                                    state++;
                                    dataSection++;
                                }
                            }
                        }
                    }
                }
                reader.Close();
            }
            return ret;
        }
    }

    /// <summary>
    /// AQUARIUS CSV Table DTO definition
    /// E.g.
    /// StartTime,EndTime,Parameter,Identifier,Label,ViewCode,lastPollTime
    /// 2010-06-01 00:00:01.000,2010-06-06 23:59:59.000,HG, 02GA010,Telemetry,Public, 2010-06-06 23:59:59.000
    /// Timestamps,Value
    /// 2010-06-01 00:00:01.000,10.1
    /// 2010-06-01 00:15:01.000,10.2
    /// 2010-06-01 00:30:01.000,10.3
    /// </summary>
    [Serializable]
    public class AQCSVTable
    {
        public string tblName;
        public AQCSVMetaData metaData;
        public AQCSVDataSet dataSet;

        public static AQCSVTable Load(string csv)
        {
            int lineCount = 0;
            return Load(csv, null, null, ref lineCount);
        }
        public static AQCSVTable Load(string csv, string tblName)
        {
            int lineCount = 0;
            return Load(csv, tblName, null, ref lineCount);
        }
        public static AQCSVTable Load(string csv, string tblName, string countColumnName, ref int lineCount)
        {
            AQCSVTable ret = new AQCSVTable();
            ret.tblName = tblName;
            if (string.IsNullOrEmpty(csv))
            {
                return ret;
            }
            ret.metaData = new AQCSVMetaData();
            ret.dataSet = new AQCSVDataSet();
            using (StringReader reader = new StringReader(csv))
            {
                string line = null;
                int curLineCnt = 0;
                int dataNumber = 0;
                int dataCount = 0;
                int state = 0; //0, meta header, 1, meta data, 2 data header, 3, data 
                List<string[]> dateItems = new List<string[]>();
                while ((line = reader.ReadLine())!=null)
                {
                    curLineCnt++;
                    if (curLineCnt <= lineCount) continue;
                    lineCount = curLineCnt;
                    if (line.Length > 0)
                    {
                        switch (state)
                        {
                            case 0://meta header
                                {
                                    ret.metaData.headers = CSVUtil.splitCCVLine(line);
                                    ret.metaData.columns = ret.metaData.headers.Length;
                                    state = 1;
                                    break;
                                }
                            case 1://meta data
                                {
                                    ret.metaData.datas = CSVUtil.splitCCVLine(line);
                                    if (!string.IsNullOrEmpty(countColumnName))
                                    {
                                        dataNumber = int.Parse(ret.metaData[countColumnName]);
                                    }
                                    state = 2;
                                    break;
                                }
                            case 2://data header
                                {
                                    ret.dataSet.headers = CSVUtil.splitCCVLine(line);
                                    ret.dataSet.columns = ret.dataSet.headers.Length;
                                    state = 3;
                                    break;
                                }
                            case 3://data
                                {
                                    string[] dataItem = CSVUtil.splitCCVLine(line);
                                    dateItems.Add(dataItem);
                                    dataCount++;                                   
                                    break;
                                }
                            default:
                                break;
                        }
                        if (dataNumber > 0 && dataCount == dataNumber) //move to next
                        {
                            lineCount++;
                            break;
                        }
                    }
                }
                reader.Close();
                ret.dataSet.datas = dateItems.ToArray();
                ret.dataSet.length = ret.dataSet.datas.Length;
            }
            
            return ret;
        }

    }

    /// <summary>
    /// AQUARIUS CSV Table DTO definition
    /// E.g.
    /// StartTime,EndTime,Parameter,Identifier,Label,ViewCode,lastPollTime
    /// 2010-06-01 00:00:01.000,2010-06-06 23:59:59.000,HG, 02GA010,Telemetry,Public, 2010-06-06 23:59:59.000
    /// Timestamps,Value
    /// 2010-06-01 00:00:01.000,10.1
    /// 2010-06-01 00:15:01.000,10.2
    /// 2010-06-01 00:30:01.000,10.3
    /// </summary>
    [Serializable]
    public class AQCSVTimeSeries
    {
        public AQCSVMetaData metaData;
        public AQCSVTable[] ranges = null;

        public static AQCSVTimeSeries Load(string csv)
        {
            AQCSVTimeSeries ret = new AQCSVTimeSeries();
            if (string.IsNullOrEmpty(csv))
            {
                return ret;
            }
            ret.metaData = new AQCSVMetaData();
            using (StringReader reader = new StringReader(csv))
            {
                string line = null;
                int lineCount = 0;
                //meta header
                line = reader.ReadLine();
                ret.metaData.headers = CSVUtil.splitCCVLine(line);
                ret.metaData.columns = ret.metaData.headers.Length;
                lineCount++;

                //meta data
                line = reader.ReadLine();
                ret.metaData.datas = CSVUtil.splitCCVLine(line);
                int rangeCount = int.Parse(ret.metaData["NumRanges"]);
                ret.ranges = new AQCSVTable[rangeCount];
                lineCount++;

                for (int i = 0; i < rangeCount; i++)
                {
                    ret.ranges[i] = AQCSVTable.Load(csv, null, "NumPoints", ref lineCount);
                }
                reader.Close();
            }
            return ret;
        }

        public DateTime[] GetDateTimeVec(int column)
        {
            List<DateTime> ret = new List<DateTime>();
            foreach (AQCSVTable range in ranges)
            {
                if(range.dataSet!=null&&range.dataSet.datas!=null && range.dataSet.datas.Length>0)
                {
                    ret.AddRange(range.dataSet.GetDateTimeVec(column));
                }
            }
            return ret.ToArray();
        }
        public double[] GetOADateTimeVec(int column)
        {
            List<double> ret = new List<double>();
            foreach (AQCSVTable range in ranges)
            {
                if(range.dataSet!=null&&range.dataSet.datas!=null && range.dataSet.datas.Length>0)
                {
                    ret.AddRange(range.dataSet.GetOADateTimeVec(column));
                }
            }
            return ret.ToArray();
        }
        public double[] GetDoubleVec(int column, double nullValue)
        {
            List<double> ret = new List<double>();
            foreach (AQCSVTable range in ranges)
            {
                if(range.dataSet!=null&&range.dataSet.datas!=null && range.dataSet.datas.Length>0)
                {
                    ret.AddRange(range.dataSet.GetDoubleVec(column, nullValue));
                }
            }
            return ret.ToArray();
        }
        
    }

    /// <summary>
    /// E.g.
    /// StartTime,EndTime,Parameter,Identifier,Label,ViewCode,lastPollTime,Records
    /// 2010-06-01 00:00:01.000,2010-06-06 23:59:59.000,HG, 02GA010,Telemetry,Public, 2010-06-06 23:59:59.000,3
    /// 
    /// columns = 7;
    /// headers = new string[] { "StartTime", "EndTime", "Parameter", "Identifier", 
    ///                          "Label", "ViewCode", "lastPollTime", "Records" };
    /// datas = new string[] { "2010-06-01 00:00:01.000", "2010-06-06 23:59:59.000", "HG", 
    ///                        "02GA010", "Telemetry", "Public", "2010-06-06 23:59:59.000", "3" };
    /// </summary>
    [Serializable]
    public class AQCSVMetaData
    {
        public int columns;
        public string[] headers;
        public string[] datas;

        public bool Contains(params string[] names)
        {
            var nameList = new List<string>();
            nameList.AddRange(names);
            for (int i = 0; i < headers.Length; i++)
            {
                if (nameList.Contains(headers[i]))
                {
                    nameList.Remove(headers[i]);
                    if (nameList.Count == 0)
                        return true;
                }
            }
            return false;
        }


        public string this[int pos]
        {
            get
            {
                if(pos<0 || pos>=headers.Length)
                {
                    throw new IndexOutOfRangeException(string.Format("Index {0} is out of AQCSVMetaData.headers range.", pos));
                }
                return datas[pos];
            }
            set
            {
                if (pos < 0 || pos >= headers.Length)
                {
                    throw new IndexOutOfRangeException(string.Format("Index {0} is out of AQCSVMetaData.headers range.", pos));
                }
                datas[pos] = value;
            }
        }

        public string this[string name]
        {
            get 
            {
                for(int i=0;i<headers.Length;i++)
                {
                    if(headers[i]==name)
                    {
                        return datas[i];
                    }
                }
                throw new Exception(string.Format("{0} is not in the AQCSVMetaData.headers.", name));
            }
            set
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    if (headers[i] == name)
                    {
                        datas[i] = value;
                        return;
                    }
                }
                throw new Exception(string.Format("{0} is not in the AQCSVMetaData.headers.", name));
            }
        }
    }

    /// <summary>
    /// AQCSV data set dto.
    /// E.g. 
    /// Timestamps,Value
    /// 2010-06-01 00:00:01.000,10.1
    /// 2010-06-01 00:15:01.000,10.2
    /// 2010-06-01 00:30:01.000,10.3
    /// 
    /// columns = 2;
    /// length = 3;
    /// headers = string[] { "Timestamps", "Value" };
    /// datas = string[][] {
    ///     new string[] { "2010-06-01 00:00:01.000", "10.1" },
    ///     new string[] { "2010-06-01 00:15:01.000", "10.2" },
    ///     new string[] { "2010-06-01 00:30:01.000", "10.3" }
    ///     };
    ///     
    /// </summary>
    [Serializable]
    public class AQCSVDataSet
    {
        public int columns;
        public int length;
        public string[] headers;
        public string[][] datas;

        public DateTime[] GetDateTimeVec(int column)
        {
            List<DateTime> ret = new List<DateTime>();
            foreach(string[] row in datas)
            {
                if (string.IsNullOrEmpty(row[column]))
                {
                    ret.Add(DateTime.MinValue);
                }
                else
                {
                    DateTime dt = Convert.ToDateTime(row[column]);
                    ret.Add(dt);
                }
            }
            return ret.ToArray();
        }
        public double[] GetOADateTimeVec(int column)
        {
            List<double> ret = new List<double>();
            foreach (string[] row in datas)
            {
                if (string.IsNullOrEmpty(row[column]))
                {
                    ret.Add(DateTime.MinValue.ToOADate());
                }
                else
                {
                    DateTime dt = Convert.ToDateTime(row[column]);
                    ret.Add(dt.ToOADate());
                }
            }
            return ret.ToArray();
        }
        public double[] GetDoubleVec(int column, double nullValue)
        {
            List<double> ret = new List<double>();
            foreach (string[] row in datas)
            {
                if(string.IsNullOrEmpty(row[column]))
                {
                    ret.Add(nullValue);
                }
                else
                {
                    double dt = Convert.ToDouble(row[column]);
                    ret.Add(dt);
                }
            }
            return ret.ToArray();
        }
    }

    #region CSV utility
    /// <summary>
    /// CSV Utility
    /// </summary>
    public static class CSVUtil
    {
        private readonly static log4net.ILog log = log4net.LogManager.GetLogger(
            typeof(CSVUtil).Name);

        public static double ToDouble(string data)
        {
            double aqNan = AopSpecialValues.AqNan;
            if(string.IsNullOrEmpty(data))
            {
                return aqNan;
            }
            else 
            {
                try
                {
                    return double.Parse(data);
                }
                catch
                {
                    return aqNan;
                }                
            }
        }

        public static double[] ToDouble(string[] data)
        {
            return Array.ConvertAll<string, double>(data, ToDouble);
        }

        public static string[] ToString(string[][] data, int column)
        {
            string[] ret = Array.ConvertAll<string[], string>(data,
                delegate(string[] metaData)
                {
                    return metaData[column];
                });
            return ret;
        }
        public static double[] ToDouble(string[][] data, int column)
        {
            double[] ret = Array.ConvertAll<string[], double>(data, 
                delegate(string[] metaData)
                {
                    string dataCol = metaData[column];
                    double aqNan = AopSpecialValues.AqNan;
                    if (string.IsNullOrEmpty(dataCol))
                    {
                        return aqNan;
                    }
                    else
                    {
                        try
                        {
                            return double.Parse(dataCol);
                        }
                        catch
                        {
                            return aqNan;
                        }
                    }
                });
            return ret;
        }

        public static DateTime ToDateTime(string data)
        {
            return ToDateTime(data, DateTime.MinValue);
        }

        public static DateTime ToDateTime(string data, DateTime defaultTime)
        {
            if (string.IsNullOrEmpty(data))
            {
                return defaultTime;
            }
            else
            {
                DateTime ret;
                if (DateTime.TryParseExact(data, "yyyy-MM-ddTHH:mm:ss.fffzzz", null, System.Globalization.DateTimeStyles.AdjustToUniversal, out ret))
                {
                    //if DateTime contains zzz, it is a time with timezone, here we use AdjustToUniversal to keep timezone information, and we do not need to convert to localTime
                    return ret;
                }
                else if (DateTime.TryParse(data, out ret))
                {
                    if(ret.Kind == DateTimeKind.Unspecified)
                    {
                        log.WarnFormat("DateTime string '{0}' does not exactly in format 'yyyy-MM-ddTHH:mm:ss.fffzzz', we may meet some timezone issues on responsed data.", data);    
                    }
                    return ret;
                }
                else
                {
                    log.WarnFormat("String '{0}' can not covert to DateTime, return DateTime.MinValue.", data);
                    return defaultTime;
                }
            }
        }

        public static DateTime[] ToDateTime(string[] data)
        {
            return Array.ConvertAll<string, DateTime>(data, ToDateTime);
        }

        public static string[] splitCCVLine(string line)
        {
            //*// Using string.split
            return line.Split(',');
            /*///
            List<string> list = new List<string>();
            int pos0 = -1;
            int pos1 = -1;
            while(true)
            {
                pos1 = line.IndexOf(',', pos0 + 1);
                if(pos1<=pos0)
                {
                    break;
                }
                string piece = "";
                if(pos1>(pos0+1))
                {
                    piece = line.Substring(pos0 + 1, pos1 - pos0 - 1);
                    if(piece[0]=='"')//need ignore comma in the end of double quote;
                    {
                        int pos2 = line.IndexOf(@""",", pos1);
                        piece = line.Substring(pos0 + 1, pos2 - pos0 - 1);
                        pos0 = pos2;
                    }
                    else
                        pos0 = pos1;
                }
                list.Add(piece);
            }
            return list.ToArray();
            //*/
        }

        public static string ToColumn(string str) //Used by ToColumn(object)
        {
            if (string.IsNullOrEmpty(str)) return "";

            var val = str;
            if (val.IndexOfAny(new[] { ',', '\n', '\r', '\"' }) > -1)
            {
                if (val.IndexOf('\"') > -1) val = val.Replace("\"", "\"\"");
                val = "\"" + val + "\"";
            }
            return val;
        }
        public static string ToColumn(DateTime dt)
        {
            string ret;
            switch (dt.Kind)
            {
                case DateTimeKind.Utc:
                    ret = dt.ToString("yyyy-MM-ddTHH:mm:ss.fff" + "-00:00");
                    break;
                case DateTimeKind.Unspecified:
                    ret = dt.ToString("yyyy-MM-ddTHH:mm:ss.fff");
                    break;
                case DateTimeKind.Local:
                default:
                    ret = dt.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                    break;
            }
            return ret;
        }
        public static string ToColumnFromUtcOADate(double dt)
        {
            return DateTime.FromOADate(dt).ToString("yyyy-MM-ddTHH:mm:ss.fff");
        }
        public static string ToColumnFromOADate(double dt)
        {
            return DateTime.FromOADate(dt).ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
        }

        public static string ToColumnFromOADate(double dt, string utcoffset)
        {
            return DateTime.FromOADate(dt).ToString("yyyy-MM-ddTHH:mm:ss.fff") + utcoffset;
        }
        public static string ToColumnFromOADate(double dt, double utcoffset)
        {
            string utcOffsetStr = ToTimezoneString(utcoffset);
            return ToColumnFromOADate(dt, utcOffsetStr);
        }
        public static string ToColumnFromOADate(double dt, int timezoenBias)
        {
            string utcOffsetStr = ToTimezoneString(timezoenBias);
            return ToColumnFromOADate(dt, utcOffsetStr);
        }
        public static string ToColumn(double dt)
        {
            return dt.ToString("#0.000##############################", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ToColumn(object obj)
        {
            if (obj == null)
            {
                return "";
            }
            TypeCode typeCode = Type.GetTypeCode(obj.GetType());
            switch (typeCode)
            {
                case TypeCode.String: //A sealed class type representing Unicode character strings. 
                    {
                        var str = (string) obj;
                        return ToColumn(str);
                    }
                case TypeCode.Boolean: //A simple type representing Boolean values of true or false. 
                    {
                        bool val = (bool) obj;
                        return val ? "true" : "false";
                    }
                case TypeCode.Char:
                    //An integral type representing unsigned 16-bit integers with values between 0 and 65535
                case TypeCode.SByte:
                    //An integral type representing signed 8-bit integers with values between -128 and 127. 
                case TypeCode.Byte:
                    //An integral type representing unsigned 8-bit integers with values between 0 and 255. 
                case TypeCode.Int16:
                    //An integral type representing signed 16-bit integers with values between -32768 and 32767. 
                case TypeCode.UInt16:
                    //An integral type representing unsigned 16-bit integers with values between 0 and 65535. 
                case TypeCode.Int32:
                    //An integral type representing signed 32-bit integers with values between -2147483648 and 2147483647. 
                case TypeCode.UInt32:
                    //An integral type representing unsigned 32-bit integers with values between 0 and 4294967295. 
                case TypeCode.Int64:
                    //An integral type representing signed 64-bit integers with values between -9223372036854775808 and 9223372036854775807.
                case TypeCode.UInt64:
                    //An integral type representing unsigned 64-bit integers with values between 0 and 18446744073709551615. 
                    return obj.ToString();
                case TypeCode.Single:
                    //A floating point type representing values ranging from approximately 1.5 x 10 -45 to 3.4 x 10 38 with a precision of 7 digits. 
                case TypeCode.Double:
                    //A floating point type representing values ranging from approximately 5.0 x 10 -324 to 1.7 x 10 308 with a precision of 15-16 digits.
                    {
                        double val = (double) obj;
                        return ToColumn(val);
                    }
                case TypeCode.Decimal:
                    //A simple type representing values ranging from 1.0 x 10 -28 to approximately 7.9 x 10 28 with 28-29 significant digits. 
                    return obj.ToString();
                case TypeCode.DateTime: //A type representing a date and time value. 
                    {
                        DateTime val = (DateTime)obj;
                        return ToColumn(val);
                    }
                case TypeCode.Empty: //A null reference. 
                case TypeCode.DBNull: //A database null (column) value. 
                    return "";
                default:
                    return obj.ToString();
            }
        }

        //Used for publish API GetDataSetsList which for AQAtomTimeSeries startTime_, endTime_ column
        public static string ToColumnFromDbTime(DateTime dbTime, double actualUtcoffset)
        {
            int timezoneBias = (int)(-60.0 * actualUtcoffset);
            return ToColumnFromDbTime(dbTime, 60);
        }

        public static string ToColumnFromDbTime(DateTime dbTime, int timezoneBias)
        {
            if (dbTime == DateTime.MinValue || dbTime == DateTime.MaxValue) return "";
            if(dbTime.Kind == DateTimeKind.Utc)
            {
                dbTime = dbTime - TimeSpan.FromMinutes(timezoneBias);
            }
            else
            {
                if(dbTime.Kind == DateTimeKind.Unspecified)log.Warn("Database return DateTime timezone is unspecified.");
                dbTime = dbTime.ToUniversalTime() - TimeSpan.FromMinutes(timezoneBias);
            }
            return dbTime.ToString("yyyy-MM-ddTHH:mm:ss.fff") + ToTimezoneString(timezoneBias);
        }

        //Used for publish API GetTimeSeriesData startTime, endTime matching
        public static double ToTsDblTime(DateTime time, double actualUtcoffset)
        {
            int timezoneBias = (int)(actualUtcoffset*60*-1);
            return ToTsDblTime(time, timezoneBias);
        }

        //Used for publish API GetTimeSeriesData startTime, endTime matching
        public static double ToTsDblTime(DateTime time, int timezoneBias)
        {
            if (time == DateTime.MinValue || time == DateTime.MaxValue) return time.ToOADate();
            switch (time.Kind)
            {
                case DateTimeKind.Utc:
                    return (time - TimeSpan.FromMinutes(timezoneBias)).ToOADate();
                case DateTimeKind.Local:
                    //First convert to universal, then apply specific timezoneBias, and finally case to double OADate.
                    var convertTime = time.ToUniversalTime() - TimeSpan.FromMinutes(timezoneBias);
                    return convertTime.ToOADate();
                //case DateTimeKind.Unspecified:
                default:
                    log.Warn("Timezone is unspecified, treat as timeseries timezone.");
                    return time.ToOADate();
            }
        }

        public static double ToTsDblTime(string timeString, int timezoneBias)
        {
            var time = ToDateTime(timeString, DateTime.MinValue);
            return ToTsDblTime(time, timezoneBias);
        }

        public static string ToTimezoneString(double utcoffset)
        {
            TimeSpan span = TimeSpan.FromHours(utcoffset);
            return ToTimezoneString(span);
        }
        public static string ToTimezoneString(int timezoneBias)
        {
            TimeSpan span = TimeSpan.FromMinutes(-1*timezoneBias);
            return ToTimezoneString(span);
        }

        static string ToTimezoneString(TimeSpan span)
        {
            var sign = "";
            if (span > TimeSpan.Zero)
                sign = "+";
            else if (span == TimeSpan.Zero)
                sign = "-";
            /*string utcstr = sign + span.Hours.ToString("00") + ":" + span.Minutes.ToString("00");*/
            string utcstr = sign + span.ToString();
            utcstr = utcstr.Substring(0, utcstr.Length - 3);
            if (utcstr.Length != 6)
            {
                utcstr = sign + span.Hours.ToString("00") + ":"+ span.Minutes.ToString("00");
            }
            return utcstr;
        }

    }
    #endregion
}
