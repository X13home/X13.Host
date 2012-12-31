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
using System.Reflection;
using System.IO;

namespace X13.WOUM {
  public static class ExConverter {
    static ExConverter() {
      var tyC=new TypeComparer();
      _serializers=new SortedList<Type, Delegate>(32, tyC);
      _deserializers=new SortedList<Type, Delegate>(32, tyC);
    }

    #region String2Name
    private static string nsTable="123456789abcdefg_ !\"#$%&'()hijklmn_________________________opqr_s)*+,-./:;<=>?@[\\]^`{|}~__tuvwx";
    public static string Name2String(string name) {
      if(string.IsNullOrEmpty(name)) {
        return string.Empty;
      }
      StringBuilder sb=new StringBuilder();
      bool escCh=false;
      for(int i=7; i<name.Length; i++) {
        if(name[i]=='_' && !escCh) {
          escCh=true;
        } else if(escCh) {
          int k=(int)name[i];
          if(k>=0x20 && k<=0x7F) {
            sb.Append(nsTable[k-0x20]);
          }
          escCh=false;
        } else {
          sb.Append(name[i]);
        }
      }
      return sb.ToString();
    }
    public static string String2Name(string str) {
      if(string.IsNullOrEmpty(str)) {
        return string.Empty;
      }
      StringBuilder sb=new StringBuilder();
      sb.Append("Logram_");
      for(int i=0; i<str.Length; i++) {
        int k=(int)str[i];
        if(!char.IsLetterOrDigit(str[i]) && k>=0x20 && k<=0x7F) {
          sb.Append('_');
          sb.Append(nsTable[k-0x20]);
        } else {
          sb.Append(str[i]);
        }
      }
      return sb.ToString();
    }
    #endregion String2Name

    #region Serializer
    private static SortedList<Type, Delegate> _serializers;
    public static Action<T, Stream> Serializer<T>() {
      if(!_serializers.ContainsKey(typeof(T))) {
        MethodInfo mi=null;
        Type type=typeof(T);
        if(type.IsEnum) {
          type=Enum.GetUnderlyingType(typeof(T));
        }
        TypeCode tc=Type.GetTypeCode(type);
        switch(tc) {
        case TypeCode.DBNull:
        case TypeCode.Empty:
          return null;
        case TypeCode.Object:
          if(type==typeof(byte[])) {
            goto default;
          }
          return null;
        default:
          mi = typeof(ExConverter).GetMethod("ToStream", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.ExactBinding, null, new[] { type, typeof(Stream) }, null);
          _serializers[typeof(T)]=Delegate.CreateDelegate(typeof(Action<T, Stream>), mi);
          break;
        }
      }
      return (Action<T, Stream>)_serializers[typeof(T)];
    }
    private static void ToStream(string val, Stream ms) {
      if(!string.IsNullOrEmpty(val)) {
        byte[] buf=Encoding.UTF8.GetBytes(val);
        ms.Write(buf, 0, buf.Length);
      }

    }
    private static void ToStream(bool val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(char val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(sbyte val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(byte val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(short val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(ushort val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(int val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(uint val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(long val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(ulong val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(float val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(decimal val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(double val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    private static void ToStream(DateTime val, Stream ms) {
      (new BinaryWriter(ms)).Write(val.Ticks);
    }
    private static void ToStream(byte[] val, Stream ms) {
      (new BinaryWriter(ms)).Write(val);
    }
    #endregion Serializer

    #region Deserializer
    private static SortedList<Type, Delegate> _deserializers;
    public static Func<Stream, T> Deserializer<T>() {
      if(!_deserializers.ContainsKey(typeof(T))) {
        string mName=string.Empty;
        MethodInfo mi=null;
        Type type=typeof(T);
        if(type.IsEnum) {
          type=Enum.GetUnderlyingType(type);
        }
        TypeCode tc=Type.GetTypeCode(type);
        switch(tc) {
        case TypeCode.Empty:
        case TypeCode.DBNull:
          break;
        case TypeCode.Object:
          if(type==typeof(byte[])) {
            mi = typeof(ExConverter).GetMethod("ToByteArray", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.ExactBinding, null, new[] { typeof(Stream) }, null);
            _deserializers[typeof(T)]=(Func<Stream, T>)Delegate.CreateDelegate(typeof(Func<Stream, T>), mi);
            break;
          }
          return null;
        default: {
            mName="To"+tc.ToString();
            mi = typeof(ExConverter).GetMethod(mName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.ExactBinding, null, new[] { typeof(Stream) }, null);
            _deserializers[typeof(T)]=Delegate.CreateDelegate(typeof(Func<Stream, T>), mi);
          }
          break;
        }
      }
      return (Func<Stream, T>)_deserializers[typeof(T)];
    }

    private static string ToString(Stream ms) {
      byte[] buf=new byte[ms.Length-ms.Position];
      ms.Read(buf, 0, buf.Length);
      return Encoding.UTF8.GetString(buf);
    }
    private static bool ToBoolean(Stream ms) {
      return (new BinaryReader(ms)).ReadBoolean();
    }
    private static char ToChar(Stream ms) {
      return (new BinaryReader(ms)).ReadChar();
    }
    private static sbyte ToSByte(Stream ms) {
      return (new BinaryReader(ms)).ReadSByte();
    }
    private static byte ToByte(Stream ms) {
      return (new BinaryReader(ms)).ReadByte();
    }
    private static short ToInt16(Stream ms) {
      return (new BinaryReader(ms)).ReadInt16();
    }
    private static int ToInt32(Stream ms) {
      return (new BinaryReader(ms)).ReadInt32();
    }
    private static ushort ToUInt16(Stream ms) {
      return (new BinaryReader(ms)).ReadUInt16();
    }
    private static uint ToUInt32(Stream ms) {
      return (new BinaryReader(ms)).ReadUInt32();
    }
    private static long ToInt64(Stream ms) {
      return (new BinaryReader(ms)).ReadInt64();
    }
    private static ulong ToUInt64(Stream ms) {
      return (new BinaryReader(ms)).ReadUInt64();
    }
    private static float ToSingle(Stream ms) {
      return (new BinaryReader(ms)).ReadSingle();
    }
    private static double ToDouble(Stream ms) {
      return (new BinaryReader(ms)).ReadDouble();
    }
    private static decimal ToDecimal(Stream ms) {
      return (new BinaryReader(ms)).ReadDecimal();
    }
    private static DateTime ToDateTime(Stream ms) {
      return new DateTime((new BinaryReader(ms)).ReadInt64());
    }
    private static byte[] ToByteArray(Stream ms) {
      return (new BinaryReader(ms)).ReadBytes((int)(ms.Length-ms.Position));
    }
    #endregion Deserializer

    private class TypeComparer : IComparer<Type> {
      public int Compare(Type x, Type y) {
        if(x==null) {
          if(y==null) {
            return 0;
          } else {
            return 1;
          }
        } else {
          if(y==null) {
            return -1;
          } else {
            return x.Name.CompareTo(y.Name);
          }
        }
      }
    }
  }
}
