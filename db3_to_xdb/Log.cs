using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  internal static class Log {
    public static void Debug(string format, params object[] arg) {
      Console.ForegroundColor=ConsoleColor.Gray;
      Console.WriteLine("{0}[D] {1}", DateTime.Now.ToString("HH:mm:ss.ff"), string.Format(format, arg));
    }
    public static void Info(string format, params object[] arg) {
      Console.ForegroundColor=ConsoleColor.White;
      Console.WriteLine("{0}[D] {1}", DateTime.Now.ToString("HH:mm:ss.ff"), string.Format(format, arg));
    }
    public static void Warning(string format, params object[] arg) {
      Console.ForegroundColor=ConsoleColor.Yellow;
      Console.WriteLine("{0}[D] {1}", DateTime.Now.ToString("HH:mm:ss.ff"), string.Format(format, arg));
    }
    public static void Error(string format, params object[] arg) {
      Console.ForegroundColor=ConsoleColor.Red;
      Console.WriteLine("{0}[D] {1}", DateTime.Now.ToString("HH:mm:ss.ff"), string.Format(format, arg));
    }
  }
}
