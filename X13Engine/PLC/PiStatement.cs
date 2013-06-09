#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace X13.PLC {

  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 4)]
  [ExportMetadata("name", "PLC")]
  public class PLCPlugin : IPlugModul {
    [ImportMany(typeof(IStatement))]
    private IEnumerable<Lazy<IStatement, IStData>> _statement;
    private DVar<string> _id;
    public void Init() {
      Topic.root.Subscribe("/etc/PLC/#", L_dummy);
      Topic.root.Subscribe("/etc/declarers/#", L_dummy);
      string path=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      _id=Topic.root.Get<string>("/local/cfg/id");

      #region Load statements
      var catalog = new AggregateCatalog();
      catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
      catalog.Catalogs.Add(new DirectoryCatalog(path));
      var _container = new CompositionContainer(catalog);
      try {
        _container.ComposeParts(this);
      }
      catch(CompositionException ex) {
        Log.Error("Load statements - {0}", ex.ToString());
        return;
      }
      #endregion Load statements

      foreach(var i in _statement) {
        PiStatement.AddStatemen(i.Metadata.declarer, i.Value.GetType());
        i.Value.Load();
      }
      _statement=null;
    }
    public void Start() {
      var defId=Topic.root.Get<string>("/etc/PLC/default");
      if(string.IsNullOrWhiteSpace(defId.value)) {
        defId.saved=true;
        defId.value=_id.value;
      }

      Topic.root.Subscribe("/plc/+/_via", via_changed);
    }

    private void via_changed(Topic sender, TopicChanged arg) {
      DVar<string> via;
      if(arg.Art!=TopicChanged.ChangeArt.Value || sender.name!="_via" || (via=sender as DVar<string>)==null || via.parent==null) {
        return;
      }
      if(string.Compare(via.value, _id.value)==0) {
        via.parent.Subscribe("#", L_dummy);
      } else {
        via.parent.Unsubscribe("#", L_dummy);
      }
      if(string.IsNullOrEmpty(via.value) && PiLogram._isDefault) {
        via.saved=true;
        via.value=_id.value;
      }
    }

    public static void L_dummy(Topic sender, TopicChanged arg) {
      return;
    }
    public void Stop() {
    }



    public interface IStData {
      string declarer { get; }
    }

  }

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
    private PiLogram _parent;
    private Timer _initDelay;

    [Newtonsoft.Json.JsonProperty]
    private string _declarer;

    private PiStatement() {
    }
    public PiStatement(string declarer) {
      this._declarer=declarer;
    }

    public void RefreshExec() {
      if(_owner==null) {
        return;
      }
      if(_parent==null || !_parent.exec) {
        if(_st!=null) {
          _st.DeInit();
          _st=null;
        }
      } else if(_st==null) {
        if(_statements.ContainsKey(_declarer)) {
          _owner.Get<string>("_declarer").saved=true;
          _owner.Get<string>("_declarer").value=_declarer;
          _st=(IStatement)Activator.CreateInstance(_statements[_declarer]);
          _owner.Subscribe("+", _owner_changed);
          _initDelay=new Timer(DoInit, null, 100, Timeout.Infinite);
        } else {
          Log.Warning("{0}[{1}] unknown on {2}", _owner.path, _declarer??string.Empty, Topic.root.Get<string>("/local/cfg/id").value);
          _owner.Remove();
        }
      }
    }
    private void DoInit(object o) {
      if(_initDelay!=null) {
        _initDelay.Change(Timeout.Infinite, Timeout.Infinite);
        _initDelay=null;
        if(_st!=null && _owner!=null) {
          _st.Init(_owner);
        }
      }
    }
    public void SetOwner(Topic owner) {
      if(owner!=_owner) {
        if(_owner!=null) {
          _owner.Unsubscribe("+", _owner_changed);
        }
        _parent=null;
        RefreshExec();

        _owner=owner as DVar<PiStatement>;
        if(_owner!=null) {
          if(_owner.parent!=null && _owner.parent.valueType==typeof(PiLogram)) {
            _parent=(_owner.parent as DVar<PiLogram>).value;
          }
          RefreshExec();
        }
      }
    }
    private void _owner_changed(Topic sender, TopicChanged param) {
      if(_initDelay!=null) {
        _initDelay.Change(100, Timeout.Infinite);
      } else if(param.Art==TopicChanged.ChangeArt.Value && _st!=null) {
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
    void Load();
    void Init(DVar<PiStatement> model);
    void Calculate(DVar<PiStatement> model, Topic source);
    void DeInit();
  }
}
