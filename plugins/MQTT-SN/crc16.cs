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

namespace X13.Periphery {
  public static class Crc16 {
    private const ushort polynomial = 0xA001;
    private static ushort[] table = new ushort[256];


    public static ushort ComputeChecksum(byte[] bytes) {
      ushort crc = 0;
      for(int i = 0; i < bytes.Length; ++i) {
        byte index = (byte)(crc ^ bytes[i]);
        crc = (ushort)((crc >> 8) ^ table[index]);
      }
      return crc;
    }
    public static ushort UpdateChecksum(ushort crc, byte b) {
      byte index = (byte)(crc ^ b);
      crc = (ushort)((crc >> 8) ^ table[index]);
      return crc;
    }

    static Crc16() {
      ushort value;
      ushort temp;
      for(ushort i = 0; i < table.Length; ++i) {
        value = 0;
        temp = i;
        for(byte j = 0; j < 8; ++j) {
          if(((value ^ temp) & 0x0001) != 0) {
            value = (ushort)((value >> 1) ^ polynomial);
          } else {
            value >>= 1;
          }
          temp >>= 1;
        }
        table[i] = value;
      }
    }
  }
}
