#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license


using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;

namespace X13.PLC.Astronomy
{
  [Export(typeof(IStatement))]
  [ExportMetadata("declarer", "Sun")]
  public class Sun : IStatement {
    private DVar<double> _dLat;
    private DVar<double> _dLon;
    private DVar<bool> _dOut;
    private DVar<DateTime> _dSunrise;
    private DVar<DateTime> _dSunset;

    private Timer _evnt;

    public void Load() {
      var t1=Topic.root.Get<string>("/etc/declarers/func/Sun");
      t1.value="/Astronomy;component/Images/fu_sun.png";
      t1.Get<string>("Lat").value="Ag";
      t1.Get<string>("Lon").value="Bg";
      t1.Get<string>("Sunrise").value="ao";
      t1.Get<string>("Out").value="bz";
      t1.Get<string>("Sunset").value="co";

      t1.Get<string>("_description").value="v Sunruse/sunset calculator";

      t1.Get<string>("rename").value="|R";
      t1.Get<string>("remove").value="}D";
    }

    public void Init(DVar<PiStatement> model) {
      _dLat=BiultInStatements.AddPin<double>(model, "Lat");
      _dLon=BiultInStatements.AddPin<double>(model, "Lon");
      _dSunrise=BiultInStatements.AddPin<DateTime>(model, "Sunrise");
      _dSunset=BiultInStatements.AddPin<DateTime>(model, "Sunset");
      _dOut=BiultInStatements.AddPin<bool>(model, "Out");
      _evnt=new Timer((o) => Calculate(null, null));
    }

    public void Calculate(DVar<PiStatement> model, Topic source) {
      var DR = Math.PI / 180;
      var RD = 1 / DR;
      var B5 = DR * _dLat.value;
      var L5 = _dLon.value;
      var Now = DateTime.Now;
      var H=TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalHours;
      var L0 = 4.8771 + .0172 * (Now.DayOfYear + .5 - L5 / 360);
      var C = .03342 * Math.Sin(L0 + 1.345);
      var C2 = RD * (Math.Atan(Math.Tan(L0 + C)) - Math.Atan(.9175 * Math.Tan(L0 + C)) - C);
      var SD = .3978 * Math.Sin(L0 + C);
      var CD = Math.Sqrt(1 - SD * SD);
      var SC = (SD * Math.Sin(B5) + .0145) / (Math.Cos(B5) * CD);

      if(Math.Abs(SC) <= 1) {
        // calculate sunrise 
        var C3 = RD * Math.Atan(SC / Math.Sqrt(1 - SC * SC));
        var R1 = 6 + H - ((L5%15) + C2 + C3) / 15;
        _dSunrise.value  = Now.Date.AddHours(R1);
        // calculate sunset
        var S1 = 18 + H - ((L5%15) + C2 - C3) / 15;
        _dSunset.value = Now.Date.AddHours(S1);
      } else {
        if(SC > 1) {
          // sun is up all day ...
          // Set Sunset to be in the future ...
          _dSunset.value = Now.Date.AddDays(1);
          // Set Sunrise to be in the past ...
          _dSunrise.value = Now.Date;
        }
        if(SC < -1) {
          // sun is down all day ...
          // Set Sunrise and Sunset to be in the future ...
          _dSunrise.value = Now.Date.AddDays(1);
          _dSunset.value = Now.AddDays(2);
        }
      }
      if(Now<_dSunrise.value) {
        _dOut.value=false;
        _evnt.Change(_dSunrise.value.AddSeconds(2)-Now, TimeSpan.FromDays(1));
      } else if(Now<_dSunset.value) {
        _dOut.value=true;
        _evnt.Change(_dSunset.value.AddSeconds(2)-Now, TimeSpan.FromDays(1));
      } else {
        _dOut.value=false;
        _evnt.Change(_dSunrise.value.AddHours(23)-Now, TimeSpan.FromDays(1));
      }
    }

    public void DeInit() {
      _evnt.Change(Timeout.Infinite, Timeout.Infinite);
    }

  }
}
