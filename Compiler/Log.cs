///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13 {
  public class Log {
    private static bool _useDiagnostic;
    private static bool _useConsole;
    public static bool useFile;
    private static AutoResetEvent _kickEv;
    private static RegisteredWaitHandle _wh;
    private static System.Collections.Concurrent.ConcurrentQueue<LogRecord> _records;
    private static string _lfPath;
    private static DateTime _firstDT;
    private static string _lfMask;

    static Log() {
      _useDiagnostic = System.Diagnostics.Debugger.IsAttached;
      try { int window_height = Console.WindowHeight; _useConsole = true; }
      catch { _useConsole = false; }
      if(!Directory.Exists("../log")) {
        Directory.CreateDirectory("../log");
      }
      useFile = true;
      _lfMask = "../log/{0}_" + Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location) + ".log";
      _records = new System.Collections.Concurrent.ConcurrentQueue<LogRecord>();
      _kickEv = new AutoResetEvent(false);
      _wh = ThreadPool.RegisterWaitForSingleObject(_kickEv, Process, null, -1, false);
    }
    public static void Debug(string format, params object[] arg) {
      onWrite(LogLevel.Debug, format, arg);
    }
    public static void Info(string format, params object[] arg) {
      onWrite(LogLevel.Info, format, arg);
    }
    public static void Warning(string format, params object[] arg) {
      onWrite(LogLevel.Warning, format, arg);
    }
    public static void Error(string format, params object[] arg) {
      onWrite(LogLevel.Error, format, arg);
    }
    public static void onWrite(LogLevel ll, string format, params object[] arg) {
      _records.Enqueue(new LogRecord() { ll = ll, dt = DateTime.Now, format = format, args = arg });
      _kickEv.Set();
    }
    public static event Action<LogLevel, DateTime, string> Write;
    public static void Finish() {
      _kickEv.Set();
      AutoResetEvent fin = new AutoResetEvent(false);
      _wh.Unregister(fin);
      fin.WaitOne(400);
    }

    private static void Process(object o, bool to) {
      LogRecord r;
      string msg;
      while(_records.TryDequeue(out r)) {
        try {
          msg = string.Format(r.format, r.args);
        }
        catch(Exception) {
          r.ll = LogLevel.Error;
          msg = "Bad format: " + r.format;
        }
        if(Write != null) {
          Write(r.ll, r.dt, msg);
        }
        string msgA;
        ConsoleColor cc;
        switch(r.ll) {
        case LogLevel.Info:
          cc = ConsoleColor.White;
          msgA = r.dt.ToString("HH:mm:ss.ff") + "[I] " + msg;
          break;
        case LogLevel.Warning:
          cc = ConsoleColor.Yellow;
          msgA = r.dt.ToString("HH:mm:ss.ff") + "[W] " + msg;
          break;
        case LogLevel.Error:
          cc = ConsoleColor.Red;
          msgA = r.dt.ToString("HH:mm:ss.ff") + "[E] " + msg;
          break;
        default:
          msgA = r.dt.ToString("HH:mm:ss.ff") + "[D] " + msg;
          cc = ConsoleColor.Gray;
          break;
        }
        if(_useDiagnostic) {
          System.Diagnostics.Debug.WriteLine(msgA);
        }
        if(_useConsole) {
          Console.ForegroundColor = cc;
          Console.WriteLine(msgA);
        }
        if(useFile) {
          LogLevel lt = LogLevel.Debug;
          if((int)r.ll >= (int)lt) {
            if(_lfPath == null || _firstDT != r.dt.Date) {
              _firstDT = r.dt.Date;
              try {
                string m1 = string.Format(_lfMask, "*");
                foreach(string f in Directory.GetFiles(Path.GetDirectoryName(m1), Path.GetFileName(m1), SearchOption.TopDirectoryOnly)) {
                  if(File.GetLastWriteTime(f).AddDays(20) < _firstDT)
                    File.Delete(f);
                }
              }
              catch(System.IO.IOException) {
              }
              _lfPath = string.Format(_lfMask, _firstDT.ToString("yyMMdd"));
            }
            for(int i = 2; i >= 0; i--) {
              try {
                using(FileStream fs = File.Open(_lfPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) {
                  fs.Seek(0, SeekOrigin.End);
                  byte[] ba = Encoding.UTF8.GetBytes(msgA + "\r\n");
                  fs.Write(ba, 0, ba.Length);
                }
                break;
              }
              catch(System.IO.IOException) {
                Thread.Sleep(15);
              }
            }
          }

        }
      }
    }
    private class LogRecord {
      public LogLevel ll;
      public DateTime dt;
      public string format;
      public object[] args;
    }
  }
  public enum LogLevel {
    Debug,
    Info,
    Warning,
    Error
  }
}
