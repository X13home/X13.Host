#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

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
using System.Text;

namespace X13.PLC {
  [Export(typeof(IStatement))]
  [ExportMetadata("declarer", "Interpolation3")]
  public class Interpolation3 : IStatement {
    private DVar<double> _x;  //input
    private DVar<double> _y;  //output
    private DVar<string> _csv;
    private CubicSpline _cubSpl;
    public void Load() {
      var t1=Topic.root.Get<string>("/etc/declarers/func/Interpolation3");
      t1.value="pack://application:,,/ExpStatements;component/Images/fu_intr3.png";
      t1.Get<string>("X").value="Ag";
      t1.Get<string>("Y").value="ag";
      t1.Get<string>("_description").value="g3Cubic interpolation";

      t1.Get<string>("rename").value="|R";
      t1.Get<string>("remove").value="}D";
    }

    public void Init(DVar<PiStatement> model) {
      _cubSpl=new CubicSpline();
      _x=BiultInStatements.AddPin<double>(model, "X");
      _y=BiultInStatements.AddPin<double>(model, "Y");
      _csv=BiultInStatements.AddPin<string>(model, "_csv");
    }

    public void Calculate(DVar<PiStatement> model, Topic source) {
      if(source==_csv) {
        _cubSpl.Reset();
        string[] dat=null;
        if(File.Exists(_csv.value)) {
          try {
            dat=File.ReadAllLines(_csv.value);
          }
          catch(Exception ex) {
            Log.Warning("{0}.Load({1}) - {2}", model.path, _csv.value, ex.Message);
          }
        } else {
          Log.Warning("{0}.Load({1}) not exist", model.path, _csv.value);
        }
        if(dat==null || dat.Length==0) {
          return;
        }
        double x, y;
        string[] lt;
        for(int i=0; i<dat.Length; i++) {
          lt=dat[i].Split(';');
          if(lt==null || lt.Length!=2) {
            continue;
          }
          if(double.TryParse(lt[0], out x)) {
            if(double.TryParse(lt[1], out y)) {
              _cubSpl.AddNode(x, y);
            } else {
              Log.Warning("{0}.Load({1}) wrong Y value [{2}]{3}", model.path, _csv.value, i, dat[i]);
              continue;
            }
          } else {
            Log.Warning("{0}.Load({1}) wrong X value [{2}]{3}", model.path, _csv.value, i, dat[i]);
            continue;
          }
        }
        _cubSpl.BuildSpline();
      }
      _y.value=_cubSpl.Func(_x.value);
    }

    public void DeInit() {
    }
  }
}
