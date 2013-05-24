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
using System.Reflection;
using System.IO;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Threading;

namespace X13 {
  public interface IPlugModul {
    void Init();
    void Start();
    void Stop();
  }
  public interface IPlugModulData {
    int priority { get; }
    string name { get; }
  }

  public class Plugins {
    private Timer _1SecTimer;
    private DVar<DateTime> _now;
    private DVar<long> _nowOffset;

#pragma warning disable 649
    [ImportMany(typeof(IPlugModul), RequiredCreationPolicy=CreationPolicy.Shared)]
    private IEnumerable<Lazy<IPlugModul, IPlugModulData>> _modules;
#pragma warning restore 649

    public void Init(bool defState) {
      string path=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      string exeName=Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
      var root=Topic.root;
      Log.Info("Starting {0}", exeName);

      var myId=Topic.root.Get<string>("/local/cfg/id");
      if(string.IsNullOrWhiteSpace(myId.value)) {
        myId.saved=true;
        if(defState) {
          myId.value=string.Format("{0}", Environment.MachineName);
        } else {
          myId.value=string.Format("{0}_{2}@{1}", Environment.UserName, Environment.MachineName, exeName);
        }
      }

      var catalog = new AggregateCatalog();
      catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
      catalog.Catalogs.Add(new DirectoryCatalog(path));
      CompositionContainer _container = new CompositionContainer(catalog);
      try {
        _container.ComposeParts(this);
      }
      catch(CompositionException ex) {
        Log.Error("Load plugins - {0}", ex.ToString());
        throw;
      }
      {
        _now=Topic.root.Get<DateTime>("/var/now");
        _nowOffset=Topic.root.Get<long>("/local/cfg/Client/TimeOffset");
        DateTime nowDT=DateTime.Now;
        _now.value=nowDT;
        _now.Get<long>("second").value=nowDT.Second;
        _now.Get<long>("minute").value=nowDT.Minute;
        _now.Get<long>("hour").value=nowDT.Hour;
        _now.Get<long>("wDay").value=(long)nowDT.DayOfWeek;
        _now.Get<long>("day").value=nowDT.Day;
        _now.Get<long>("month").value=nowDT.Month;
        _now.Get<long>("year").value=nowDT.Year;
        //_1SecTimer.Change(Timeout.Infinite, Timeout.Infinite);  // !!!!!!!!!!!!!!!!!!!!!!
      }

      foreach(var i in _modules.OrderBy(z => z.Metadata.priority)) {
        try {
          if(!string.IsNullOrWhiteSpace(i.Metadata.name)) {
            string plPath="/local/cfg/"+i.Metadata.name+"/enable";
            Topic enT;
            DVar<bool> enD;
            if(Topic.root.Exist(plPath, out enT)) {
              enD=enT as DVar<bool>;
              if(enD!=null && !enD.value) {
                continue;                     // plugin disabled
              }
            } else {
              enD=Topic.root.Get<bool>(plPath);
              enD.saved=true;
              enD.value=defState;
              if(!enD.value) {
                continue;
              }
            }
          }
          i.Value.Init();
          Log.Debug("plugin {0} Loaded", i.Metadata.name??i.Value.GetType().FullName);
        }
        catch(Exception ex) {
          Log.Error("Load plugin {0} failure - {1}", i.Metadata.name??i.Value.GetType().FullName, ex.ToString());
        }
      }

    }
    public void Start() {
      foreach(var i in _modules.OrderBy(z => z.Metadata.priority)) {
        try {
          if(!string.IsNullOrWhiteSpace(i.Metadata.name)) {
            DVar<bool> enD=Topic.root.Get<bool>("/local/cfg/"+i.Metadata.name+"/enable");
            if(enD.value) {
              i.Value.Start();
              Log.Debug("plugin {0} Started", i.Metadata.name??i.Value.GetType().FullName);
            }
          }
        }
        catch(Exception ex) {
          Log.Error("Start plugin {0} failure - {1}", i.Metadata.name??i.Value.GetType().FullName, ex.ToString());
        }
      }
      _1SecTimer=new Timer(new TimerCallback(Tick1Sec), null, 2050-DateTime.Now.Millisecond, 1000);
    }
    public void Stop() {
      string exeName=Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
      Log.Info("Shutdown {0}", exeName);
      _1SecTimer.Change(Timeout.Infinite, Timeout.Infinite);

      foreach(var i in _modules.OrderByDescending(z => z.Metadata.priority)) {
        try {
          string plPath="/local/cfg/"+i.Metadata.name+"/enable";
          if(Topic.root.Get<bool>(plPath).value) {
            i.Value.Stop();
          }
        }
        catch(Exception ex) {
          Log.Error("Stop plugin {0} failure - {1}", i.Metadata.name??i.Value.GetType().FullName, ex.ToString());
        }
      }
    }
    public IPlugModul this[string name] {
      get {
        var it=_modules.FirstOrDefault(z => z.Metadata.name==name);
        if(it==null) {
          return null;
        }
        return it.Value;
      }
    }
    private void Tick1Sec(object o) {
      DateTime nowDT=DateTime.Now.AddTicks(_nowOffset.value);
      var mq=(nowDT.Second!=0?Topic.root.Get("/local/MQ"):_now);
      _1SecTimer.Change(1050-nowDT.Millisecond, 1000);
      _now.SetValue(nowDT, new TopicChanged(TopicChanged.ChangeArt.Value, mq));
      _now.Get<long>("second").SetValue(nowDT.Second, new TopicChanged(TopicChanged.ChangeArt.Value, mq));
      if(nowDT.Second==0) {
        _now.Get<long>("minute").SetValue(nowDT.Minute, new TopicChanged(TopicChanged.ChangeArt.Value, _now));
        if(nowDT.Minute==0) {
          _now.Get<long>("hour").SetValue(nowDT.Hour, new TopicChanged(TopicChanged.ChangeArt.Value, _now));
          if(nowDT.Hour==0) {
            _now.Get<long>("wDay").SetValue((long)nowDT.DayOfWeek, new TopicChanged(TopicChanged.ChangeArt.Value, _now));
            _now.Get<long>("day").SetValue(nowDT.Day, new TopicChanged(TopicChanged.ChangeArt.Value, _now));
            if(nowDT.Day==1) {
              _now.Get<long>("month").SetValue(nowDT.Month, new TopicChanged(TopicChanged.ChangeArt.Value, _now));
              if(nowDT.Month==1) {
                _now.Get<long>("year").SetValue(nowDT.Year, new TopicChanged(TopicChanged.ChangeArt.Value, _now));
              }
            }
          }
        }
      }
    }
  }
}
