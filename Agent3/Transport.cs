using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace X13.Agent3 {
  internal class Transport : IComparable<Transport> {
    private static List<Transport> _list;

    static Transport() {
      _list=new List<Transport>();
      Load(DateTime.Today);
      _list.Sort();
    }

    public static bool Update() {
      bool ret=false;
      DateTime now = DateTime.Now;
      if(_list.Count<7) {
        Load(DateTime.Today.AddDays(1).AddSeconds(1));
        _list.Sort();
        ret=true;
      }
      int i=0;
      while(i<7 && _list.Count>i) {
        if(_list[i].dt<now.AddMinutes((long)_list[i].route.value)) {
          _list.RemoveAt(i);
          ret=true;
        } else {
          i++;
        }
      }
      return ret;
    }
    public static Transport At(int i) {
      return _list.Count>i?_list[i]:null;
    }

    [Flags]
    private enum Dow {
      none=0,
      work=1,
      Fr=2,
      Sa=4,
      Su=8
    }

    private static void Load(DateTime today) {
      Dow mask;
      var h=Holiday.Find(today);
      if(h!=null && h.type==Holiday.HolidayType.legal) {
        mask=Dow.Su;
      } else {
        switch(today.DayOfWeek) {
        case DayOfWeek.Sunday:
          mask=Dow.Su;
          break;
        case DayOfWeek.Saturday:
          mask=Dow.Sa;
          break;
        case DayOfWeek.Friday:
          mask=Dow.Fr;
          break;
        default:
          mask=Dow.work;
          break;
        }
      }


      var trp=TopicSrc.Get("/local/cfg/Transport", true);
      foreach(var route in trp.children.Where(z => z.value!=null && z.value is long)) {
        DateTime now=DateTime.Now.AddMinutes((long)route.value);
        foreach(var i in route.children.Where(z => z.value!=null && z.value is long)) {
          try {
            Dow im=(Dow)int.Parse(i.name.Substring(4, 1), NumberStyles.AllowHexSpecifier);
            if((im & mask)!=mask) {
              continue;
            }
            DateTime tBegin=today.AddHours(int.Parse(i.name.Substring(0, 2))).AddMinutes(int.Parse(i.name.Substring(2, 2)));

            if(i.name.Length!=9 || (long)i.value==0) {
              if(tBegin>=now) {
                _list.Add(new Transport(route, tBegin));
              }
            } else {
              DateTime tEnd;
              tEnd=today.AddHours(int.Parse(i.name.Substring(5, 2))).AddMinutes(int.Parse(i.name.Substring(7, 2))).AddSeconds(59);
              if(tEnd<=now) {
                continue;
              }
              while(tBegin<=now) {
                tBegin=tBegin.AddMinutes((long)i.value);
              }
              while(tBegin<tEnd) {
                _list.Add(new Transport(route, tBegin));
                tBegin=tBegin.AddMinutes((long)i.value);
              }
            }
          }
          catch(Exception ex) {
            Log.Warning("Transport.Load {0} - {1}", i.path, ex.Message);
          }
        }
      }
    }

    private Transport(TopicSrc route, DateTime dt) {
      this.route=route;
      this.dt=dt;
    }
    public readonly TopicSrc route;
    public readonly DateTime dt;

    public int CompareTo(Transport other) {
      return dt.CompareTo(other.dt);
    }
  }
}
