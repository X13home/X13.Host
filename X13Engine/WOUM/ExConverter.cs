#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace X13.WOUM {
  public static class ExConverter {

    static ExConverter() {
      _fn2t=new Dictionary<string, Type>();
      _fn2t.Add("long", typeof(long));
      _fn2t.Add("bool", typeof(bool));
      _fn2t.Add("string", typeof(string));
      _fn2t.Add("double", typeof(double));
      _fn2t.Add("DateTime", typeof(DateTime));
      _fn2t.Add("Topic", typeof(X13.Topic));
      _fn2t.Add("Statement", typeof(X13.PLC.PiStatement));
      _fn2t.Add("Wire", typeof(X13.PLC.PiWire));
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
    public static string Name2String2(string name) {
      if(string.IsNullOrEmpty(name)) {
        return string.Empty;
      }
      StringBuilder sb=new StringBuilder();
      int fl=0;
      int ch=0;
      for(int i=2; i<name.Length; i++) {
        if(fl>0) {
          int tmp;
          if(!int.TryParse(name.Substring(i, 1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out tmp)) {
            Log.Warning("Name2String2({0}) - bad symbol", name);
            return string.Empty;
          }
          ch=(ch<<4) | (tmp&0x0f);
          if(fl==4) {
            sb.Append((char)ch);
            fl=0;
          } else {
            fl++;
          }
        } else if(name[i]=='_') {
          fl=1;
          ch=0;
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
      sb.Append("L_");
      for(int i=0; i<str.Length; i++) {
        int k=(int)str[i];
        if(char.IsLetterOrDigit(str[i]) && k>=0x20 && k<=0x7F) {
          sb.Append(str[i]);
        } else {
          sb.AppendFormat("_{0:X4}", k);
        }
      }
      return sb.ToString();
    }
    #endregion String2Name

    private static Dictionary<string, Type> _fn2t;
    public static Type FullName2Type(string fullName) {
      Type ret=null;
      if(string.IsNullOrWhiteSpace(fullName) || _fn2t.TryGetValue(fullName, out ret)) {
        //Do nothing
      } else if((ret=Type.GetType(fullName))!=null) {
        _fn2t[fullName]=ret;
      } else {
        foreach(var a in AppDomain.CurrentDomain.GetAssemblies()) {
          if((ret=a.GetType(fullName))!=null) {
            _fn2t[fullName]=ret;
            break;
          }
        }
      }
      if(ret==null) {
        ret=typeof(JObject);
      }
      return ret;
    }
    public static string Type2Name(Type type) {
      if(type==null) {
        return string.Empty;
      }

      string rez=type.FullName;
      switch(rez) {
      case "System.Byte":
      case "System.Int16":
      case "System.Int32":
      case "System.SByte":
      case "System.UInt16":
      case "System.UInt32":
      case "System.Int64":
      case "System.UInt64":
        rez="long";
        break;
      case "System.Boolean":
        rez="bool";
        break;
      case "System.String":
        rez="string";
        break;
      case "System.Decimal":
      case "System.Double":
      case "System.Single":
        rez="double";
        break;
      case "System.DateTime":
        rez="DateTime";
        break;
      case "X13.Topic":
        rez="Topic";
        break;
      case "X13.PLC.PiStatement":
        rez="Statement";
        break;
      case "X13.PLC.PiWire":
        rez="Wire";
        break;
      }

      return rez;
    }
    public static Type Json2Type(string json) {
      if(!string.IsNullOrWhiteSpace(json)) {
        using(JsonTextReader reader = new JsonTextReader(new StringReader(json))) {
          if(reader.Read()) {

            switch(reader.TokenType) {
            case JsonToken.Boolean:
              return typeof(bool);
            case JsonToken.Float:
              return typeof(double);
            case JsonToken.Integer:
              return typeof(Int64);
            case JsonToken.Date:
              return typeof(DateTime);
            case JsonToken.String:
              return typeof(string);
            case JsonToken.StartObject: {
                while(reader.Read()) {
                  if(reader.Depth==1 && reader.TokenType==JsonToken.PropertyName && (string)reader.Value=="+") {
                    if(reader.Read() && reader.TokenType==JsonToken.String) {
                      return FullName2Type((string)reader.Value);
                    }
                  }
                }
              }
              break;
            }

          }
        }
      }
      return null;
    }
  }
}
