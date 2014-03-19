﻿#region license
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
using Newtonsoft.Json.Linq;

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
      if(string.IsNullOrWhiteSpace(json)) {
        return null;
      }
      if(json[0]=='"') {
        return typeof(string);
      }
      if(json[0]=='{') {
        JObject o=JObject.Parse(json);
        JToken jDesc;
        if(o.TryGetValue("+", out jDesc)) {
          return FullName2Type(jDesc.ToObject<string>());
        }
        return null;
      }
      if(json=="true" || json=="false") {
        return typeof(bool);
      }
      if(json.StartsWith("new Date(")) {
        return typeof(DateTime);
      }
      int dotCnt=0;
      for(int i=json.Length-1; i>=0; i--) {
        if(char.IsDigit(json, i)) {
          continue;
        }
        if(json[i]=='.') {
          dotCnt++;
          if(dotCnt>1) {
            return null;
          }
          continue;
        }
        if(i==0 && (json[0]=='-' || json[0]=='+')) {
          continue;
        }
        return null;  // is not number
      }
      return dotCnt==0?typeof(Int64):typeof(double);
    }

  }
}
