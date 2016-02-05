#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace X13.PLC {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  [TypeConverter(typeof(ByteArrayConverter))]
  public class ByteArray { // : IConvertible 
    [Newtonsoft.Json.JsonProperty]
    private byte[] _val;
    [Newtonsoft.Json.JsonProperty]
    private string _titel;

    public ByteArray() {
      _val=new byte[0];
    }
    public ByteArray(byte[] data) {
      _val=data;
    }
    public ByteArray(ByteArray src, byte[] data, int pos) {
      if(data==null) {
        return;
      }
      if(src==null) {
        if(pos<0) {
          pos=0;
        }
        _val=new byte[pos+data.Length];
        Buffer.BlockCopy(data, 0, _val, pos, data.Length);
      } else {
        if(pos<0) {  // negative => position from end
          pos=src._val.Length+1+pos;
        }
        if(pos>=src._val.Length) {
          _val=new byte[pos+data.Length];
          Buffer.BlockCopy(src._val, 0, _val, 0, src._val.Length);
          Buffer.BlockCopy(data, 0, _val, pos, data.Length);
        } else if(pos==0) {
          _val=new byte[src._val.Length+data.Length];
          Buffer.BlockCopy(data, 0, _val, 0, data.Length);
          Buffer.BlockCopy(src._val, 0, _val, data.Length, src._val.Length);
        } else {
          _val=new byte[src._val.Length+data.Length];
          Buffer.BlockCopy(src._val, 0, _val, 0, pos);
          Buffer.BlockCopy(data, 0, _val, pos, data.Length);
          Buffer.BlockCopy(src._val, pos, _val, pos+data.Length, src._val.Length-pos);
        }
      }
    }
    public byte[] GetBytes() {
      return _val;
    }
    public string Titel { get { return _titel; } set { _titel = value; } }
    public override string ToString() {
      return BitConverter.ToString(_val);
    }
    /*
    public TypeCode GetTypeCode() {
      return TypeCode.Object;
    }

    public bool ToBoolean(IFormatProvider provider) {
      return false;
    }

    public byte ToByte(IFormatProvider provider) {
      return 0;
    }

    public char ToChar(IFormatProvider provider) {
      return (char)0;
    }

    public DateTime ToDateTime(IFormatProvider provider) {
      return DateTime.MinValue;
    }

    public decimal ToDecimal(IFormatProvider provider) {
      return 0m;
    }

    public double ToDouble(IFormatProvider provider) {
      return 0.0;
    }

    public short ToInt16(IFormatProvider provider) {
      return 0;
    }

    public int ToInt32(IFormatProvider provider) {
      return 0;
    }

    public long ToInt64(IFormatProvider provider) {
      return 0;
    }

    public sbyte ToSByte(IFormatProvider provider) {
      return 0;
    }

    public float ToSingle(IFormatProvider provider) {
      return 0;
    }

    public string ToString(IFormatProvider provider) {
      return Encoding.Default.GetString(_val);
    }

    public object ToType(Type conversionType, IFormatProvider provider) {
      if(conversionType==typeof(string)) {
        return this.ToString(provider);
      } else if(conversionType==typeof(byte[])){
        return _val; 
      } else {
        return null;
      }
    }

    public ushort ToUInt16(IFormatProvider provider) {
      return 0;
    }

    public uint ToUInt32(IFormatProvider provider) {
      return 0;
    }

    public ulong ToUInt64(IFormatProvider provider) {
      return 0;
    }*/

  }
  public class ByteArrayConverter : TypeConverter {
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) {
      if(sourceType == typeof(string)) {
        return true;
      }
      return base.CanConvertFrom(context, sourceType);
    }
    // Overrides the ConvertFrom method of TypeConverter.
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
      if(value is string) {
        string[] v = ((string)value).Split(new char[] { ',', ':', '-' });
        List<byte> rez=new List<byte>();
        byte tmp;
        for(int i=0; i<v.Length; i++) {
          if(byte.TryParse(v[i], NumberStyles.HexNumber, culture, out tmp)) {
            rez.Add(tmp);
          }
        }
        return new ByteArray(rez.ToArray());
      }
      return base.ConvertFrom(context, culture, value);
    }
  }
}
