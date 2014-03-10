using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13{
  public static class Crc16 {
    private const ushort polynomial = 0xA001;
    private static ushort[] table = new ushort[256];


    public static ushort ComputeChecksum(byte[] buf, int len=0) {
      ushort crc = 0;
      if(len==0) {
        len=buf.Length;
      }
      for(int i = 0; i < len; ++i) {
        byte index = (byte)(crc ^ buf[i]);
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
