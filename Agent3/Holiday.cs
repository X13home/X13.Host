﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace X13.Agent3 {
  internal class Holiday {
    private static List<Holiday> _list;
    private static IFormatProvider ciEn=new CultureInfo("en-US");
    static Holiday() {
      _list=new List<Holiday>();
      var hh=TopicSrc.Get("/local/cfg/Holidays", true);
      foreach(TopicSrc h in hh.children.Where(z => (z.value as string)!=null)) {
        try {
          Holiday hd=new Holiday(h.name, h.value as string);
          _list.Add(hd);
        }
        catch(Exception ex) {
          Log.Warning("Holliday.ctor({0}, {1}) - {2}", h.name, h.value, ex.Message);
        }
      }
    }

    public static Holiday Find(DateTime dt) {
      Holiday ret=null;
      foreach(Holiday h in _list) {
        if(h.begin<=dt && h.end>=dt) {
          if(ret==null || ret.type==HolidayType.school || h.type==HolidayType.legal) {
            ret=h;
          }
        }
      }

      return ret;
    }

    [Flags]
    public enum HolidayType {
      none=0,
      legal=1,
      school=2,
      memo=4
    }

    private Holiday(string name, string value) {
      titel=value;
      begin=DateTime.ParseExact(string.Format("{0}-{1}-{2}", name.Substring(0, 2), name.Substring(2, 2), name.Substring(4, 2)), "yy-MM-dd", ciEn);
      type=(HolidayType)(int.Parse(name.Substring(6, 1)));
      if(name.Length==13) {
        end=DateTime.ParseExact(string.Format("{0}-{1}-{2}", name.Substring(7, 2), name.Substring(9, 2), name.Substring(11, 2)), "yy-MM-dd", ciEn).AddSeconds(24*60*60-1);
      } else {
        end=begin.AddSeconds(24*60*60-1);
      }
    }
    public override string ToString() {
      StringBuilder sb=new StringBuilder();
      sb.AppendFormat("{0:dd.MM.yyyy}", this.begin);
      if(begin<end.Date) {
        sb.AppendFormat(" - {0:dd.MM.yyyy}", this.end);
      }
      sb.AppendFormat(" {0}", this.titel);
      return sb.ToString();
    }
    public readonly DateTime begin;
    public readonly DateTime end;
    public readonly string titel;
    public readonly HolidayType type;
  }
}
