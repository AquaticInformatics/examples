using System;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Drawing.Design;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Globalization;
using PerpetuumSoft.Framework.Drawing;
using PerpetuumSoft.Framework.Serialization;
using PerpetuumSoft.Reporting.DOM;
using PerpetuumSoft.Reporting.Components;
using Dundas.Charting.WinControl;

// Keep the ReportApp namespace to retain compatibility

namespace ReportApp
{
    // Lifted entire class from DateTimeFormat.cs
    public static class DateTimeFormat
    {
        public const string DateTime = "yyyy-MM-dd HH:mm:ss";
        public const string DateTimeNoSeconds = "yyyy-MM-dd HH:mm";
        public const string DayIndexHoursMinutes = "d|HH:mm";
        public const string Date = "yyyy-MM-dd";
        public const string Year = "yyyy";
        public const string YearMonth = "yyyy-MM";
        public const string MonthName = "MMMM";
        public const string MonthDay = "MM-dd";
        public const string Time = "HH:mm:ss";
        public const string HoursMinutes = "HH:mm";
    }

    // Lifted one static method from ReportPlugin.cs
    public static class ReportPlugIn
    {
        public static System.Drawing.Color HexStringToDrawingColor(string hexColor)
        {
            int r1 = int.Parse(hexColor.Substring(1, 2), NumberStyles.HexNumber);
            int g1 = int.Parse(hexColor.Substring(3, 2), NumberStyles.HexNumber);
            int b1 = int.Parse(hexColor.Substring(5, 2), NumberStyles.HexNumber);

            System.Drawing.Color color = System.Drawing.Color.FromArgb(r1, g1, b1);

            return color;
        }
    }
}

namespace ReportApp
{

    public enum SeriesLineType
    {
        Point = 0,
        Line = 3,
        Column = 10,
    }

    public enum XAxisDateFormat
    {
        ShortDate = 0,
        LongDate = 1,
        ShortTime = 2,
        LongTime = 3,
        FullDateShortTime = 4,
        FullDateLongTime = 5,
        GeneralDateShortTime = 6,
        GeneralDateLongTime = 7,
        MonthYear = 8,
        DayMonth = 9,
        Month = 10,
        Year = 11,
        GeneralNumber = 12,
        NotSet = 13,
    }

    public class SeriesDescription
    {
        private Color _LineColor = Color.Blue;
        private Color _MarkerColor = Color.Blue;
        private Color _MarkerBorderColor = Color.Transparent;
        private int _MarkerSize = 20;
        private int _LineWidth = 1;
        private AxisType _YAxisType = AxisType.Primary;
        private bool _ShowInLegend = true;
        private MarkerStyle _MarkerStyle = MarkerStyle.Circle;
        private SeriesLineType _LineType = SeriesLineType.Line;
        private string _ValuesTable = null;
        private string _XValuesColumn = null;
        private string _YValuesColumn = null;
        private string _LabelColumn = null;
        private string _LabelColumn2 = null;
        private string _ColorColumn = null;
        private string _SeriesNamesTable = null;
        private string _SeriesNameColumn = null;
        private string _SeriesName = null;

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(_ValuesTable) && !string.IsNullOrEmpty(_YValuesColumn))
                return _ValuesTable + "." + _YValuesColumn;

