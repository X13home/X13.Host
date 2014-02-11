#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Threading;


namespace X13.Periphery {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 5)]
  [ExportMetadata("name", "Gpio")]
  public class Gpio : IPlugModul {
    private const long _version=301;
    private Dictionary<int, Pin> _pins;
    private Topic _gpio;
    private WOUM.BlockingQueue<Pin> _queue;

    public Gpio() {
      _pins=new Dictionary<int, Pin>();
    }

    public void Init() {
      if(!Engine.IsLinux) {
        Topic.root.Get<bool>("/local/cfg/Gpio.RasPi/enable").value=false;
        return;
      }
      Topic.root.Subscribe("/etc/Gpio/#", Dummy);
      Topic.root.Subscribe("/etc/declarers/dev/Gpio/#", Dummy);
    }

    public void Start() {
      var ver=Topic.root.Get<long>("/etc/Gpio/version");
      if(ver.value<_version) {
        ver.value=_version;
        Log.Info("Load Gpio declarers");
        var st=Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.Periphery.gpio.xst");
        if(st!=null) {
          using(var sr=new StreamReader(st)) {
            Topic.Import(sr, null);
          }
        }
      }
      _gpio=Topic.root.Get("/dev/Gpio");
      _gpio.Get<string>("_declarer", _gpio).value="Gpio";
      _queue=new WOUM.BlockingQueue<Pin>(Process, Pool);
      _queue.timeout=30;
      _gpio.Subscribe("+", GpioChanged);
    }

    public void Stop() {
      Topic.root.Unsubscribe("/etc/Gpio/#", Dummy);
      Topic.root.Unsubscribe("/etc/declarers/Gpio/#", Dummy);
      _gpio.Unsubscribe("+", GpioChanged);
      if(_queue!=null) {
        _queue.Dispose();
        Thread.Sleep(100);
        _queue=null;
      }
      foreach(var kv in _pins) {
        kv.Value.Dispose();
      }
    }
    private void Dummy(Topic t, TopicChanged a) {
    }
    private void GpioChanged(Topic t, TopicChanged a) {
      if(t==null || t.parent!=_gpio || a.Visited(_gpio, true)) {
        return;
      }
      DVar<bool> tb=t as DVar<bool>;
      if(tb!=null) {
        Pin pin=GetPin(t.name);
        if(pin==null) {
          t.Remove();
          return;
        }
        if(a.Art==TopicChanged.ChangeArt.Remove) {
          _pins.Remove(pin.idx);
          pin.Dispose();
        } else if(a.Art==TopicChanged.ChangeArt.Value && pin.dir) {
          pin.value=tb.value;
          _queue.Enqueue(pin);
        }
      }
    }
    private Pin GetPin(string name) {
      Pin rez=null;
      int idx=-1;
      if(name!=null && name.Length>2 && Int32.TryParse(name.Substring(2), out idx)) {  // Ip1, On19
        char fc=name[0];
        char sc=name[1];
        bool dir;
        bool negative;
        if(fc=='I') {
          dir=false;
        } else if(fc=='O') {
          dir=true;
        } else {
          Log.Error("unknown direktion in "+name+", allow 'I' & 'O'");
          return null;
        }
        if(sc=='p') {
          negative=false;
          //} else if(sc=='n') {
          //  negative=true;
        } else {
          Log.Error("unknown type in "+name+", allow 'p'"); //  & 'n'
          return null;
        }
        if(_pins.TryGetValue(idx, out rez)) {
          if(rez.dir==dir && rez.neg==negative) {
            return rez;
          } else {
            Log.Error("pin "+ rez.ToString() +" already used, "+ name);
            return null;
          }
        } else {
          rez=new Pin(idx, dir, negative);
          _queue.Enqueue(rez);
          _pins[idx]=rez;
        }
      }
      return rez;
    }
    private void Pool() {
      for(int i=0; i<_pins.Count && _queue!=null; i++) {
        _queue.Enqueue(_pins.Values.ElementAt(i));
      }
    }
    private void Process(Pin p) {
      try {
        if(p.Process()) {
          var t=_gpio.Get<bool>(p.ToString());
          t.saved=false;
          t.SetValue(p.value, new TopicChanged(TopicChanged.ChangeArt.Value, _gpio));
        }
      }
      catch(IOException) {
      }
      catch(Exception ex) {
        Log.Warning("gpio - "+ex.Message);
      }

    }
    private class Pin : IDisposable {
      private const string GPIO_PATH = "/sys/class/gpio/";

      public readonly int idx;
      private string _idxS;
      private bool _value;
      private bool _st;

      public Pin(int idx, bool dir, bool neg) {
        this.idx=idx;
        this._idxS=idx.ToString();
        this.dir=dir;
        this.neg=neg;
        this._st=false;
      }
      /// <summary>false- input, true - output</summary>
      public bool dir { get; private set; }
      public bool neg { get; private set; }
      public bool value {
        get { return _value; }
        set {
          _value=value;
        }
      }
      public bool Process() {
        try {
          if(!_st) {
            if(!Directory.Exists(GPIO_PATH + "gpio" + _idxS.ToString()))
              File.WriteAllText(GPIO_PATH + "export", _idxS);

            // set i/o direction
            File.WriteAllText(GPIO_PATH + "gpio" + _idxS + "/direction", dir?"out":"in");
            _st=true;
          }
          if(dir) {
            File.WriteAllText(GPIO_PATH + "gpio" + _idxS + "/value", _value ? "1" : "0");
          } else {
            string readValue = File.ReadAllText(GPIO_PATH + "gpio" + _idxS + "/value");
            bool tmp=(readValue.Length > 0 && readValue[0] == '1');
            if(tmp!=_value) {
              _value=tmp;
              return true;
            }
          }
        }
        catch(IOException) {
        }
        catch(Exception ex) {
          Log.Warning(this.ToString()+ "Process() - "+ex.Message);
        }
        return false;
      }
      public void Dispose() {
        File.WriteAllText(GPIO_PATH + "unexport", _idxS);
      }
      public override string ToString() {
        return string.Concat(dir?"O":"I", neg?"n":"p", idx.ToString("00"));
      }
    }
  }
}
