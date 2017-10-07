using Log4NetILog = log4net.ILog;
using PluginILog = Server.BusinessInterfaces.FieldDataPluginCore.ILog;

namespace PluginTester
{
    public class Log4NetLogger : PluginILog
    {
        public static PluginILog Create(Log4NetILog log)
        {
            return new Log4NetLogger(log);
        }

        private readonly Log4NetILog _log;

        private Log4NetLogger(Log4NetILog log)
        {
            _log = log;
        }

        public void Info(string message)
        {
            _log.Info(message);
        }

        public void Warn(string message)
        {
            _log.Warn(message);
        }

        public void Error(string message)
        {
            _log.Error(message);
        }
    }
}