            return "SeriesDescription";
        }

        [XSerializable]
        [ReportBindable]
        [DefaultValue(typeof(SeriesLineType), "Line")]
        [DisplayName("Series Style")]
        [Category("Appearance")]
        public SeriesLineType LineType
        {
            get
            {
                return _LineType;
            }
            set
            {
                _LineType = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [DefaultValue(typeof(AxisType), "Primary")]
        [DisplayName("Attach to Axis")]
        [Category("Misc")]
        public AxisType YAxisType
        {
            get
            {
                return _YAxisType;
            }
            set
            {
                _YAxisType = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [DisplayName("Show in Legend")]
        [DefaultValue(typeof(bool), "True")]
        [Category("Misc")]
        public bool ShowInLegend
        {
            get { return _ShowInLegend; }
            set { _ShowInLegend = value; }
        }

        [XSerializable]
        [ReportBindable]
        [DefaultValue(typeof(Color), "Blue")]
        [Category("Appearance")]
        public Color Color
        {
            get
            {
                return _LineColor;
            }
            set
            {
                _LineColor = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [DefaultValue(typeof(int), "1")]
        [DisplayName("Line Thickness")]
        [Category("Appearance")]
        public int LineWidth
        {
            get
            {
                return _LineWidth;
            }
            set
            {
                _LineWidth = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [DefaultValue(typeof(Color), "Blue")]
        [DisplayName("Marker Color")]
        [Category("Appearance")]
        public Color MarkerColor
        {
            get
            {
                return _MarkerColor;
            }
            set
            {
                _MarkerColor = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [DefaultValue(typeof(Color), "Transparent")]
        [DisplayName("Marker Border Color")]
        [Category("Appearance")]
        public Color MarkerBorderColor
        {
            get
            {
                return _MarkerBorderColor;
            }
            set
            {
                _MarkerBorderColor = value;
            }
        }
        
        [XSerializable]
        [ReportBindable]
        [DefaultValue(typeof(int), "20")]
        [DisplayName("Marker Size")]
        [Category("Appearance")]
        public int MarkerSize
        {
            get
            {
                return _MarkerSize;
            }
            set
            {
                _MarkerSize = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [DefaultValue(typeof(MarkerStyle), "Circle")]
        [DisplayName("Marker Shape")]
        [Category("Appearance")]
        public MarkerStyle MarkerStyle
        {
            get
            {
                return _MarkerStyle;
            }
            set
            {
                _MarkerStyle = value;
            }
        }
        
        [XSerializable]
        [ReportBindable]
        [DisplayName("Table for Values")]
        [Category("Data")]
        public string ValuesTable
        {
            get
            {
                return _ValuesTable;
            }
            set
            {
                _ValuesTable = value;
            }
        }
        
        [XSerializable]
        [ReportBindable]
        [Category("Data")]
        [DisplayName("Column for Y Values")]
        public string YValuesColumn
        {
            get
            {
                return _YValuesColumn;
            }
            set
            {
                _YValuesColumn = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [Category("Data")]
        [DisplayName("Column for X Values")]
        public string XValuesColumn
        {
            get
            {
                return _XValuesColumn;
            }
            set
            {
                _XValuesColumn = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [Category("Data")]
        [DisplayName("Column for Label Values")]
        public string LabelColumn
        {
            get
            {
                return _LabelColumn;
            }
            set
            {
                _LabelColumn = value;
            }
        }
        [XSerializable]
        [ReportBindable]
        [Category("Data")]
        [DisplayName("Column for Label Values to concatenate")]
        public string LabelColumn2
        {
            get
            {
                return _LabelColumn2;
            }
            set
            {
                _LabelColumn2 = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [Category("Data")]
        [DisplayName("Column for Color Values")]
        public string ColorColumn
        {
            get
            {
                return _ColorColumn;
            }
            set
            {
                _ColorColumn = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [Category("Data")]
        [DisplayName("Series Name")]
        public string SeriesName
        {
            get
            {
                return _SeriesName;
            }
            set
            {
                _SeriesName = value;
            }
        }
        [XSerializable]
        [ReportBindable]
        [Category("Data")]
        [DisplayName("Table for Series Names")]
        public string SeriesNamesTable
        {
            get
            {
                return _SeriesNamesTable;
            }
            set
            {
                _SeriesNamesTable = value;
            }
        }

        [XSerializable]
        [ReportBindable]
        [Category("Data")]
        [DisplayName("Column for Series Name")]
        public string SeriesNameColumn
        {
            get
            {
                return _SeriesNameColumn;
            }
            set
            {
                _SeriesNameColumn = value;
            }
        }
    }

    /// <summary>
    /// Defines a shape control for an Aquarius Chart.
    /// </summary>
    [ToolboxBitmap(typeof(AquariusChart))]
    public class AquariusChart : Box
    {
        private Chart _Chart = null;
        private Chart _CurrentChart = null;

        private string _ChartTitle = "";
        private string _LegendTitle = "";
        private string _XAxisTitle = "";
        private XAxisDateFormat _XAxisDateFormat = XAxisDateFormat.GeneralDateShortTime;
        private bool _XAxisReverse = false;
        private bool _XAxisLogScale = false;
        private bool _YAxisReverse = false;
        private bool _SecondaryYAxisReverse = false;
        private bool _YAxisLogScale = false;
        private bool _SecondaryYAxisLogScale = false;
        private string _YAxisTitle = "";
        private string _YAxisMaximum = "";
        private string _YAxisMinimum = "";
        private string _SecondaryYAxisMaximum = "";
        private string _SecondaryYAxisMinimum = "";
        private string _SecondaryYAxisTitle = "";
        private LegendDocking _LegendPosition = LegendDocking.Top;
        private bool _LegendVisible = true;
        private AutoBool _LegendReversed = AutoBool.Auto;
        private Color _ChartBackColor = Color.WhiteSmoke;
        private Color _ChartAreaBackColor = Color.White;
        private Color _ChartGridLinesColor = Color.DarkGray;
        private FontDescriptor _ChartTitleFont = new FontDescriptor("Arial", 14);
        private Color _ChartTitleFontColor = Color.Black;
        private FontDescriptor _LegendTitleFont = new FontDescriptor("Arial", 12);
        private Color _LegendTitleFontColor = Color.Black;
        private FontDescriptor _LegendItemFont = new FontDescriptor("Arial", 10);
        private Color _LegendItemFontColor = Color.Black;
        private FontDescriptor _XAxisTitleFont = new FontDescriptor("Arial", 10);
        private Color _XAxisTitleFontColor = Color.Black;
        private FontDescriptor _XAxisLabelFont = new FontDescriptor("Arial", 8);
        private Color _XAxisLabelFontColor = Color.Black;
        private FontDescriptor _YAxisTitleFont = new FontDescriptor("Arial", 10);
        private Color _YAxisLabelFontColor = Color.Black;
        private FontDescriptor _YAxisLabelFont = new FontDescriptor("Arial", 8);
        private Color _YAxisTitleFontColor = Color.Black;
        private FontDescriptor _SecondaryYAxisTitleFont = new FontDescriptor("Arial", 10);
        private Color _SecondaryYAxisLabelFontColor = Color.Black;
        private FontDescriptor _SecondaryYAxisLabelFont = new FontDescriptor("Arial", 8);
        private Color _SecondaryYAxisTitleFontColor = Color.Black;

        public List<SeriesDescription> _SeriesDescription = new List<SeriesDescription>();

        public AquariusChart()
        {
            _Chart = new Chart();
            InitChart(_Chart);

            Series s = new Series();
            s.Color = Color.Blue;
            s.MarkerColor = Color.Blue;
            s.Type = SeriesChartType.Line;
            s.MarkerStyle = MarkerStyle.Circle;
            s.MarkerSize = 20;
            s.BorderWidth = 1;
            s.XValueType = ChartValueTypes.DateTime;

            DateTime dd = DateTime.Now;
            double dt = dd.ToOADate();
            DataPoint p = new DataPoint(dt++, 2);
            s.Points.Add(p);
            p = new DataPoint(dt++, 4);
            s.Points.Add(p);
            p = new DataPoint(dt++, 3);
            s.Points.Add(p);
            p = new DataPoint(dt++, 6);
            s.Points.Add(p);

            _Chart.Series.Add(s);
        }
        public void InitChart(Chart c)
        {
            c.SuppressExceptions = true;
            ChartArea chartArea = new ChartArea();
            c.ChartAreas.Add(chartArea);
            c.Titles.Add(new Title(""));
            c.BackColor = _ChartBackColor;
            chartArea.BackColor = _ChartAreaBackColor;
            chartArea.AxisX.ScrollBar.ButtonColor = Color.Gray;
            chartArea.AxisX.ScrollBar.LineColor = Color.Black;
            chartArea.AxisY.ScrollBar.ButtonColor = Color.Gray;
            chartArea.AxisY.ScrollBar.LineColor = Color.Black;
            chartArea.AxisX.LineColor = _ChartGridLinesColor;
            chartArea.AxisY.LineColor = _ChartGridLinesColor;
            chartArea.AxisY.MajorGrid.LineColor = _ChartGridLinesColor;
            chartArea.AxisX.MajorGrid.LineColor = _ChartGridLinesColor;
            chartArea.AxisY.MajorTickMark.LineColor = _ChartGridLinesColor;
            chartArea.AxisX.MajorTickMark.LineColor = _ChartGridLinesColor;
            chartArea.AxisY2.LineColor = _ChartGridLinesColor;
            chartArea.AxisY2.MajorGrid.LineColor = _ChartGridLinesColor;
            chartArea.AxisY2.MajorTickMark.LineColor = _ChartGridLinesColor;
            chartArea.AxisX.LabelStyle.Format = "g";
            chartArea.AxisY.StartFromZero = false;
            chartArea.AxisY2.StartFromZero = false;
            Legend legend = c.Legends[0]; // created automatically
            legend.BackColor = Color.Transparent;
            legend.Reversed = AutoBool.False;
            legend.Docking = LegendDocking.Top;
            legend.Font = _LegendItemFont.GetFont();
            legend.TitleFont = _LegendTitleFont.GetFont();
        }
        [Browsable(false)]
        public Chart Chart
        {
            get { return _Chart; }
            set { _Chart = value; }
        }

        [Category("Chart Properties - Data Series")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Series")]
        [Editor(typeof(SeriesCollectionEditor), typeof(UITypeEditor))]
        public List<SeriesDescription> Series
        {
            get { return _SeriesDescription; }
            set { _SeriesDescription = value; }
        }

        [Category("Chart Properties - Title")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Text")]
        public string ChartTitle
        {
            get { return _ChartTitle; }
            set { _ChartTitle = value;}
       }

        [Category("Chart Properties - Title")]
        [XSerializable]
        [ReportBindable]
        [TypeConverter("FontDescriptorConverter")]
        [DisplayName("Font")]
        public FontDescriptor ChartTitleFont
        {
            get { return _ChartTitleFont; }
            set { _ChartTitleFont = value; }
        }

        [Category("Chart Properties - Title")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Color")]
        public Color ChartTitleFontColor
        {
            get { return _ChartTitleFontColor; }
            set { _ChartTitleFontColor = value; }
        }

        [Category("Chart Properties - Colors")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Background Color")]
        public Color ChartBackColor
        {
            get { return _ChartBackColor; }
            set { _ChartBackColor = value; }
        }

        [Category("Chart Properties - Colors")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Grid and Axes Color")]
        public Color ChartGridLinesColor
        {
            get { return _ChartGridLinesColor; }
            set { _ChartGridLinesColor = value; }
        }

        [Category("Chart Properties - Colors")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Plot Background Color")]
        public Color ChartAreaBackColor
        {
            get { return _ChartAreaBackColor; }
            set { _ChartAreaBackColor = value; }
        }

        [Category("Chart Properties - Legend")]
        [XSerializable]
        [ReportBindable]
        [TypeConverter("FontDescriptorConverter")]
        [DisplayName("Item Font")]
        public FontDescriptor LegendItemFont
        {
            get
            {
                return _LegendItemFont;
            }
            set
            {
                _LegendItemFont = value;
            }
        }

        [Category("Chart Properties - Legend")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Item Font Color")]
        public Color LegendItemFontColor
        {
            get
            {
                return _LegendItemFontColor;
            }
            set
            {
                _LegendItemFontColor = value;
            }
        }

        [Category("Chart Properties - Legend")]
        [XSerializable]
        [ReportBindable]
        [TypeConverter("FontDescriptorConverter")]
        [DisplayName("Title Font")]
        public FontDescriptor LegendTitleFont
        {
            get
            {
                return _LegendTitleFont;
            }
            set
            {
                _LegendTitleFont = value;
            }
        }

        [Category("Chart Properties - Legend")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Title Font Color")]
        public Color LegendTitleFontColor
        {
            get
            {
                return _LegendTitleFontColor;
            }
            set
            {
                _LegendTitleFontColor = value;
            }
        }

        [Category("Chart Properties - Legend")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Title Text")]
        public string LegendTitle
        {
           get
           {
               return _LegendTitle;
           }
           set
           {
               _LegendTitle = value;
           }
        }

        [Category("Chart Properties - Legend")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Position")]
        public LegendDocking LegendPosition
        {
           get
           {
               return _LegendPosition;
           }
           set
           {
               _LegendPosition = value;
           }
        }

        [Category("Chart Properties - Legend")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Visible")]
        public bool LegendVisible
        {
            get { return _LegendVisible; }
            set { _LegendVisible = value; }
        }

        [Category("Chart Properties - Legend")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Items Reversed")]
        public AutoBool LegendReversed
        {
            get { return _LegendReversed; }
            set { _LegendReversed = value; }
        }

        [Category("Chart Properties - X Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Title Text")]
        public string XAxisTitle
        {
            get
            {
                return _XAxisTitle;
            }
            set
            {
                _XAxisTitle = value;
            }
        }

        [Category("Chart Properties - X Axis")]
        [XSerializable]
        [ReportBindable]
        [TypeConverter("FontDescriptorConverter")]
        [DisplayName("Title Font")]
        public FontDescriptor XAxisTitleFont
        {
           get
           {
               return _XAxisTitleFont;
           }
           set
           {
               _XAxisTitleFont = value;
           }
        }

        [Category("Chart Properties - X Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Title Font Color")]
        public Color XAxisTitleFontColor
        {
           get
           {
               return _XAxisTitleFontColor;
           }
           set
           {
               _XAxisTitleFontColor = value;
           }
        }

        [Category("Chart Properties - X Axis")]
        [XSerializable]
        [ReportBindable]
        [TypeConverter("FontDescriptorConverter")]
        [DisplayName("Label Font")]
        public FontDescriptor XAxisLabelFont
        {
           get
           {
               return _XAxisLabelFont;
           }
           set
           {
               _XAxisLabelFont = value;
           }
        }

        [Category("Chart Properties - X Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Label Font Color")]
        public Color XAxisLabelFontColor
        {
           get
           {
               return _XAxisLabelFontColor;
           }
           set
           {
               _XAxisLabelFontColor = value;
           }
        }

        [Category("Chart Properties - X Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Reverse")]
        public bool XAxisReverse
        {
           get { return _XAxisReverse; }
           set { _XAxisReverse = value; }
        }

        [Category("Chart Properties - X Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Log Scale")]
        public bool XAxisLogScale
        {
            get { return _XAxisLogScale; }
            set { _XAxisLogScale = value; }
        }

        [Category("Chart Properties - X Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Date Format")]
        public XAxisDateFormat XAxisDateFormat
        {
           get { return _XAxisDateFormat; }
           set { _XAxisDateFormat = value; }
        }

        [Category("Chart Properties - Y Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Title Text")]
        public string YAxisTitle
        {
           get
           {
               return _YAxisTitle;
           }
           set
           {
               _YAxisTitle = value;
           }
        }
        [Category("Chart Properties - Y Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Minimum")]
        public string YAxisMinimum
        {
            get { return _YAxisMinimum; }
            set { _YAxisMinimum = value; }
        }
        [Category("Chart Properties - Y Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Maximum")]
        public string YAxisMaximum
        {
            get { return _YAxisMaximum; }
            set { _YAxisMaximum = value; }
        }

        [Category("Chart Properties - Y Axis")]
        [XSerializable]
        [ReportBindable]
        [TypeConverter("FontDescriptorConverter")]
        [DisplayName("Title Font")]
        public FontDescriptor YAxisTitleFont
        {
           get
           {
               return _YAxisTitleFont;
           }
           set
           {
               _YAxisTitleFont = value;
           }
        }

        [Category("Chart Properties - Y Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Title Font Color")]
        public Color YAxisTitleFontColor
        {
           get
           {
               return _YAxisTitleFontColor;
           }
           set
           {
               _YAxisTitleFontColor = value;
           }
        }

        [Category("Chart Properties - Y Axis")]
        [XSerializable]
        [ReportBindable]
        [TypeConverter("FontDescriptorConverter")]
        [DisplayName("Label Font")]
        public FontDescriptor YAxisLabelFont
        {
            get
            {
                return _YAxisLabelFont;
            }
            set
            {
                _YAxisLabelFont = value;
            }
        }

        [Category("Chart Properties - Y Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Label Font Color")]
        public Color YAxisLabelFontColor
        {
            get
            {
                return _YAxisLabelFontColor;
            }
            set
            {
                _YAxisLabelFontColor = value;
            }
        }

        [Category("Chart Properties - Y Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Reverse")]
        public bool YAxisReverse
        {
            get { return _YAxisReverse; }
            set { _YAxisReverse = value; }
        }

        [Category("Chart Properties - Y Axis")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Log Scale")]
        public bool YAxisLogScale
        {
            get { return _YAxisLogScale; }
            set { _YAxisLogScale = value; }
        }

        [Category("Chart Properties - Y Axis Secondary")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Title Text")]
        public string SecondaryYAxisTitle
        {
            get
            {
                return _SecondaryYAxisTitle;
            }
            set
            {
                _SecondaryYAxisTitle = value;
            }
        }
        [Category("Chart Properties - Y Axis Secondary")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Minimum")]
        public string SecondaryYAxisMinimum
        {
            get { return _SecondaryYAxisMinimum; }
            set { _SecondaryYAxisMinimum = value; }
        }
        [Category("Chart Properties - Y Axis Secondary")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Maximum")]
        public string SecondaryYAxisMaximum
        {
            get { return _SecondaryYAxisMaximum; }
            set { _SecondaryYAxisMaximum = value; }
        }
        [Category("Chart Properties - Y Axis Secondary")]
        [XSerializable]
        [ReportBindable]
        [TypeConverter("FontDescriptorConverter")]
        [DisplayName("Title Font")]
        public FontDescriptor SecondaryYAxisTitleFont
        {
            get
            {
                return _SecondaryYAxisTitleFont;
            }
            set
            {
                _SecondaryYAxisTitleFont = value;
            }
        }

        [Category("Chart Properties - Y Axis Secondary")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Title Font Color")]
        public Color SecondaryYAxisTitleFontColor
        {
            get {
                return _SecondaryYAxisTitleFontColor;
            }
            set
            {
                _SecondaryYAxisTitleFontColor = value;
            }
        }

        [Category("Chart Properties - Y Axis Secondary")]
        [XSerializable]
        [ReportBindable]
        [TypeConverter("FontDescriptorConverter")]
        [DisplayName("Label Font")]
        public FontDescriptor SecondaryYAxisLabelFont
        {
            get { return _SecondaryYAxisLabelFont; }
            set { _SecondaryYAxisLabelFont = value; }
        }

        [Category("Chart Properties - Y Axis Secondary")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Label Font Color")]
        public Color SecondaryYAxisLabelFontColor
        {
            get { return _SecondaryYAxisLabelFontColor; }
            set { _SecondaryYAxisLabelFontColor = value; }
        }

        [Category("Chart Properties - Y Axis Secondary")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Reverse")]
        public bool SecondaryYAxisReverse
        {
            get { return _SecondaryYAxisReverse; }
            set { _SecondaryYAxisReverse = value; }
        }

        [Category("Chart Properties - Y Axis Secondary")]
        [XSerializable]
        [ReportBindable]
        [DisplayName("Log Scale")]
        public bool SecondaryYAxisLogScale
        {
            get { return _SecondaryYAxisLogScale; }
            set { _SecondaryYAxisLogScale = value; }
        }

        /// <summary>
        /// Sets the predefined values for the properties of a object 
        /// created with visual tools such as Report Designer.
        /// </summary>
        public override void InitNew()
        {
            // Override if needed.
        }

        public override void Prepare()
        {
            // save the handle to this Chart object instance (this is the one for the design template)
            Chart templateChart = this.Chart;

            Chart chartToRender = new Chart();
            InitChart(chartToRender);

            this.Chart = chartToRender;
            _CurrentChart = null;

            // Prepare() will call this control's GenerateScript (if one exists) 
            // which allows the user to directly modify the Chart object that will be passed into Render
            // generate script, for instance, could set properties not available through the property grid
            // aquariusChart1.Chart.ChartAreas[0].YAxis.Interval = 10;

            base.Prepare();

            this.Chart = templateChart; // put this Chart back

            _CurrentChart = chartToRender; // this will be used later in Render method
        }

        /// <summary>
        /// Paints the control.
        /// </summary>
        /// <param name="args"></param>
        protected override void PaintContent(PaintArguments args, RectangleF contentRectangle)
        {
            Graphics g = args.Graphics;
            var rect = contentRectangle;
            Rectangle r = new Rectangle((int)contentRectangle.X, (int)contentRectangle.Y, (int)contentRectangle.Width, (int)contentRectangle.Height);
            _Chart.Printing.PrintPaint(g, r);
        }

        /// <summary>
        /// Fills the properties of the final rendered shape.
        /// </summary>
        /// <param name="production"></param>
        protected override void PopulateProperties(ReportControl production)
        {
            base.PopulateProperties(production);
            AquariusChart p = production as AquariusChart;

            p.Name = this.Name;
            p.Series = this.Series;

            p.ChartTitle = this.ChartTitle;
            p.XAxisTitle = this.XAxisTitle;
            p.YAxisTitle = this.YAxisTitle;
            p.YAxisMinimum = this.YAxisMinimum;
            p.YAxisMaximum = this.YAxisMaximum;
            p.SecondaryYAxisTitle = this.SecondaryYAxisTitle;
            p.SecondaryYAxisMinimum = this.SecondaryYAxisMinimum;
            p.SecondaryYAxisMaximum = this.SecondaryYAxisMaximum;

            p.ChartTitleFont = this.ChartTitleFont;
            p.LegendItemFont = this.LegendItemFont;
            p.LegendTitleFont = this.LegendTitleFont;

            p.XAxisTitleFont = this.XAxisTitleFont;
            p.XAxisLabelFont = this.XAxisLabelFont;
            p.YAxisTitleFont = this.YAxisTitleFont;
            p.YAxisLabelFont = this.YAxisLabelFont;
            p.SecondaryYAxisTitleFont = this.SecondaryYAxisTitleFont;
            p.SecondaryYAxisLabelFont = this.SecondaryYAxisLabelFont;

            p.ChartGridLinesColor = this.ChartGridLinesColor;
            p.ChartBackColor = this.ChartBackColor;
            p.ChartAreaBackColor = this.ChartAreaBackColor;

            p.ChartTitleFontColor = this.ChartTitleFontColor;
            p.LegendItemFontColor = this.LegendItemFontColor;
            p.LegendTitleFontColor = this.LegendTitleFontColor;

            p.XAxisTitleFontColor = this.XAxisTitleFontColor;
            p.XAxisLabelFontColor = this.XAxisLabelFontColor;
            p.YAxisTitleFontColor = this.YAxisTitleFontColor;
            p.YAxisLabelFontColor = this.YAxisLabelFontColor;
            p.SecondaryYAxisTitleFontColor = this.SecondaryYAxisTitleFontColor;
            p.SecondaryYAxisLabelFontColor = this.SecondaryYAxisLabelFontColor;

            p.LegendTitle = this.LegendTitle;
            p.LegendPosition = this.LegendPosition;
            p.LegendVisible = this.LegendVisible;
            p.LegendReversed = this.LegendReversed;

            p.XAxisDateFormat = this.XAxisDateFormat;
            p.XAxisReverse = this.XAxisReverse;
            p.XAxisLogScale = this.XAxisLogScale;

            p.YAxisReverse = this.YAxisReverse;
            p.SecondaryYAxisReverse = this.SecondaryYAxisReverse;
            p.YAxisLogScale = this.YAxisLogScale;
            p.SecondaryYAxisLogScale = this.SecondaryYAxisLogScale;
        }

        public string GetDateFormatAsString(XAxisDateFormat format)
        {
            string ret = "g";
            switch (format)
            {
                case XAxisDateFormat.FullDateLongTime:
                    ret = "F";
                    break;
                case XAxisDateFormat.FullDateShortTime:
                    ret = "f";
                    break;
                case XAxisDateFormat.GeneralDateLongTime:
                    ret = "G";
                    break;
                case XAxisDateFormat.GeneralDateShortTime:
                    ret = "g";
                    break;
                case XAxisDateFormat.LongDate:
                    ret = "D";
                    break;
                case XAxisDateFormat.LongTime:
                    ret = "T";
                    break;
                case XAxisDateFormat.ShortDate:
                    ret = "d";
                    break;
                case XAxisDateFormat.ShortTime:
                    ret = "t";
                    break;
                case XAxisDateFormat.Month:
                    ret = DateTimeFormat.MonthName;
                    break;
                case XAxisDateFormat.Year:
                    ret = DateTimeFormat.Year;
                    break;
                case XAxisDateFormat.MonthYear:
                    ret = DateTimeFormat.YearMonth;
                    break;
                case XAxisDateFormat.DayMonth:
                    ret = DateTimeFormat.MonthDay;
                    break;
                case XAxisDateFormat.GeneralNumber:
                    ret = "g";
                    break;
            }
            return ret;
        }

        /// <summary>
        /// Renders the shape to the final document.
        /// </summary>
        /// 
        public void SetSeries(AquariusChart aqChart)
        {
            ObjectPointerCollection c = aqChart.Engine.Objects;

            _Chart.Titles[0].Text = _ChartTitle;
            Legend legend = _Chart.Legends[0];
            ChartArea chartArea = _Chart.ChartAreas[0];

            legend.Title = _LegendTitle;

            _Chart.Titles[0].Font = _ChartTitleFont.GetFont();
            _Chart.Titles[0].Color = _ChartTitleFontColor;
            legend.TitleFont = _LegendTitleFont.GetFont();
            legend.TitleColor = _LegendTitleFontColor;
            legend.Font = _LegendItemFont.GetFont();
            legend.FontColor = _LegendItemFontColor;

            legend.Docking = _LegendPosition;
            legend.Enabled = _LegendVisible;
            legend.Reversed = _LegendReversed;

            chartArea.BackColor = _ChartAreaBackColor;
            _Chart.BackColor = _ChartBackColor;

            chartArea.AxisX.Title = _XAxisTitle;
            chartArea.AxisX.TitleFont = _XAxisTitleFont.GetFont();
            chartArea.AxisX.TitleColor = _XAxisTitleFontColor;
            chartArea.AxisX.LabelStyle.Font = _XAxisLabelFont.GetFont();
            chartArea.AxisX.LabelStyle.FontColor = _XAxisLabelFontColor;
            chartArea.AxisX.Reverse = _XAxisReverse;

            if (_XAxisLogScale && _XAxisDateFormat == XAxisDateFormat.GeneralNumber)
            {
                chartArea.AxisX.Logarithmic = _XAxisLogScale;
            }
            else
            {
                chartArea.AxisX.Logarithmic = false;
            }

            if (_XAxisDateFormat != XAxisDateFormat.NotSet)
            {
                chartArea.AxisX.LabelStyle.Format = GetDateFormatAsString(_XAxisDateFormat);
            }

            chartArea.AxisY.Title = _YAxisTitle;
            chartArea.AxisY.TitleFont = _YAxisTitleFont.GetFont();
            chartArea.AxisY.TitleColor = _YAxisTitleFontColor;
            chartArea.AxisY.LabelStyle.Font = _YAxisLabelFont.GetFont();
            chartArea.AxisY.LabelStyle.FontColor = _YAxisLabelFontColor;
            chartArea.AxisY.Logarithmic = _YAxisLogScale;
            chartArea.AxisY2.Logarithmic = _SecondaryYAxisLogScale;
            chartArea.AxisY.Reverse = _YAxisReverse;
            chartArea.AxisY2.Reverse = _SecondaryYAxisReverse;

            try
            {
                double m = double.Parse(_YAxisMaximum);
                if (!double.IsNaN(m))
                    chartArea.AxisY.Maximum = m;
            }
            catch { }
            try
            {
                double m = double.Parse(_YAxisMinimum);
                if (!double.IsNaN(m))
                    chartArea.AxisY.Minimum = m;
            }
            catch { }

            chartArea.AxisY2.Title = _SecondaryYAxisTitle;
            chartArea.AxisY2.TitleFont = _SecondaryYAxisTitleFont.GetFont();
            chartArea.AxisY2.TitleColor = _SecondaryYAxisTitleFontColor;
            chartArea.AxisY2.LabelStyle.Font = _SecondaryYAxisLabelFont.GetFont();
            chartArea.AxisY2.LabelStyle.FontColor = _SecondaryYAxisLabelFontColor;

            try
            {
                double m = double.Parse(_SecondaryYAxisMaximum);
                if (!double.IsNaN(m))
                    chartArea.AxisY2.Maximum = m;
            }
            catch { }
            try
            {
                double m = double.Parse(_SecondaryYAxisMinimum);
                if (!double.IsNaN(m))
                    chartArea.AxisY2.Minimum = m;
            }
            catch { }

            chartArea.AxisX.LineColor = _ChartGridLinesColor;
            chartArea.AxisY.LineColor = _ChartGridLinesColor;
            chartArea.AxisY.MajorGrid.LineColor = _ChartGridLinesColor;
            chartArea.AxisX.MajorGrid.LineColor = _ChartGridLinesColor;
            chartArea.AxisY.MajorTickMark.LineColor = _ChartGridLinesColor;
            chartArea.AxisX.MajorTickMark.LineColor = _ChartGridLinesColor;

            chartArea.AxisY2.LineColor = _ChartGridLinesColor;
            chartArea.AxisY2.MajorGrid.LineColor = _ChartGridLinesColor;
            chartArea.AxisY2.MajorTickMark.LineColor = _ChartGridLinesColor;

            try
            {
                foreach (SeriesDescription sd in this.Series)
                {
                    if ((string.IsNullOrEmpty(sd.ValuesTable)) || (string.IsNullOrEmpty(sd.XValuesColumn)) || (string.IsNullOrEmpty(sd.YValuesColumn)))
                        continue;

                    object o = c[sd.ValuesTable];
                    try
                    {
                        Series series = new Series();
                        series.Type = SeriesChartType.Line;

                        if (_XAxisDateFormat != XAxisDateFormat.GeneralNumber)
                        {
                            series.XValueType = ChartValueTypes.DateTime;
                        }
                        series.EmptyPointStyle.BorderWidth = 0;
                        series.EmptyPointStyle.MarkerStyle = MarkerStyle.None;

                        if (!string.IsNullOrEmpty(sd.SeriesName))
                        {
                            series.LegendText = sd.SeriesName;
                        }
                        else if (!string.IsNullOrEmpty(sd.SeriesNamesTable))
                        {
                            object o2 = c[sd.SeriesNamesTable];
                            if (!string.IsNullOrEmpty(sd.SeriesNameColumn))
                            {
                                if (o2 is DataTable)
                                {
                                    DataTable nms = (DataTable)o2;
                                    DataColumn cc = nms.Columns[sd.SeriesNameColumn];
                                    if ((cc != null) && (nms.Rows.Count > 0))
                                    {

                                        DataRow r = nms.Rows[0];
                                        object or = r.ItemArray[cc.Ordinal];
                                        if (or is string)
                                        {
                                            series.LegendText = (string)or;
                                        }
                                    }
                                }
                                else if (o2 is DataRelation)
                                {
                                    DataRelation r2 = (DataRelation)o2;
                                    DataRow row = null;

                                    ReportComponent par = aqChart.Parent;
                                    if (par is Detail)
                                    {
                                        object di = ((Detail)par).MasterBand.DataItem;
                                        if ((di is DataRow) && (((DataRow)di).Table == r2.ParentTable))
                                        {
                                            row = ((DataRow)di);
                                        }
                                        else if ((di is DataRowView) && (((DataRowView)di).Row.Table == r2.ParentTable))
                                        {
                                            row = ((DataRowView)di).Row;
                                        }
                                    }
                                    DataColumn cc = r2.ParentTable.Columns[sd.SeriesNameColumn];
                                    if ((cc != null) && (row != null))
                                    {
                                        object or = row.ItemArray[cc.Ordinal];
                                        if (or != null)
                                        {
                                            series.LegendText = or.ToString();
                                        }
                                    }
                                }
                            }
                        }
                        series.Color = sd.Color;
                        series.MarkerColor = sd.MarkerColor;
                        series.MarkerBorderColor = sd.MarkerBorderColor;
                        series.MarkerStyle = sd.MarkerStyle;
                        series.MarkerSize = sd.MarkerSize;
                        series.BorderWidth = sd.LineWidth;

                        series.Type = (SeriesChartType)sd.LineType;

                        series.YAxisType = sd.YAxisType;
                        series.ShowInLegend = sd.ShowInLegend;

                        Type t = o.GetType();

                        if (o is DataTable)
                        {
                            DataTable tab = (DataTable)o;
                            DataRowCollection tableRows = tab.Rows;

                            int ordX = Ordinal(tab, sd.XValuesColumn);
                            int ordY = Ordinal(tab, sd.YValuesColumn);
                            int ordL = Ordinal(tab, sd.LabelColumn);
                            int ordL2 = Ordinal(tab, sd.LabelColumn2);
                            int ordC = Ordinal(tab, sd.ColorColumn);

                            if ((ordX != -1) && (ordY != -1))
                            {
                                foreach (DataRow rw in tableRows)
                                {
                                    AddDataPoint(series, rw, ordX, ordY, ordL, ordL2, ordC);
                                }
                            }
                        }
                        else if (o is DataRelation)
                        {
                            DataRelation r = (DataRelation)o;
                            DataRow [] tableRows = null;

                            ReportComponent par = aqChart.Parent;
                            if (par is Detail)
                            {
                                object di = ((Detail)par).MasterBand.DataItem;
                                if ((di is DataRow) && (((DataRow)di).Table == r.ParentTable))
                                {
                                    tableRows = ((DataRow)di).GetChildRows(r);
                                }
                                else if ((di is DataRowView) && (((DataRowView)di).Row.Table == r.ParentTable))
                                {
                                    tableRows = ((DataRowView)di).Row.GetChildRows(r);
                                }
                            }

                            int ordX = Ordinal(r.ChildTable, sd.XValuesColumn);
                            int ordY = Ordinal(r.ChildTable, sd.YValuesColumn);
                            int ordL = Ordinal(r.ChildTable, sd.LabelColumn);
                            int ordL2 = Ordinal(r.ChildTable, sd.LabelColumn2);
                            int ordC = Ordinal(r.ChildTable, sd.ColorColumn);

                            if ((tableRows != null) && (ordX != -1) && (ordY != -1))
                            {
                                foreach (DataRow rw in tableRows)
                                {
                                    AddDataPoint(series, rw, ordX, ordY, ordL, ordL2, ordC);
                                }
                            }
                        }

                        if (series.Points.Count > 0)
                        {
                            _Chart.Series.Add(series);
                        }
                    }
                    catch
                    {
                    }

                }
                if (_XAxisDateFormat == XAxisDateFormat.GeneralNumber)
                {
                    chartArea.AxisX.RoundAxisValues();
                }
            }
            catch
            {
            }
        }
        public int Ordinal(DataTable tab, string columnName)
        {
            int ord = -1;
            if (columnName != null)
            {
                try
                {
                    DataColumn col = tab.Columns[columnName];
                    if (col != null)
                    {
                        ord = col.Ordinal;
                    }
                }
                catch { }
            }

            return ord;
        }
        public void AddDataPoint(Series series, DataRow r, int ordX, int ordY, int ordL, int ordL2, int ordC)
        {
            double dX = double.NaN;
            double dVal = double.NaN;
            string lbl = "";
            object color = null;

            object[] itemArr = r.ItemArray;

            object objectDateTime = itemArr[ordX];
            object objectValue = itemArr[ordY];

            try
            {
                if (objectValue is string)
                {
                    dVal = double.Parse((string)objectValue);
                }
                else if (objectValue is decimal)
                {
                    decimal i = (decimal)objectValue;
                    dVal = (double)i;
                }
                else if (objectValue is int)
                {
                    int i = (int)objectValue;
                    dVal = (double)i;
                }
                else if (objectValue is long)
                {
                    long i = (long)objectValue;
                    dVal = (double)i;
                }
                else if (objectValue is float)
                {
                    float f = (float)objectValue;
                    dVal = (double)f;
                }
                else
                {
                    dVal = (double)objectValue;
                }
            }
            catch
            {
            }
            try
            {
                if (objectDateTime is DateTime)
                {
                    dX = ((DateTime)objectDateTime).ToOADate();
                }
                else if (objectDateTime is decimal)
                {
                    decimal i = (decimal)objectDateTime;
                    dX = (double)i;
                }
                else if (objectDateTime is int)
                {
                    int i = (int)objectDateTime;
                    dX = (double)i;
                }
                else if (objectDateTime is long)
                {
                    long i = (long)objectDateTime;
                    dX = (double)i;
                }
                else if (objectDateTime is float)
                {
                    float f = (float)objectDateTime;
                    dX = (double)f;
                }
                else if (objectDateTime is double)
                {
                    dX = (double)objectDateTime;
                }
                else if (objectDateTime is string)
                {
                    dX = double.Parse((string)objectDateTime);
                }
            }
            catch
            {
            }

            try
            {
                if (ordL > -1)
                {
                    // check special case of the label is the X value's DateTime
                    if ((ordL == ordX) && !double.IsNaN(dX) &&
                        (series.XValueType == ChartValueTypes.DateTime))
                    {
                        lbl = FormatDate(dX);
                    }
                    else
                    {
                        try
                        {
                            object l = itemArr[ordL];
                            lbl = l.ToString();
                        }
                        catch { }
                    }
                }

                if (ordL2 > -1)
                {
                    // check special case of the label is the X value's DateTime
                    if ((ordL2 == ordX) && !double.IsNaN(dX) &&
                        (series.XValueType == ChartValueTypes.DateTime))
                    {
                        if (!string.IsNullOrEmpty(lbl))
                            lbl += "\n";
                        lbl += FormatDate(dX);
                    }
                    else
                    {
                        object l = itemArr[ordL2];
                        string ls = l.ToString();

                        if (!string.IsNullOrEmpty(lbl) && !string.IsNullOrEmpty(ls))
                        {
                            lbl += "\n";
                        }
                        if (!string.IsNullOrEmpty(ls))
                            lbl += ls;
                    }
                }
            }
            catch { }

            try
            {
                if (ordC > -1)
                {
                    object c = itemArr[ordC];
                    string s = c.ToString();

                    if (s.StartsWith("#"))
                    {
                        color = ReportPlugIn.HexStringToDrawingColor(s);
                    }
                    else
                    {
                        Color cc = Color.FromName(s);
                        if (cc.IsKnownColor)
                            color = cc;
                    }
                }
            }
            catch { }

            if (!double.IsNaN(dX))
            {
                DataPoint p = new DataPoint(dX, dVal);

                if (!string.IsNullOrEmpty(lbl))
                {
                    p.Label = lbl;
                }

                if ((color != null) && (color is Color))
                {
                    p.MarkerColor = (Color) color;
                }

                if (double.IsNaN(dVal))
                {
                    p.Empty = true;
                }
                series.Points.Add(p);
            }
        }

        public string FormatDate(double dX)
        {
            DateTime dd = DateTime.FromOADate(dX);
            if ((dd.Hour == 0) && (dd.Minute == 0) && (dd.Second == 0))
            {
                return dd.ToString(DateTimeFormat.Date);
            }
            else
            {
                return dd.ToString(DateTimeFormat.DateTime);
            }
        }

        public override void Render()
        {
            AquariusChart result = new AquariusChart();
            result.Chart.Series.Clear();

            if (_CurrentChart != null)
            {
                // _CurrentChart was made in Prepare() and might contain changes from the GenerateScript
                result.Chart = _CurrentChart;
                _CurrentChart = null;
            }

            PopulateProperties(result);
            RenderLocation(result);

            result.SetSeries(this);
            result.Chart.Name = Name;

            var parm = Document.Parameters["DLLHandles"];
            if (parm != null)
            {
                object dlls = parm.Value;

                if (dlls != null)
                {
                    if (dlls is List<object>)
                    {
                        foreach (object dll in (List<object>) dlls)
                        {
                            /*
                            if (dll is ReportPlugIn)
                            {
                                ((ReportPlugIn) dll).SetChartParameters(result.Chart);
                            }
                            */
                        }
                    }
                }
            }

            Engine.ProductionPage.Controls.Add(result);
        }

        public class SeriesCollectionEditor : CollectionEditor
        {
            public SeriesCollectionEditor(Type type)
                : base(type)
            {
            }

            protected override CollectionEditor.CollectionForm CreateCollectionForm()
            {
                CollectionForm collectionForm = base.CreateCollectionForm();
                Form frmCollectionEditorForm = collectionForm as Form;
                TableLayoutPanel tlpLayout = frmCollectionEditorForm.Controls[0] as TableLayoutPanel;
                if (tlpLayout != null)
                {
                    if (tlpLayout.Controls[5] is PropertyGrid)
                    {
                        PropertyGrid propGrid = tlpLayout.Controls[5] as PropertyGrid;
                        propGrid.PropertySort = PropertySort.NoSort;
                        propGrid.HelpVisible = true;
                    }
                }
                return collectionForm;
            }
        }
    }
}