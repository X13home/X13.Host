using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace X13 {
  class Program {
    static void Main(string[] args) {
      if(args.Length > 0 && File.Exists(args[0]) && Path.GetExtension(args[0]).ToLower() == ".js") {
        var src = File.ReadAllText(args[0]);
        var c = new X13.CC.EP_Compiler();
        c.CMsg += c_CMsg;
        if(c.Parse(src)) {
          Log.Info("Ok");
        }
      } else {
        Log.Warning("USE: Compiler <sourcer file>.js");
      }
      Log.Finish();
    }

    static void c_CMsg(NiL.JS.MessageLevel level, NiL.JS.Core.CodeCoordinates coords, string message) {
      switch(level) {
      case NiL.JS.MessageLevel.Error:
      case NiL.JS.MessageLevel.CriticalWarning:
        Log.Error("[{0}, {1}] {2}", coords.Line, coords.Column, message);
        break;
      case NiL.JS.MessageLevel.Warning:
        Log.Warning("[{0}, {1}] {2}", coords.Line, coords.Column, message);
        break;
      default:
        Log.Info("[{0}, {1}] {2}", coords.Line, coords.Column, message);
        break;
      }

    }
  }
}
