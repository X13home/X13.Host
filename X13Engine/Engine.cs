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
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Threading;
using System.Reflection;
using X13.WOUM;
using System.IO;
using X13.MQTT;

namespace X13 {
  public class Engine {
    static void Main(string[] args) {
      var eng=new Engine();
      eng.StartUp();
      Console.WriteLine("Engine running; press Enter to Exit");
      Console.Read();
      eng.Shutdown();
    }
#pragma warning disable 649
    [ImportMany(typeof(IPlugModul))]
    private IEnumerable<Lazy<IPlugModul, IPlugModulData>> _modules;
#pragma warning restore 649

    private static BlockingQueue<LogEntry> _log;
    private string _lfPath;
    private DateTime _firstDT;
    private DVar<LogLevel> _lThreshold;
    private DVar<long> _lHead;
    private DVar<bool> _debug;
    private Timer _1SecTimer;
    private DVar<DateTime> _now;
    private DVar<long> _nowOffset;

    public void StartUp() {
      string path=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      Directory.SetCurrentDirectory(path);

      _log=new BlockingQueue<LogEntry>(ProcessLog);
      if(!Directory.Exists("../log")) {
        Directory.CreateDirectory("../log");
      }
      if(!Directory.Exists("../data")) {
        Directory.CreateDirectory("../data");
      }
      AppDomain.CurrentDomain.UnhandledException+=new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
      Log.Write+=new Action<LogLevel, DateTime, string>(Log_Write);
      Topic.brokerMode=true;
      var root=Topic.root;
      _lHead=root.Get<long>("/var/log");
      _lThreshold=root.Get<LogLevel>("/etc/log/threshold");
      Topic.paused=true;
      Log.Info("Starting");
      Topic.Import("../data/engine.xst", "/local/cfg");
      _debug=Topic.root.Get<bool>("/local/cfg/repository/_verbose");
      if(_debug.value) {
        root.Subscribe("/#", MQTT_Main_changed);
      }
      var myId=Topic.root.Get<string>("/local/cfg/id");
      if(string.IsNullOrWhiteSpace(myId.value)) {
        myId.saved=true;
        myId.value=Environment.MachineName;
      }

      #region Load plugins
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
      #endregion Load plugins

      foreach(var i in _modules.Where(z => z.Metadata.priority<16).OrderBy(z => z.Metadata.priority)) {
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
            enD.value=true;
          }
        }
        i.Value.Start();
        Log.Debug("plugin {0} loaded", i.Metadata.name??i.Value.GetType().FullName);
      }

      string dbVersion=Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
      var dbVer=Topic.root.Get<string>("/etc/repository/version");
      if(dbVer.value==null || string.Compare(dbVer.value, dbVersion)<0) {
        dbVer.saved=true;
        dbVer.value=dbVersion;
        _lHead.saved=true;
        _lThreshold.saved=true;
#if DEBUG
        _lThreshold.value=LogLevel.Debug;
#else
        _lThreshold.value=LogLevel.Info;
#endif
        var devDec=Topic.root.Get<string>("/dev/_declarer");
        devDec.saved=true;
        devDec.value="DevFolder";

        Log.Info("Load default declarers");
        var st=Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.PLC.types.xst");
        if(st!=null) {
          using(var sr=new StreamReader(st)) {
            Topic.Import(sr, null);
          }
        }
      }
      _1SecTimer=new Timer(new TimerCallback(Tick1Sec), null, 5050-DateTime.Now.Millisecond, 1000);
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

      Topic.ready.Reset();
      Topic.paused=false;
      ThreadPool.QueueUserWorkItem(o => {
        CountStart(myId.value);
      });

      Topic.ready.WaitOne(2500);

