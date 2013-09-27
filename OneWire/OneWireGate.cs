#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using DalSemi.OneWire.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13.Periphery {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class OneWireGate : OneWireBase {
    private PortAdapter _adapter;
    private List<OneWireBase> _devs=new List<OneWireBase>();
    private bool _run;

    public OneWireGate()
      : base(null) {
    }
    public OneWireGate(PortAdapter adapter, byte[] rom)
      : base(null, rom, adapter.AdapterName) {
      this.adapter=adapter;
      base.gate=this;
    }

    internal void AddDevice(OneWireBase dev) {
      if(dev!=null && dev!=this) {
        _devs.Add(dev);
      }
    }
    internal void DelDevice(OneWireBase dev) {
      if(dev!=null) {
        _devs.Remove(dev);
      }
    }
    internal void Start() {
      _run=true;
      System.Threading.ThreadPool.QueueUserWorkItem(Pool);
    }
    private void Pool(object o) {
      try {
        while(_run) {
          OneWireBase dev=_devs.Where(z => z.prio>0).OrderByDescending(z => z.prio).FirstOrDefault();
          if(dev==null && _devs.Any(z=>z.GetFlag(Flags.NeedAlarm))) {
            _adapter.SetSearchOnlyAlarmingDevices();
            byte[] rom=new byte[8];
            if(_adapter.GetFirstDevice(rom, 0)) {
              do {
                dev=_devs.FirstOrDefault(z => z.rom.SequenceEqual(rom));
                if(dev!=null) {
                  dev.GetFlag(Flags.Alarm);
                }
              } while(_adapter.GetNextDevice(rom, 0));
            }
          }
          if(dev!=null) {
            try {
              dev.Proccess();
            }
            catch(Exception ex) {
              Log.Warning("{0}.Process - {1}", dev._owner.path, ex.Message);
            }
          } else {
            Thread.Sleep(15);
          }
        }
      }
      catch(Exception ex) {
        Log.Error("{0}.Pool() - {1}", _owner!=null?_owner.path:this.ToString(), ex);
      }
    }
    internal PortAdapter adapter {
      get {
        return _adapter;
      }
      set {
        _adapter=value;
        PSetOwner();
      }
    }
    protected override void PSetOwner() {
      if(_owner!=null) {
        if(_adapter!=null) {
          _owner.Get<string>("_via", _owner).value=string.Concat(Topic.root.Get<string>("/local/cfg/id"), "/", _adapter.PortName);
        }
      }
    }
  }
}
