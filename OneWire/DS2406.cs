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
  public class DS2406 : OneWireBase {
    private DVar<bool> _InA, _OnA, _InB, _OnB;
    private byte _st1;
    private DateTime _to;

    public DS2406()
      : base("DS2406") {
    }
    public DS2406(OneWireGate gate, byte[] rom)
      : base(gate, rom, "DS2406") {
    }
    internal override bool GetFlag(Flags fl) {
      switch(fl) {
      case Flags.Alarm:
        _prio+=50;
        return true;
      case Flags.DoRequest:
      case Flags.NeedAlarm:
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
      if((_OnA!=null && _OnA.value==((_st1&0x01)!=0)) || (_OnB!=null && _OnB.value==((_st1&0x02)!=0))) {
        _gate.adapter.SelectDevice(rom, 0);
        buf= new byte[] { 0x55, 0x07, 0x00, 0x00, 0xFF, 0xFF }; // write status command
        buf[3]=0x0B;    // Activity Latch = 1 , ch A
        if(_OnA!=null && !_OnA.value) {
          buf[3]|=0x20;
        }
        if((_st1&0x40)!=0) {  // channel A & B 
          buf[3]|=0x10;
          if(_OnB!=null && !_OnB.value) {
            buf[3]|=0x40;
          }
        }
        _gate.adapter.DataBlock(buf, 0, buf.Length);
        if(CRC16.Compute(buf, 0, buf.Length, 0)!=0xB001) {
          Log.Warning("{0} crc error out", _owner.path);
        }
      }
      _gate.adapter.SelectDevice(rom, 0);
      buf=new byte[] { 0xF5, 0x7D, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
      //if(clearActivity) {
      buf[1]|=0x80;
      //  clearActivity=false;
      //}
      _gate.adapter.DataBlock(buf, 0, buf.Length);
      if(CRC16.Compute(buf, 0, buf.Length, 0)==0xB001) {
        if(_st1!=buf[3]) {
          if((buf[3]&0x40)==0) {  // channel A only
            if(_InB!=null) {
              _InB.Remove();
              _InB=null;
            }
            if(_OnB!=null) {
              _OnB.Remove();
              _OnB=null;
            }
          } else {
            if(_InB==null) {
              _InB=_owner.Get<bool>("InB");
              _InB.saved=false;
            }
            _InB.value=((buf[3]&0x08)==0);
            if(_OnB==null) {
              _OnB=_owner.Get<bool>("OnB");
              _OnB.saved=false;
            }
            _OnB.value=((buf[3]&0x02)==0);
          }
          if(_InA==null) {
            _InA=_owner.Get<bool>("InA");
            _InA.saved=false;
          }
          _InA.value=((buf[3]&0x04)==0);
          if(_OnA==null) {
            _OnA=_owner.Get<bool>("OnA");
            _OnA.saved=false;
          }
          _OnA.value=((buf[3]&0x01)==0);
        }
        _st1=buf[3];
      } else {
        Log.Warning("{0} crc error in", _owner.path);
      }
      _to=DateTime.Now;
    }
  }
}
