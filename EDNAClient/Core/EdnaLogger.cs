using System;
using System.IO;
using System.Text;
using System.Threading;

namespace EDNAClient.Core
{
    public static class EdnaLogger
    {
        private static StreamWriter? _writer;
        private static readonly object _lock = new object();

        public static void Init(string logsDir)
        {
            Directory.CreateDirectory(logsDir);
            var ts   = DateTime.Now.ToString("yyMMdd-HHmmss");
            var path = Path.Combine(logsDir, $"EDNA_{ts}.log");
            _writer  = new StreamWriter(path, append: false, Encoding.UTF8) { AutoFlush = true };
        }

        public static void Close()
        {
            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        public static bool DetailEnabled { get; set; }

        public static void Log(string msg)    => Write("-LOG-", msg);
        public static void Warn(string msg)   => Write("-WRN-", msg);
        public static void Detail(string msg) { if (DetailEnabled) Write("-DTL-", msg); }

        public static void Error(string msg, Exception? ex = null)
        {
            var full = ex == null ? msg : $"{msg}: [{ex.GetType().Name}] {ex.Message}";
            Write("-ERR-", full);
        }

        private static void Write(string level, string msg)
        {
            var now = DateTime.Now;
            var tid = Thread.CurrentThread.ManagedThreadId;
            var line = $"{now:dd-HH:mm:ss.fff} {tid:00}_00 {level} {msg}";
            lock (_lock)
            {
                if (_writer == null) throw new InvalidOperationException("EdnaLogger.Init has not been called");
                _writer.WriteLine(line);
            }
        }
    }
}
