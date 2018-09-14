using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using log4net;

namespace SosExporter
{
    public class TimeSeriesPointFilter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        public void FilterTimeSeriesPoints(TimeSeriesDataServiceResponse timeSeries)
        {
            if (!Context.Config.Approvals.Any() && !Context.Config.Grades.Any() && !Context.Config.Qualifiers.Any())
                return;

            var approvalFilter = new Filter<ApprovalFilter>(Context.Config.Approvals);
            var gradeFilter = new Filter<GradeFilter>(Context.Config.Grades);
            var qualifierFilter = new Filter<QualifierFilter>(Context.Config.Qualifiers);

            var filteredPoints = new List<TimeSeriesPoint>();

            foreach (var point in timeSeries.Points)
            {
                var approval = timeSeries.Approvals.Single(a => IsPointWithinTimeRange(point, a));
                var grade = timeSeries.Grades.Single(g => IsPointWithinTimeRange(point, g));
                var qualifiers = timeSeries.Qualifiers.Where(q => IsPointWithinTimeRange(point, q)).ToList();

                if (approvalFilter.IsFiltered(f => IsApprovalFiltered(approval, f))) continue;
                if (gradeFilter.IsFiltered(f => IsGradeFiltered(grade, f))) continue;
                if (qualifierFilter.IsFiltered(f => qualifiers.Any(q => IsQualifierFiltered(q, f)))) continue;

                filteredPoints.Add(point);
            }

            if (timeSeries.NumPoints == filteredPoints.Count)
                return;

            var appliedFilters = new[]
                {
                    approvalFilter.Count > 0 ? "approval" : string.Empty,
                    gradeFilter.Count > 0 ? "grade" : string.Empty,
                    qualifierFilter.Count > 0 ? "qualifier" : string.Empty,
                }
                .Where(s => !string.IsNullOrEmpty(s));

            Log.Info($"Excluded {timeSeries.NumPoints - filteredPoints.Count} of {timeSeries.NumPoints} points due to {string.Join(" and ", appliedFilters)} filters.");

            timeSeries.NumPoints = filteredPoints.Count;
            timeSeries.Points = filteredPoints;
        }

        private static bool IsPointWithinTimeRange(TimeSeriesPoint point, TimeRange timeRange)
        {
            return timeRange.StartTime <= point.Timestamp.DateTimeOffset && point.Timestamp.DateTimeOffset < timeRange.EndTime;
        }

        private static bool IsApprovalFiltered(Approval approval, ApprovalFilter filter)
        {
            switch (filter.ComparisonType)
            {
                case ComparisonType.LessThan: return approval.ApprovalLevel < filter.ApprovalLevel;
                case ComparisonType.LessThanEqual: return approval.ApprovalLevel <= filter.ApprovalLevel;
                case ComparisonType.Equal: return approval.ApprovalLevel == filter.ApprovalLevel;
                case ComparisonType.GreaterThanEqual: return approval.ApprovalLevel >= filter.ApprovalLevel;
                case ComparisonType.GreaterThan: return approval.ApprovalLevel > filter.ApprovalLevel;
            }

            throw new ArgumentException($"Unknown ComparisonType={filter.ComparisonType}", nameof(filter));
        }

        private static bool IsGradeFiltered(Grade grade, GradeFilter filter)
        {
            var gradeCode = int.Parse(grade.GradeCode);

            switch (filter.ComparisonType)
            {
                case ComparisonType.LessThan: return gradeCode < filter.GradeCode;
                case ComparisonType.LessThanEqual: return gradeCode <= filter.GradeCode;
                case ComparisonType.Equal: return gradeCode == filter.GradeCode;
                case ComparisonType.GreaterThanEqual: return gradeCode >= filter.GradeCode;
                case ComparisonType.GreaterThan: return gradeCode > filter.GradeCode;
            }

            throw new ArgumentException($"Unknown ComparisonType={filter.ComparisonType}", nameof(filter));
        }

        private static bool IsQualifierFiltered(Qualifier qualifier, QualifierFilter filter)
        {
            return qualifier.Identifier == filter.Text;
        }
    }
}
