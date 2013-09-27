#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13.Periphery {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class DS18B20 : OneWireBase {
    private DateTime _to;
    private int _st=0;
    public DS18B20()
      : base("DS18B20") {
    }
    public DS18B20(OneWireGate gate, byte[] rom)
      : base(gate, rom, "DS18B20") {
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
        return ms>0?((ms>0 && ms <30000)?(int)(1+ms/1000):31):0;
      }
    }
    internal override void Proccess() {
      var ms=(DateTime.Now-_to).TotalMilliseconds;
      if(ms>0) {
        if(_st==0) {
          _gate.adapter.SelectDevice(rom, 0);
          _gate.adapter.PutByte(0x44);
          _to=DateTime.Now.AddMilliseconds(750);
          _st=1;
        } else{
          if(ms<45000) {
            _gate.adapter.SelectDevice(rom, 0);
            byte[] buf=new byte[] { 0xBE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };  // READ SCRATCHPAD
            _gate.adapter.DataBlock(buf, 0, buf.Length);
            if(DalSemi.Utils.CRC8.Compute(buf, 1, 9)==0) {
              _owner.Get<double>("T").value=Math.Round((buf[2]*256+buf[1])/16.0, 1);
              if(buf[3]!=0x7F || buf[4]!=0x80 || buf[5]!=0x7F) {
                buf=new byte[] { 0x4E, 0x7F, 0x80, 0x7F };  // WRITE SCRATCHPAD, THi, TLo, cfg
                _gate.adapter.SelectDevice(rom, 0);
                _gate.adapter.DataBlock(buf, 0, buf.Length);
                buf=new byte[] { 0x48 };    // COPY SCRATCHPAD
                _gate.adapter.SelectDevice(rom, 0);
                _gate.adapter.DataBlock(buf, 0, buf.Length);
              }
            } else {
              Log.Warning("{0} bad crc", _owner.path);
            }
          }
          _to=DateTime.Now.AddMilliseconds(30000);
          _st=0;
        }
      }
    }
  }
}
