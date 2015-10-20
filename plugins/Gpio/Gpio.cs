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
using System.Runtime.InteropServices;
using System.Threading;


namespace X13.Periphery {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 5)]
  [ExportMetadata("name", "Gpio")]
  public class Gpio : IPlugModul {
    private Dictionary<uint, Pin> _pins;
    private Topic _gpio;
    private Timer _poolTimer;
    public Gpio() {
      _pins=new Dictionary<uint, Pin>();
    }
    public void Init() {
      if(!Engine.IsLinux) {
        Topic.root.Get<bool>("/local/cfg/Gpio.RasPi/enable").value=false;
        return;
      }
      if(!Pin.bcm2835_init()) {
        Log.Warning("Unable to initialize bcm2835.so library");
        Topic.root.Get<bool>("/local/cfg/Gpio.RasPi/enable").value=false;
        return;
      }
      Topic.root.Subscribe("/etc/Gpio/#", Dummy);
      Topic.root.Subscribe("/etc/declarers/dev/Gpio/#", Dummy);
    }
    public void Start() {
      using(var sr=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.Periphery.gpio.xst"))) {
        Topic.Import(sr, null);
      }
      _gpio=Topic.root.Get("/dev/Gpio");
      _gpio.Get<string>("_declarer", _gpio).value="Gpio";
      _gpio.Subscribe("+", GpioChanged);
      _poolTimer=new Timer(Pool, null, 500, 50);
    }
    public void Stop() {
      Topic.root.Unsubscribe("/etc/Gpio/#", Dummy);
      Topic.root.Unsubscribe("/etc/declarers/Gpio/#", Dummy);
      _gpio.Unsubscribe("+", GpioChanged);
      if(_poolTimer!=null) {
        _poolTimer.Change(-1, -1);
        _poolTimer=null;
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
        }
      }
    }
    private Pin GetPin(string name) {
      Pin rez=null;
      uint idx=0;
      if(name!=null && name.Length>2 && UInt32.TryParse(name.Substring(2), out idx)) {  // Ip1, On19
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
        } else if(sc=='n') {
          negative=true;
        } else {
          Log.Error("unknown type in "+name+", allow 'p' & 'n'");
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
          _pins[idx]=rez;
        }
      }
      return rez;
    }
    private void Pool(object o) {
      for(int i=0; i<_pins.Count; i++) {
        try {
          var p=_pins.Values.ElementAt(i);
          if(!p.dir && p.Process()) {
            var t=_gpio.Get<bool>(p.ToString());
            t.saved=false;
            t.SetValue(p.value, new TopicChanged(TopicChanged.ChangeArt.Value, _gpio));
          }
        }
        catch(Exception ex) {
          Log.Warning("gpio - "+ex.Message);
        }
      }
    }
    // based on http://github.com/cypherkey/RaspberryPi.Net/
    private class Pin : IDisposable {
      public readonly uint idx;
      private string _idxS;
      private bool _value;
      public Pin(uint idx, bool dir, bool neg) {
        this.idx=idx;
        this._idxS=idx.ToString();
        this.dir=dir;
        this.neg=neg;
        bcm2835_gpio_fsel(idx, dir);
        if(!dir) {
          bcm2835_gpio_set_pud(idx, (uint)(neg?2:1)); //OFF = 0,  PULL_DOWN = 1,   PULL_UP = 2
        }
        Process();
      }
      /// <summary>false- input, true - output</summary>
      public bool dir { get; private set; }
      public bool neg { get; private set; }
      public bool value { get { return _value; } set { _value=value; Process(); } }
      public bool Process() {
        try {
          if(dir) {
            bcm2835_gpio_write(idx, neg?!_value:_value);
          } else {
            bool tmp=bcm2835_gpio_lev(idx);
            if(neg) {
              tmp=!tmp;
            }
            if(tmp!=_value) {
              _value=tmp;
              return true;
            }
          }
        }
        catch(Exception ex) {
          Log.Warning(this.ToString()+ "Process() - "+ex.Message);
        }
        return false;
      }
      public void Dispose() {
      }
      public override string ToString() {
        return string.Concat(dir?"O":"I", neg?"n":"p", idx.ToString("00"));
      }

      #region Imported functions
      [DllImport("libbcm2835.so", EntryPoint = "bcm2835_init")]
      public static extern bool bcm2835_init();

      [DllImport("libbcm2835.so", EntryPoint = "bcm2835_gpio_fsel")]
      static extern void bcm2835_gpio_fsel(uint pin, bool mode_out);

      [DllImport("libbcm2835.so", EntryPoint = "bcm2835_gpio_write")]
      static extern void bcm2835_gpio_write(uint pin, bool value);

      [DllImport("libbcm2835.so", EntryPoint = "bcm2835_gpio_lev")]
      static extern bool bcm2835_gpio_lev(uint pin);

      [DllImport("libbcm2835.so", EntryPoint = "bcm2835_gpio_set_pud")]
      static extern void bcm2835_gpio_set_pud(uint pin, uint pud);
      #endregion
    }
  }
}
