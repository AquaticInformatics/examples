using System;
using System.Reflection;
using System.Threading;
using log4net;

namespace ObservationReportExporter
{
    public class SingleInstanceGuard : IDisposable
    {
        // ReSharper disable once PossibleNullReferenceException
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Mutex InstanceMutex { get; set; }
        public string Name { get; }
        private bool ShouldRelease { get; set; }

        public SingleInstanceGuard(string name)
        {
            Name = $"{ExeHelper.ExeName}.{name}";
            InstanceMutex = new Mutex(true, Name);
            ShouldRelease = true;
        }

        public bool IsAnotherInstanceRunning()
        {
            try
            {
                var isAnotherInstanceRunning = !InstanceMutex.WaitOne(TimeSpan.Zero, true);

                if (isAnotherInstanceRunning)
                {
                    ShouldRelease = false;
                }

                return isAnotherInstanceRunning;
            }
            catch (AbandonedMutexException)
            {
                Log.Debug($"Previous run of the program did not clear the '{Name}' mutex cleanly.");

                return false;
            }
            catch (Exception ex)
            {
                Log.Warn($"Error occurred while checking if the program is still running:'{ex.Message}'. Will continue.");
            }

            return false;
        }

        public void Dispose()
        {
            Release();

            InstanceMutex?.Dispose();
        }

        private void Release()
        {
            if (!ShouldRelease)
                return;

            try
            {
                InstanceMutex?.ReleaseMutex();
            }
            catch (Exception e)
            {
                Log.Warn($"Can't release mutex '{Name}': {e.Message}");
            }
        }
    }
}
