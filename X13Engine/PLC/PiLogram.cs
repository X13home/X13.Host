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
    private static string _id;
    private static bool _isDefault;
    static PiLogram() {
      _id=Topic.root.Get<string>("/local/cfg/id").value;
      var plcDefault=Topic.root.Get<string>("/etc/PLC/default");
      _isDefault=plcDefault.value==_id;
    }

    private DVar<string> _via;

    public PiLogram() {
    }

    public DVar<PiLogram> Owner { get; private set; }

    public bool exec { get { return _via!=null && _via.value==_id; } }

    private void _via_changed(Topic sender, TopicChanged arg) {
      if(arg.Art!=TopicChanged.ChangeArt.Value) {
        return;
      }
      if(_via!=null && string.IsNullOrEmpty(_via.value) && _isDefault) {
        _via.value=_id;
      }
      RefreshStatements();
    }

    private void RefreshStatements() {
      if(Owner==null){
        return;
      }
      foreach(var stD in Owner.children.Where(z => z.valueType==typeof(PiStatement)).Select(z => (z as DVar<PiStatement>).value).Where(z => z!=null)){
        stD.RefreshExec();
      }
    }

    #region ITopicOwned Members
    void ITopicOwned.SetOwner(Topic owner) {
      if(Owner!=owner) {
        if(Owner!=null) {
          if(_via!=null) {
            _via.changed+=_via_changed;
            _via=null;
            RefreshStatements();
          }
        }
        Owner=owner as DVar<PiLogram>;
        if(Owner!=null) {
          Owner.saved=true;
          var dc=Owner.Get<string>("_declarer");
          dc.saved=true;
          dc.value="Logram";
          _via=Owner.Get<string>("_via");
          _via.changed+=_via_changed;
          RefreshStatements();
        }
      }
    }
    #endregion ITopicOwned Members

    [Newtonsoft.Json.JsonProperty]
    internal string declarer { get { return Owner.Get<string>("_declarer").value; } set { } }
  }
}
