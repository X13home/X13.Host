#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

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

namespace X13.lib {
  public class Log {
    static Log() {
      if(!Directory.Exists("../log")) {
        Directory.CreateDirectory("../log");
      }
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
      string msg=string.Format(format, arg);
      DateTime now=DateTime.Now;
      if(Write!=null) {
        Write(ll, now, msg);
      }
    }
    public static event Action<LogLevel, DateTime, string> Write;
  }
  public enum LogLevel {
    Debug,
    Info,
    Warning,
    Error
  }
}