      foreach(var i in _modules.Where(z => z.Metadata.priority>=16).OrderBy(z => z.Metadata.priority)) {
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
            enD.value=true;
          }
        }
        i.Value.Start();
        Log.Debug("plugin {0} loaded", i.Metadata.name??i.Value.GetType().FullName);
      }
    }

    public void Shutdown() {
      _1SecTimer.Change(Timeout.Infinite, Timeout.Infinite);
      foreach(var i in _modules.OrderByDescending(z => z.Metadata.priority)) {
        //i.Metadata.name
        i.Value.Stop();
      }

      _log.Dispose();
      Thread.Sleep(300);
      Topic.Export("../data/engine.xst", Topic.root.Get("/local/cfg"));
    }
    private void ProcessLog(LogEntry en) {
      string rez=null;
      switch(en.ll) {
      case LogLevel.Error:
        rez=string.Format("{0:HH:mm:ss.ff}[E] {1}", en.dt, en.msg);
        break;
      case LogLevel.Warning:
        rez=string.Format("{0:HH:mm:ss.ff}[W] {1}", en.dt, en.msg);
        break;
      case LogLevel.Info:
        rez=string.Format("{0:HH:mm:ss.ff}[I] {1}", en.dt, en.msg);
        break;
      case LogLevel.Debug:
        rez=string.Format("{0:HH:mm:ss.ff}[D] {1}", en.dt, en.msg);
        break;
      }
      if(en.ll!=LogLevel.Debug) {
        var dMsg=_lHead.Get<string>((_lHead.value).ToString("D2"));
        dMsg.saved=true;
        dMsg.value=string.Format("{0:dd} {1}", en.dt, rez);
        _lHead.value=(_lHead.value+1)%100;
      } else {
        _lHead.Get<string>("A0").value=string.Format("{0:dd} {1}", en.dt, rez);
      }

      //Console.WriteLine(rez);
      LogLevel lt=LogLevel.Info;
      if(_lThreshold!=null) {
        lt=_lThreshold.value;
      }
      if((int)en.ll>=(int)lt) {
        if(_lfPath==null || _firstDT!=en.dt.Date) {
          _firstDT=en.dt.Date;
          try {
            foreach(string f in Directory.GetFiles("../log/", "*.log", SearchOption.TopDirectoryOnly)) {
              if(File.GetLastWriteTime(f).AddDays(6)<_firstDT)
                File.Delete(f);
            }
          }
          catch(System.IO.IOException) {
          }
          _lfPath="../log/"+_firstDT.ToString("yyMMdd")+".log";
        }
        for(int i=2; i>=0; i--) {
          try {
            using(FileStream fs=File.Open(_lfPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) {
              fs.Seek(0, SeekOrigin.End);
              byte[] ba=Encoding.UTF8.GetBytes(rez+"\r\n");
              fs.Write(ba, 0, ba.Length);
            }
            break;
          }
          catch(System.IO.IOException) {
            Thread.Sleep(15);
          }
        }
      }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
      try {
        Log.Error("unhandled Exception {0}", e.ExceptionObject.ToString());
      }
      catch(Exception) {
      }
      if(!e.IsTerminating) {
        return;
      }
      try {
        MqBroker.Close();
      }
      catch(Exception) {
      }
      Thread.Sleep(400);
      try {
        _log.Dispose();
      }
      catch(Exception) {
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

    private void MQTT_Main_changed(Topic sender, TopicChanged param) {
      if(sender.path.StartsWith("/var/log") || sender.path.StartsWith("/var/now")) {
        return;
      }
      var ir=param.Initiator;
      switch(param.Art) {
      case TopicChanged.ChangeArt.Add:
        if(ir==null) {
          Log.Debug("+ {0}[{1}]", param.Source.path, param.Source.valueType);
        } else {
          Log.Debug("+ {0}[{1}] : {2}", param.Source.path, param.Source.valueType, ir.name);
        }
        break;
      case TopicChanged.ChangeArt.Value:
        if(ir==null) {
          Log.Debug("! {0}={1}", param.Source.path, param.Source.GetValue());
        } else {
          Log.Debug("! {0}={1} : {2}", param.Source.path, param.Source.GetValue(), ir.name);
        }
        break;
      case TopicChanged.ChangeArt.Remove:
        if(ir==null) {
          Log.Debug("- {0}", param.Source.path, param.Initiator);
        } else {
          Log.Debug("- {0} : {1}", param.Source.path, ir.name);
        }
        break;
      }
    }

    private static void Log_Write(LogLevel ll, DateTime dt, string msg) {
      _log.Enqueue(new LogEntry() { ll=ll, dt=dt, msg=msg });
    }

    private void CountStart(string id) {
      try {
        var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://s08.flagcounter.com/mini/Uatv/bg_676D8F/txt_FFFFFF/border_676D8F/flags_0/");

        // request line
        request.Method = "GET";

        // request headers
        request.Referer="mqtt://x13home.org/clients/"+id+"/";
        request.UserAgent="Mozilla/5.0 (compatible; MSIE 9.0; "+Environment.OSVersion.ToString()+")";
        request.ContentLength = 0;

        request.Timeout = 1500;     // 5 seconds
        // send request and receive response
        using(var response =(System.Net.HttpWebResponse)request.GetResponse()) {
        }
        request=null;
      }
      catch(Exception ex) {
        Log.Debug("Engine.CountStart - {0}", ex.Message);
      }
    }
    private struct LogEntry {
      public LogLevel ll;
      public DateTime dt;
      public string msg;
    }

  }
}
