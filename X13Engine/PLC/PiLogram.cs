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
using X13.MQTT;

namespace X13.PLC {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class PiLogram : ITopicOwned {
    static PiLogram() {
      if(Topic.brokerMode) {
        var t1=Topic.root.Get<string>("/system/declarers/Logram");
        t1.value="/CC;component/Images/ty_logram.png";

        t1.Get<string>("open").value="1O";
        t1.Get<string>("rename").value="2R";
        t1.Get<string>("remove").value="3D";
      }
    }

    public PiLogram() {
    }

    public DVar<PiLogram> Owner { get; private set; }

    #region ITopicOwned Members
    void ITopicOwned.SetOwner(Topic owner) {
      if(Owner!=owner) {
        Owner=owner as DVar<PiLogram>;
        if(Owner!=null) {
          Owner.saved=true;
          var dc=Owner.Get<string>("_declarer");
          dc.saved=true;
          dc.value="Logram";
        }
      }
    }
    #endregion ITopicOwned Members

    [Newtonsoft.Json.JsonProperty]
    internal string declarer { get { return Owner.Get<string>("_declarer").value; } set { } }
  }
}
