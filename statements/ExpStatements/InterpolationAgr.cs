#region license
//This file is part of the <see href="https://github.com/X13home">X13.Home</see> project.

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
  [ExportMetadata("declarer", "InterpolationA")]
  public class InterpolationAgr : IStatement {
    private DVar<double> _x;      //input
    private DVar<double> _yRef;   //input
    private DVar<bool> _match;
    private DVar<double> _y;      //output
    private DVar<double> _xe, _ye; //epsilon
    private DVar<string> _csv;
    private SortedList<double, double> _data;
    private CubicSpline _cubSpl;
    private int _upd;

    public void Load() {
      var t1=Topic.root.Get<string>("/etc/declarers/func/InterpolationA");
      t1.value="pack://application:,,/ExpStatements;component/Images/fu_intrA.png";
      t1.Get<string>("X").value="Ag";
      t1.Get<string>("YRef").value="Bg";
      t1.Get<string>("Match").value="Cz";
      t1.Get<string>("Y").value="ag";
      t1.Get<string>("_description").value="g4Agregated interpolation";

      t1.Get<string>("rename").value="|R";
      t1.Get<string>("remove").value="}D";
    }

    public void Init(DVar<PiStatement> model) {
      _data=new SortedList<double, double>();
      _cubSpl=new CubicSpline();
      _x=BiultInStatements.AddPin<double>(model, "X");
      _xe=BiultInStatements.AddPin<double>(model, "_xEpsilon");
      if(_xe.value==0) {
        _xe.value=1;
      }
      _yRef=BiultInStatements.AddPin<double>(model, "YRef");
      _match=BiultInStatements.AddPin<bool>(model, "Match");
      _ye=BiultInStatements.AddPin<double>(model, "_yEpsilon");
      if(_ye.value==0) {
        _ye.value=1;
      }
      _y=BiultInStatements.AddPin<double>(model, "Y");
      _csv=BiultInStatements.AddPin<string>(model, "_csv");
      _upd=0;
    }

    public void Calculate(DVar<PiStatement> model, Topic source) {
      if(source==_csv) {
        Import();
        Rebuild();
      } else if((source==_yRef || source==_match) && _match.value) {
        double y2=_cubSpl.Func(_x.value);
        double yo=_yRef.value-y2;
        if(double.IsNaN(y2) || Math.Abs(yo)>_ye.value) {
          double xo=Math.Round(_x.value/_xe.value)*_xe.value;
          double y1;
          if(_data.TryGetValue(xo, out y1) && !double.IsNaN(y2)) {
              y1+=yo/10;
          } else {
            y1=_yRef.value;
          }
          if(!double.IsNaN(y1)) {
            _data[xo]=y1;
          }
          //Log.Debug("{2}({0}, {1})", xo, y1, model.path);
          _upd++;
          Rebuild();
          _y.value=_cubSpl.Func(_x.value);
        }
      } else if(source==_x) {
        _y.value=_cubSpl.Func(_x.value);
        if(_upd>2) {
          Export();
          _upd=0;
        }
      }
    }

    public void DeInit() {
      Export();
    }
    private void Import() {
      string[] lines=null;
      if(_csv.value==null) {
        return;
      }
      if(File.Exists(_csv.value)) {
        try {
          lines=File.ReadAllLines(_csv.value);
        }
        catch(Exception ex) {
          Log.Warning("{0}.Import({1}) - {2}", _csv.parent.path, _csv.value, ex.Message);
        }
      } else {
        Log.Warning("{0}.Import({1}) not exist", _csv.parent.path, _csv.value);
      }
      if(lines==null || lines.Length==0) {
        return;
      }
      double x, y;
      string[] lt;
      for(int i=0; i<lines.Length; i++) {
        lt=lines[i].Split(';');
        if(lt==null || lt.Length!=2) {
          continue;
        }
        if(double.TryParse(lt[0], out x)) {
          if(double.TryParse(lt[1], out y)) {
            _data[x]=y;
          } else {
            Log.Warning("{0}.Import({1}) wrong Y value [{2}]{3}", _csv.parent.path, _csv.value, i, lines[i]);
            continue;
          }
        } else {
          Log.Warning("{0}.Import({1}) wrong X value [{2}]{3}", _csv.parent.path, _csv.value, i, lines[i]);
          continue;
        }
      }
    }
    private void Rebuild() {
      _cubSpl.Reset();
      if(_data.Count<3) {
        return;
      }
      foreach(var kv in _data) {
        _cubSpl.AddNode(kv.Key, kv.Value);
      }
      _cubSpl.BuildSpline();
    }
    private void Export() {
      if(_csv.value==null) {
        return;
      }
      try {
        using(var f=File.CreateText(_csv.value)) {
          foreach(var kv in _data) {
            f.WriteLine(kv.Key.ToString()+";"+kv.Value.ToString());
          }
        }
      }
      catch(Exception ex) {
        Log.Warning("{0}.Export({1}) - {2}", _csv.parent.path, _csv.value, ex.Message);
      }
    }
  }
}
