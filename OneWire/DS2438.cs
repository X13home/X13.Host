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
  public class DS2438 : OneWireBase {
    private DateTime _to;
    private int _st=-1;

    public DS2438()
      : base("DS2438") {
    }
    public DS2438(OneWireGate gate, byte[] rom)
      : base(gate, rom, "DS2438") {
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
        ms=ms>0?(ms<2000?ms/10:20):0;
        return _prio+(int)(ms);
      }
    }
    private const int POOL_CNT=49;
    internal override void Proccess() {
      if(_owner==null) {
        return;
      }
      byte[] buf;
      if(_st<0) {
        buf=ReadPage(0);
        if(buf==null) {
          return;
        }
        _gate.adapter.Reset();
        buf[0]=(byte)((buf[0] & 0xF0) | 0x01);
        WritePage(0, buf);
        _gate.adapter.SelectDevice(rom, 0);
        _gate.adapter.DataBlock(new byte[] { 0x48, 0 }, 0, 2);  // Copy Scratchpad
        _st=0;
      }
      if(_st>POOL_CNT) {
        _st=0;
      }
      buf=ReadPage(0);
      if(buf==null) {
        return;
      }
      if((buf[0] & 0x01)!=0) {
        var dV2=_owner.Get<double>("VSD");
        dV2.saved=false;
        dV2.value=Math.Round(((short)((buf[6]<<8) | buf[5]))/4096.0, 4);
      }
      if((_st & 1)!=0) {
        if((buf[0] & 0x40)==0) {   // A/D conversion complete
          DVar<double> dV;
          if(_st==1) {
            dV=_owner.Get<double>("VDD");
          } else {
            dV=_owner.Get<double>("VAD");
          }
          dV.saved=false;
          dV.value=((ushort)((buf[4]<<8) | buf[3]))/100.0;
          _st++;
        }
        if((buf[0] & 0x10)==0) { // = temperature conversion complete
          var dT=_owner.Get<double>("T");
          dT.saved=false;
          dT.value=Math.Round(((short)((buf[2]<<8) | buf[1]))/256.0, 2);
        }
      }
      if((_st & 1)==0) {
        if(_st==0) {
          buf[0]=(byte)((buf[0] & 0xF0) | 0x09);
          WritePage(0, buf);
          _gate.adapter.SelectDevice(rom, 0);
          _gate.adapter.PutByte(0x44);
        }
        _gate.adapter.SelectDevice(rom, 0);
        _gate.adapter.PutByte(0xB4);
        _st++;
      }
      _prio=0;
      _to=DateTime.Now.AddMilliseconds(1200);
    }
    /// <summary>Reads the specified 8 byte page and returns the data in an array.</summary>
    /// <param name="page">The page number to read</param>
    /// <returns>eight byte array that make up the page</returns>
    private byte[] ReadPage(int page) {
      byte[] buffer = new byte[11]; // Holds 2 command bytes, 8 data bytes and 1 CRC byte
      byte[] result = null;
      uint crc8;   // this device uses a crc 8
      // Perform the read scratchpad by using a combined write and read buffer
      _gate.adapter.SelectDevice(rom, 0);
      _gate.adapter.DataBlock(new byte[] { 0xB8, 0 }, 0, 2);  // Recall Memory

      _gate.adapter.SelectDevice(rom, 0);

      buffer[0] = 0xBE;    // READ_SCRATCHPAD_COMMAND
      buffer[1] = (byte)page;

      for(int i = 2; i < 11; i++)
        buffer[i] = 0xff;

      _gate.adapter.DataBlock(buffer, 0, 11);

      // CRC check. By including the CRC byte (the last byte) in the calculation
      // the calculated CRC will be 0 if it is valid
      crc8 = CRC8.Compute(buffer, 2, 9);

      if(crc8 != 0x0) {
        ReportError();
        Log.Warning("{0} bad crc", _owner.path);
      } else {
        // copy the data into the result
        result = new byte[8];
        Array.Copy(buffer, 2, result, 0, 8);
      }

      return result;
    }
    /// <summary> Writes a page of memory to this device</summary>
    /// <param name="page">The page number</param>
    /// <param name="source">Data to be written to the page</param>
    public void WritePage(int page, byte[] source) {
      byte[] buffer = new byte[10];

      _gate.adapter.SelectDevice(rom, 0);

      // write the page to the scratchpad first
      buffer[0] = 0x4E;   // WRITE_SCRATCHPAD_COMMAND
      buffer[1] = (byte)page;

      Array.Copy(source, 0, buffer, 2, 8);
      _gate.adapter.DataBlock(buffer, 0, 10);
    }
  }
}
