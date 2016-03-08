using System;
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

    public static Holiday Find(DateTime dt, bool create=false) {
      Holiday ret=_list.Where(z=>z.begin<=dt && z.end>=dt && z.type!=HolidayType.none).OrderBy(z=>z.type==HolidayType.school?HolidayType.max:z.type).FirstOrDefault();
      if((ret == null || ret.type!=HolidayType.termin) && create) {
        ret = new Holiday(dt, string.Empty);
        _list.Add(ret);
      }
      return ret;
    }
    public static void Remove(Holiday h) {
      _list.Remove(h);
    }

    public enum HolidayType {
      none=0,
      legal=1,
      school=2,
      termin = 3,
      memo = 4,
      max=256,
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
    private Holiday(DateTime dt, string memo) {
      titel = memo;
      begin = dt;
      end = begin.AddSeconds(24 * 60 * 60 - 1);
      type = HolidayType.termin;
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
    public string ToString(bool fl) {
      StringBuilder sb = new StringBuilder();
      sb.AppendFormat("{0:dd.MM.yyyy}", this.begin);
      if(begin < end.Date) {
        sb.AppendFormat(" - {0:dd.MM.yyyy}", this.end);
      }
      return sb.ToString();
     
    }
    public readonly DateTime begin;
    public readonly DateTime end;
    public readonly HolidayType type;

    public string titel;
    public bool edited;
    public string path {
      get {
        if(end > begin.AddDays(1)) {
          return string.Format("/local/cfg/Holidays/{0:00}{1:00}{2:00}{3}{4:00}{5:00}{6:00}", begin.Year%100, begin.Month, begin.Day, (int)type, end.Year%100, end.Month, end.Day);
        }
        return string.Format("/local/cfg/Holidays/{0:00}{1:00}{2:00}{3}", begin.Year%100, begin.Month, begin.Day, (int)type);
      }
    }

  }
}
