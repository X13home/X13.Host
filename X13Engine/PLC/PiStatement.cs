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

namespace X13.PLC {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class PiStatement : ITopicOwned  {
    private static SortedList<string, Type> _statements;
    static PiStatement() {
      _statements=new SortedList<string, Type>();
    }

    public static void AddStatemen(string declarer, Type statement) {
      if(!statement.GetInterfaces().Any(z => z==typeof(IStatement))) {
        throw new ArgumentException("expected IStatement");
      }
      _statements[declarer]=statement;
    }

    private DVar<PiStatement> _owner;
    private IStatement _st;

    [Newtonsoft.Json.JsonProperty]
    private string _declarer;

    private PiStatement() {
    }
    public PiStatement(string declarer) {
      this._declarer=declarer;
    }

    public void SetOwner(Topic owner) {
      if(owner!=_owner) {
        if(_owner!=null && Topic.brokerMode) {
          _owner.Unsubscribe("+", _owner_changed);
          if(owner==null && _st!=null) {
            _st.DeInit();
            _st=null;
          }
        }
        _owner=owner as DVar<PiStatement>;
        if(_st==null && _owner!=null && Topic.brokerMode) {
          if(_statements.ContainsKey(_declarer)) {
            _owner.Get<string>("_declarer").saved=true;
            _owner.Get<string>("_declarer").value=_declarer;
            _st=(IStatement)Activator.CreateInstance(_statements[_declarer]);
            _st.Init(_owner);
            _owner.Subscribe("+", _owner_changed);
          } else {
            _owner.Remove();
            Log.Warning("{0} - unknown declarer - {1}", _owner.path, _declarer);
          }
        }
      }
    }
    private void _owner_changed(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Value && _st!=null) {
        try {
          _st.Calculate(_owner, param.Source);
        }
        catch(Exception ex) {
          Log.Warning("{0}.calculate - {1}", _owner.path, ex.Message);
        }
      }
    }
    public override string ToString() {
      return _declarer;
    }
  }
  public interface IStatement {
    void Init(DVar<PiStatement> model);
    void Calculate(DVar<PiStatement> model, Topic source);
    void DeInit();
  }
}
