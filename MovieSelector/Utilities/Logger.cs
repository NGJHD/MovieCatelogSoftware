using System;
using System.Linq;
using System.IO;

namespace MovieSelector
{
    public static class Log
    {
        private static readonly object logLock = new object();

        public enum LogMsgType
        {
            I = 0,
            E,
            D,
        }

        public static void Write(LogMsgType logMsgType, string logMsg)
        {
            try
            {
                lock (logLock)
                {
                    string functionName = "";
                    System.Diagnostics.StackFrame[] stackFrames = new System.Diagnostics.StackTrace().GetFrames();

                    if (logMsgType != LogMsgType.I)
                    {
                        for (int i = (Math.Min(stackFrames.Count() - 1, 4)); i > 0; i--) //Do not log "Write" function (i.e, this!)
                        {
                            functionName += ((System.Diagnostics.StackFrame)stackFrames[i]).GetMethod().Name + (i > 1 ? "." : "");
                        }
                    }
                    else
                    {
                        functionName = stackFrames[1].GetMethod().Name;
                    }

                    File.AppendAllText(GlobalPath.LOG_PATH + "\\" + GlobalPath.LOG_FILENAME + ".log",
                                       "[" + logMsgType.ToString() + "]" +
                                       "[" + DateTime.Now.ToString("yyyyMMdd HHmmssfff") + "]" +
                                       "[" + functionName.PadRight(100) + "]" +
                                       "[" + logMsg + "]" +
                                       Environment.NewLine);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
