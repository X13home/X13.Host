﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.Periphery {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public abstract class OneWireBase : ITopicOwned {
    protected Topic _owner;
    private DVar<bool> _tPresent;
    private string _decl;
    protected OneWireGate _gate;

    protected OneWireBase(string declarer) {
      this._decl=declarer;
    }
    protected OneWireBase(OneWireGate gate, byte[] rom, string declarer) {
      this.gate=gate;
      this._decl=declarer;
      this.rom=rom;
      if(gate!=null) {
        gate.AddDevice(this);
      }
    }

    [Newtonsoft.Json.JsonProperty]
    public byte[] rom { get; private set; }
    internal OneWireGate gate {
      get {
        return _gate;
      }
      set {
        if(value!=null) {
          value.AddDevice(this);
        } else if(_gate!=null) {
          _gate.DelDevice(this);
        }
        _gate=value;
      }
    }
    internal bool present {
      get {
        return _tPresent!=null && _tPresent.value;
      }
      set {
        if(_tPresent!=null) {
          _tPresent.value=value;
        }
      }
    }
    internal virtual void Proccess() {
    }
    internal virtual bool GetFlag(Flags fl) {
      return false;
    }
    public void SetOwner(Topic owner) {
      if(!object.ReferenceEquals(owner, _owner)) {
        if(_owner!=null) {
          _owner.Unsubscribe("#", ChildChaged);
        }
        _owner=owner;
        if(_owner!=null) {
          _tPresent=_owner.Get<bool>("_present");
          _tPresent.saved=false;
          _owner.Get<string>("_declarer", _owner).value=_decl;
          PSetOwner();
          _owner.Subscribe("#", ChildChaged);
        } else if(_gate!=null) {
          _gate.DelDevice(this);
        }
      }
    }
    public override string ToString() {
      return BitConverter.ToString(rom);
    }

    protected virtual void PSetOwner() {
    }
    private void ChildChaged(Topic src, TopicChanged arg) {
      if(src==null) {
        return;
      }
    }

    internal enum Flags {
      NeedConvert,
      DoRequest,
      HasData,
    }
  }
}
