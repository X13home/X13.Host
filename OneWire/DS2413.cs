#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using DalSemi.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13.Periphery {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class DS2413 : OneWireBase {
    private DVar<bool> _InA, _OnA, _InB, _OnB;
    private byte _st1;
    private DateTime _to;
    private bool _refreshOut;
    public DS2413()
      : base("DS2413") {
    }
    public DS2413(OneWireGate gate, byte[] rom)
      : base(gate, rom, "DS2413") {
    }
    internal override bool GetFlag(Flags fl) {
      switch(fl) {
      case Flags.DoRequest:
        return true;
      }
      return false;
    }
    internal override int prio {
      get {
        var ms=(DateTime.Now-_to).TotalMilliseconds;
        ms=ms>0?(ms<2000?ms/100:20):0;
        return _prio+(int)(ms);
      }
    }
    internal override void Proccess() {
      _prio=0;
      byte[] buf;
      if(_refreshOut || (_OnA!=null && _OnA.value==((_st1&0x02)!=0)) || (_OnB!=null && _OnB.value==((_st1&0x08)!=0))) {
        _gate.adapter.SelectDevice(rom, 0);
        buf = new byte[] { 0x5A, 0, 0, 0xFF, 0xFF };
        if(_OnA!=null && !_OnA.value) {
          buf[1]|=0x01;
        }
        if(_OnB!=null && !_OnB.value) {
          buf[1]|=0x02;
        }
        buf[2]=(byte)(~buf[1]);
        _gate.adapter.DataBlock(buf, 0, buf.Length);
        if(buf[3]!=0xAA) {
          ReportError();
          Log.Warning("{0} Failure to change latch state", _owner.path);
        }
        _refreshOut=false;
      }
      _gate.adapter.SelectDevice(rom, 0);
      buf=new byte[] { 0xF5, 0xFF };
      _gate.adapter.DataBlock(buf, 0, buf.Length);
      if((0x0F & ~(buf[1]>>4))!=(0x0F&buf[1])) {
        ReportError();
        Log.Warning("{0} Complement of b3 to b0 error, buf[1]={1:X2}", _owner.path, buf[1]);
      } else {
        if(_st1!=buf[1]) {
          if(((_st1 ^ buf[1]) & 0x0A)!=0) {
            _refreshOut=true;
          }
          if(_InB==null) {
            _InB=_owner.Get<bool>("InB");
            _InB.saved=false;
          }
          _InB.value=((buf[1]&0x04)==0);
          if(_OnB==null) {
            _OnB=_owner.Get<bool>("OnB");
            _OnB.saved=false;
          }
          _OnB.value=((buf[1]&0x08)==0);
          if(_InA==null) {
            _InA=_owner.Get<bool>("InA");
            _InA.saved=false;
          }
          _InA.value=((buf[1]&0x01)==0);
          if(_OnA==null) {
            _OnA=_owner.Get<bool>("OnA");
            _OnA.saved=false;
          }
          _OnA.value=((buf[1]&0x02)==0);
        }
        _st1=buf[1];
      }
      _to=DateTime.Now;
    }
  }
}
