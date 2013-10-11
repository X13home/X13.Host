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
    internal void Stop() {
      _run=false;
    }
    private void Pool(object o) {
      int connectCnt=10010;
      OneWireBase dev;
      byte[] rom=new byte[8];

      try {
        while(_run) {
          dev=null;
          if(connectCnt++>10000) {
            List<byte[]> ids=new List<byte[]>();
            connectCnt=0;
            _adapter.TargetAllFamilies();
            _adapter.SetSearchAllDevices();
            if(_adapter.GetFirstDevice(rom, 0)) {
              do {
                ids.Add(rom);
                rom = new byte[8];
              } while(_adapter.GetNextDevice(rom, 0));
              if(ids.Any()) {
                var _dev1w=Topic.root.Get("/dev/1Wire");
                foreach(var did in ids.Where(z => z!=null)) {
                  var td=_dev1w.children.FirstOrDefault(z => {
                    var d=z.GetValue() as OneWireBase;
                    return d!=null && d.rom.SequenceEqual(did);
                  });
                  if(td==this._owner) {
                    continue;
                  }
                  if(td==null) {
                    switch(did[0]) {
                    case 0x10:    // DS18S20
                      td=_dev1w.Get<DS18S20>(string.Format("DS18S20_{0:X2}{1:X2}{2:X2}{3:X2}", did[4], did[3], did[2], did[1]));
                      (td as DVar<DS18S20>).value=new DS18S20(this, did);
                      break;
                    case 0x12:    // DS2406
                      td=_dev1w.Get<DS2406>(string.Format("DS2406_{0:X2}{1:X2}{2:X2}{3:X2}", did[4], did[3], did[2], did[1]));
                      (td as DVar<DS2406>).value=new DS2406(this, did);
                      break;
                    case 0x26:    // DS2438
                      td=_dev1w.Get<DS2438>(string.Format("DS2438_{0:X2}{1:X2}{2:X2}{3:X2}", did[4], did[3], did[2], did[1]));
                      (td as DVar<DS2438>).value=new DS2438(this, did);
                      break;
                    case 0x28:    // DS18B20
                      td=_dev1w.Get<DS18B20>(string.Format("DS18B20_{0:X2}{1:X2}{2:X2}{3:X2}", did[4], did[3], did[2], did[1]));
                      (td as DVar<DS18B20>).value=new DS18B20(this, did);
                      break;
                    default:
                      Log.Warning("unknown device {0} on {1}:{2}", BitConverter.ToString(did), _adapter.AdapterName, _adapter.PortName);
                      break;
                    }
                  } else{
                    (td.GetValue() as OneWireBase).gate=this;
                  }
                  if(td!=null) {
                    dev=td.GetValue() as OneWireBase;
                    dev.present=true;
                  }
                }
              }
            }
          } else {
            dev=_devs.Where(z => z.present && z.prio>0).OrderByDescending(z => z.prio).FirstOrDefault();
            if(dev==null && _devs.Any(z => z.GetFlag(Flags.NeedAlarm))) {
              _adapter.SetSearchOnlyAlarmingDevices();
              if(_adapter.GetFirstDevice(rom, 0)) {
                do {
                  dev=_devs.FirstOrDefault(z => z.rom.SequenceEqual(rom));
                  if(dev!=null) {
                    dev.GetFlag(Flags.Alarm);
                    Log.Debug("{0} Alarm", dev._owner.name);
                  }
                } while(_adapter.GetNextDevice(rom, 0));
              }
            }
          }
          if(dev!=null) {
            try {
              dev.Proccess();
            }
            catch(AdapterException ex) {
              dev.ReportError();
              Log.Warning("{0}.Process - {1}", dev._owner.path, ex.Message);
            }
            catch(Exception ex) {
              Log.Warning("{0}.Process - {1}", dev._owner.path, ex.Message);
            }
          } else {
            connectCnt+=30;
            Thread.Sleep(15);
          }
        }
      }
      catch(Exception ex) {
        Log.Error("{0}.Pool() - {1}", _owner!=null?_owner.path:this.ToString(), ex);
      }
      if(_adapter!=null) {
        try {
          _adapter.EndExclusive();
          _adapter.Dispose();
        }
        catch(Exception) {
        }
        _adapter=null;
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
