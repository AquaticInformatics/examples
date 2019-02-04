using System;
using System.Collections.Generic;
using System.Linq;

namespace WaterWatchPreProcessor.Filters
{
    public class Filter<TFilter> where TFilter : IFilter
    {
        private List<TFilter> IncludeFilters { get; }
        private List<TFilter> ExcludeFilters { get; }
        public int Count { get; private set; }

        public Filter(List<TFilter> filters)
        {
            IncludeFilters = filters.Where(filter => !filter.Exclude).ToList();
            ExcludeFilters = filters.Where(filter => filter.Exclude).ToList();
        }

        public bool IsFiltered(Func<TFilter, bool> predicate)
        {
            if ((!IncludeFilters.Any() || IncludeFilters.Any(predicate)) && !ExcludeFilters.Any(predicate))
                return false;

            ++Count;
            return true;
        }
    }
}
