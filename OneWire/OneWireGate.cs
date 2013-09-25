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

    internal override void Proccess() {
      foreach(var dev in _devs) {
        if(dev.GetFlag(Flags.NeedConvert)) {
          dev.Proccess();
        }
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
    public override string ToString() {
      return base.ToString();
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
