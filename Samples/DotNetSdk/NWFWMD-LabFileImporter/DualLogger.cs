using System;
using ServiceStack.Logging;

namespace NWFWMDLabFileImporter
{
    public class DualLogger : ILog
    {
        public Action<string> InfoAction { get; set; }
        public Action<string> WarnAction { get; set; }
        public Action<string> ErrorAction { get; set; }

        public void Debug(object message)
        {
            InfoAction($"DEBUG: {message}");
        }

        public void Debug(object message, Exception exception)
        {
            InfoAction($"DEBUG: {message} Exception: {exception.Message}");
        }

        public void DebugFormat(string format, params object[] args)
        {
            InfoAction($"DEBUG: {string.Format(format, args)}");
        }

        public void Error(object message)
        {
            ErrorAction($"{message}");
        }

        public void Error(object message, Exception exception)
        {
            ErrorAction($"{message}: {exception.Message}\n{exception.StackTrace}");
        }

        public void ErrorFormat(string format, params object[] args)
        {
            ErrorAction($"{string.Format(format, args)}");
        }

        public void Fatal(object message)
        {
            ErrorAction($"FATAL: {message}");
        }

        public void Fatal(object message, Exception exception)
        {
            ErrorAction($"FATAL: {message}: {exception.Message}\n{exception.StackTrace}");
        }

        public void FatalFormat(string format, params object[] args)
        {
            ErrorAction($"FATAL: {string.Format(format, args)}");
        }

        public void Info(object message)
        {
            InfoAction($"{message}");
        }

        public void Info(object message, Exception exception)
        {
            InfoAction($"{message}: {exception.Message}\n{exception.StackTrace}");
        }

        public void InfoFormat(string format, params object[] args)
        {
            InfoAction($"{string.Format(format, args)}");
        }

        public void Warn(object message)
        {
            WarnAction($"{message}");
        }

        public void Warn(object message, Exception exception)
        {
            WarnAction($"{message}: {exception.Message}\n{exception.StackTrace}");
        }

        public void WarnFormat(string format, params object[] args)
        {
            WarnAction($"{string.Format(format, args)}");
        }

        public bool IsDebugEnabled => false;
    }
}
