using System;
using Aquarius.Helpers;
using ConsoleProgressBar;

namespace SamplesObservationExporter
{
    public class ProgressBarReporter : IProgressReporter, IDisposable
    {
        public int ExpectedCount { get; set; }

        private bool ProgressBarCreated { get; set; }
        private ProgressBar ProgressBar { get; set; }
        private int LastTotalCount { get; set; }
        private int CumulativeCount { get; set; }

        public void Dispose()
        {
            if (ProgressBar == null)
                return;

            ProgressBar.Dispose();
            ProgressBar = null;
        }

        private void ShowProgress(int percent, string status = null)
        {
            if (ProgressBar == null)
                return;

            try
            {
                ProgressBar.Progress.Report(percent / 100.0, status);
            }
            catch(Exception)
            {
                // Running from within Powershell ISE tends to complain here, with System.IO.IOException: "The handle is invalid".
                // So just stop trying to do any progress bar things.
                try
                {
                    ProgressBar.Dispose();
                }
                finally
                {
                    ProgressBar = null;
                }
            }
        }

        public void Started()
        {
        }

        public void Progress(int currentCount, int totalCount)
        {
            if (totalCount <= 0)
                return;

            LastTotalCount = totalCount;

            if (!ProgressBarCreated)
            {
                ProgressBarCreated = true;
                ProgressBar = new ProgressBar();
            }

            ShowProgress(100 * (CumulativeCount + currentCount) / ExpectedCount);
        }

        public void Completed()
        {
            CumulativeCount += LastTotalCount;
            LastTotalCount = 0;
        }
    }
}
