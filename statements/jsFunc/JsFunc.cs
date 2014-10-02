#region license
//Copyright (c) 2014 Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace X13.PLC.jsFunc {
  [Export(typeof(IStatement))]
  [ExportMetadata("declarer", "jsFunc")]
  public class JsFunc : IStatement {
    DVar<string> _dCode;
    public void Load() {
      using(var sr=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("jsFunc.jsFunc.xst"))) {
        Topic.Import(sr, null);
      }
    }

    public void Init(DVar<PiStatement> model) {
      _dCode=BiultInStatements.AddPin<string>(model, "_code");
      if(!model.Exist("A")) {
        BiultInStatements.AddPin<double>(model, "A");
      }
      if(!model.Exist("Q")) {
        BiultInStatements.AddPin<double>(model, "Q");
      }
    }

    public void Calculate(DVar<PiStatement> model, Topic source) {
    }

    public void DeInit() {
    }
  }
}
