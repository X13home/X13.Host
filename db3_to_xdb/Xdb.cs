#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace X13 {
  internal class Xdb {
    /// <summary>data stored in this record</summary>
    private const uint FL_SAVED_I  =0x01000000;
    /// <summary>data stored as a separate record</summary>
    private const uint FL_SAVED_E  =0x02000000;
    /// <summary>mask</summary>
    private const uint FL_SAVED_A  =0x07000000;
    private const uint FL_LOCAL    =0x08000000;
    private const uint FL_SAVED    =0x20000000;
    private const uint FL_RECORD   =0x40000000;
    private const uint FL_REMOVED  =0x80000000;
    private const int FL_REC_LEN   =0x00FFFFFF;
    private const int FL_DATA_LEN  =0x3FFFFFFF;
    private const int FL_LEN_MASK  =0x00FFFFF0;

    private static void CopyBytes(int value, byte[] buf, int offset) {
      buf[offset++]=(byte)value;
      buf[offset++]=(byte)(value>>8);
      buf[offset++]=(byte)(value>>16);
      buf[offset++]=(byte)(value>>24);
    }
    private static void CopyBytes(uint value, byte[] buf, int offset) {
      buf[offset++]=(byte)value;
      buf[offset++]=(byte)(value>>8);
      buf[offset++]=(byte)(value>>16);
      buf[offset++]=(byte)(value>>24);
    }
    private static void CopyBytes(ushort value, byte[] buf, int offset) {
      buf[offset++]=(byte)value;
      buf[offset++]=(byte)(value>>8);
    }
    private FileStream _file;
    private long _fileLength;
    private byte[] rBuf=new byte[64];
    private byte[] dBuf;

    public void Open() {
      _file=new FileStream("../data/persist.xdb", FileMode.Create, FileAccess.ReadWrite);
      _file.Write(new byte[0x40], 0, 0x40);
      _file.Flush(true);
      _fileLength=_file.Length;
    }
    public void Write(Topic t) {
      int data_size=0;
      uint fl_size=FL_RECORD | (uint)(14+Encoding.UTF8.GetByteCount(t.name));
      if(!string.IsNullOrEmpty(t.type)) {
        byte[] tBuf=Encoding.UTF8.GetBytes(t.type);
        byte[] pBuf=Encoding.UTF8.GetBytes(t.val);
        data_size=tBuf.Length+1+pBuf.Length;
        if(dBuf==null || dBuf.Length<((data_size+6+15)&FL_LEN_MASK)) {
          dBuf=new byte[(data_size+6+15)&FL_LEN_MASK];
        }
        Buffer.BlockCopy(tBuf, 0, dBuf, 4, tBuf.Length);
        dBuf[tBuf.Length+4]=0;
        Buffer.BlockCopy(pBuf, 0, dBuf, tBuf.Length+5, pBuf.Length);
        if(data_size>0 && (int)(fl_size & FL_REC_LEN)+data_size<64) {
          fl_size=(fl_size+(uint)data_size) | FL_SAVED_I;
        } else {
          fl_size|=FL_SAVED_E;
        }
      } else {
        data_size=0;
      }
      if(!string.IsNullOrEmpty(t.val)) {
        fl_size|=FL_SAVED;
      }

      if(rBuf==null || rBuf.Length<(int)((fl_size+15)&FL_LEN_MASK)) {
        rBuf=new byte[(int)((fl_size+15)&FL_LEN_MASK)];
      }
      if(data_size>0) {
        if((fl_size & FL_SAVED_A)==FL_SAVED_I) {
          CopyBytes(data_size, rBuf, 8);
          Buffer.BlockCopy(dBuf, 4, rBuf, (int)(fl_size & FL_REC_LEN)-data_size-2, data_size);
        } else {
          CopyBytes(6+data_size, dBuf, 0);
          int data_pos=0;
          Write(out data_pos, dBuf, data_size+6);
          CopyBytes(data_pos, rBuf, 8);
        }
      } else {
        CopyBytes(data_size, rBuf, 8);
      }

      CopyBytes(fl_size, rBuf, 0);
      if(t==Topic.root) {
        CopyBytes((int)0, rBuf, 4);
      } else {
        CopyBytes(t.parent.pos, rBuf, 4);
      }
      Encoding.UTF8.GetBytes(t.name).CopyTo(rBuf, 12);
      Write(out t.pos, rBuf, (int)(fl_size & FL_REC_LEN));
    }
    public void Close() {
      if(_file!=null) {
        _file.Close();
        _file=null;
      }
    }

    private void Write(out int pos, byte[] buf, int len) {
      int bufSize=((len+15)&FL_LEN_MASK);

      pos=(int)((_fileLength+15)>>4);
      _fileLength=((long)pos<<4)+bufSize;

      if(buf==null || len<6 || buf.Length<bufSize) {
        throw new ArgumentException("len");
      }
      CopyBytes(Crc16.ComputeChecksum(buf, len-2), buf, len-2);
      for(int i=len; i<bufSize; i++) {
        buf[i]=0;
      }
      _file.Position=(long)pos<<4;
      _file.Write(buf, 0, bufSize);
    }
  }
}
