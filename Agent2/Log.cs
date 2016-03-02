#region license
//Copyright (c) 2011-2015 Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

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
    private static AutoResetEvent _kickEv;
    private static RegisteredWaitHandle _wh;
    private static System.Collections.Concurrent.ConcurrentQueue<LogRecord> _records;

    static Log() {
      _useDiagnostic=System.Diagnostics.Debugger.IsAttached;
      try { int window_height = Console.WindowHeight; _useConsole=true;}
      catch { _useConsole = false; }
      _records=new System.Collections.Concurrent.ConcurrentQueue<LogRecord>();
      _kickEv=new AutoResetEvent(false);
      _wh=ThreadPool.RegisterWaitForSingleObject(_kickEv, Process, null, -1, false);
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
      _records.Enqueue(new LogRecord() { ll=ll, dt=DateTime.Now, format=format, args=arg });
      _kickEv.Set();
    }
    public static event Action<LogLevel, DateTime, string> Write;
    public static void Finish() {
      _kickEv.Set();
      AutoResetEvent fin=new AutoResetEvent(false);
      _wh.Unregister(fin);
      fin.WaitOne(400);
    }

    private static void Process(object o, bool to) {
      LogRecord r;
      string msg;
      while(_records.TryDequeue(out r)) {
        try {
          msg=string.Format(r.format, r.args);
        }
        catch(Exception) {
          r.ll=LogLevel.Error;
          msg="Bad format: "+r.format;
        }
        if(Write!=null) {
          Write(r.ll, r.dt, msg);
        }
        if(_useConsole || _useDiagnostic) {
          string dts=r.dt.ToString("HH:mm:ss.ff");
          if(_useDiagnostic) {
            System.Diagnostics.Debug.WriteLine(string.Format("{0}[{1}] {2}", dts, r.ll.ToString(), msg));
          }
          if(_useConsole) {
            switch(r.ll) {
            case LogLevel.Debug:
              Console.ForegroundColor=ConsoleColor.Gray;
              Console.WriteLine(dts+"[D] "+msg);
              break;
            case LogLevel.Info:
              Console.ForegroundColor=ConsoleColor.White;
              Console.WriteLine(dts+"[I] "+msg);
              break;
            case LogLevel.Warning:
              Console.ForegroundColor=ConsoleColor.Yellow;
              Console.WriteLine(dts+"[W] "+msg);
              break;
            case LogLevel.Error:
              Console.ForegroundColor=ConsoleColor.Red;
              Console.WriteLine(dts+"[E] "+msg);
              break;
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
