#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using X13.MQTT;
using X13.PLC;
using X13.WOUM;
using System.IO;
using System.Reflection;
using X13;

namespace X13.Svc {
  public partial class X13Svc : ServiceBase {
    private static BlockingQueue<LogEntry> _log;
    private Timer _1SecTimer;
    private string _lfPath;
    private DateTime _firstDT;
    private DVar<LogLevel> _lThreshold;
    private PersistentStorage _pStorage;
    public X13Svc() {
      InitializeComponent();
    }

    public void StartUp() {
      OnStart(null);
    }
    public void Shutdown() {
      OnStop();
    }

    protected override void OnStart(string[] args) {
      Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

      _log=new BlockingQueue<LogEntry>(ProcessLog);
      if(!Directory.Exists("..\\Log")) {
        Directory.CreateDirectory("..\\Log");
      }
      if(!Directory.Exists("..\\Data")) {
        Directory.CreateDirectory("..\\Data");
      }
      AppDomain.CurrentDomain.UnhandledException+=new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
      Log.Write+=new Action<LogLevel, DateTime, string>(Log_Write);
      Topic.brokerMode=true;
      var root=Topic.root;
      _lThreshold=root.Get<LogLevel>("/system/log/threshold");
      Log.Info("Starting");
      BiultInStatements.Initialize();
      _1SecTimer=new Timer(new TimerCallback(Tick1Sec), null, 1050-DateTime.Now.Millisecond, 1000);
      {
        Topic nowTp=Topic.root.Get("/system/now");
        DateTime nowDT=DateTime.Now;
        nowTp.Get<long>("second").value=nowDT.Second;
        nowTp.Get<long>("minute").value=nowDT.Minute;
        nowTp.Get<long>("hour").value=nowDT.Hour;
        nowTp.Get<DayOfWeek>("wDay").value=nowDT.DayOfWeek;
        nowTp.Get<long>("day").value=nowDT.Day;
        nowTp.Get<long>("month").value=nowDT.Month;
        nowTp.Get<long>("year").value=nowDT.Year;

      }
      #region Load Security
      if(!Topic.Import(@"..\data\security.dat", "/local/security")) {
        Topic sec=Topic.root.Get("/local/security");
        byte[] randBytes=new byte[18];
        (new Random()).NextBytes(randBytes);
        SetTopic("users/root", System.Convert.ToBase64String(randBytes).Substring(2, 16), sec);
        SetTopic("users/user", " ", sec);
        SetTopic("groups/0", "Administrators", sec);
        sec.Get("groups/0/root");
        SetTopic("groups/1", "Users", sec);
        sec.Get("groups/1/user");
        SetTopic<uint>("acls/Public", 0x1F000001, sec);

        Topic.Export(@"..\data\security.dat", sec);
      }
      #endregion Load security
      string pmPath=@"..\data\persist.db3";
      Topic.paused=true;
      _pStorage=new PersistentStorage();
      bool db=_pStorage.Open(pmPath);
      string dbVersion="0.2.1";
      var dbVer=Topic.root.Get<string>("/system/db/version");
      if(!db || dbVer.value!=dbVersion) {
        dbVer.saved=true;
        dbVer.value=dbVersion;
        _lThreshold.saved=true;
        _lThreshold.value=LogLevel.Info;
        Log.Info("Load default declarers");
        var st=Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.PLC.declarers.xst");
        if(st!=null) {
          using(var sr=new StreamReader(st)){
            Topic.Import(sr, null);
          }
        }
      }
      //var rf12=root.Get<MsGateway>("/rf12");
      //if(rf12.value==null) {
      //  rf12.value=new MsGateway();
      //}
      //if(rf12.value.SerialPortName==null) {
      //  rf12.value.SerialPortName=string.Empty;
      //}
      root.Get<string>("/system/declarers/L_Folder").value="/CC;component/Images/ty_PLC.png";
      root.Get<string>("/plc/_declarer").value="L_Folder";
      foreach(Topic acl in Topic.root.Get("/local/security/acls").children){
        SetAcl(acl, Topic.root);
      }
      Topic.paused=false;
      //root.Subscribe("/#", MQTT_Main_changed);
      MqBroker.Open();
    }
    private void SetTopic<T>(string path, T value, Topic mp) {
      if(mp==null) {
        mp=Topic.root;
      }
      var tp=mp.Get<T>(path);
      tp.saved=true;
      tp.value=value;
    }
    private void SetAcl(Topic acl, Topic dParent) {
      if(acl==null || dParent==null) {
        return;
      }
      var dCur=dParent.Get(acl.name);
      var aCur=acl as DVar<uint>;
      if(aCur!=null) {
        Topic groups=Topic.root.Get("/local/security/groups");
        if(groups.Exist(((ushort)aCur.value).ToString(), out dCur.grpOwner)) {
          dCur.aclAll=(TopicAcl)((aCur.value>>28) & 0x0F);
          dCur.aclOwner=(TopicAcl)((aCur.value>>24) & 0x0F);
        } else {
          Log.Warning("unknown ACL group in {0}={1}", aCur.path, aCur.value);
        }
      }
      foreach(Topic nAcl in acl.children) {
        SetAcl(nAcl, aCur);
      }
    }
  
    protected override void OnStop() {
      _1SecTimer.Change(Timeout.Infinite, Timeout.Infinite);
      MqBroker.Close();
      _pStorage.Close();
      _log.Dispose();
      Thread.Sleep(300);
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
      //Console.WriteLine(rez);
      LogLevel lt=LogLevel.Info;
      if(_lThreshold!=null) {
        lt=_lThreshold.value;
      }
      if((int)en.ll>=(int)lt) {
        if(_lfPath==null || _firstDT!=en.dt.Date) {
          _firstDT=en.dt.Date;
          try {
            foreach(string f in Directory.GetFiles("..\\Log\\", "*.log", SearchOption.TopDirectoryOnly)) {
              if(File.GetLastWriteTime(f).AddDays(6)<_firstDT)
                File.Delete(f);
            }
          }
          catch(System.IO.IOException) {
          }
          _lfPath="..\\Log\\"+_firstDT.ToString("yyMMdd")+".log";
        }
        for(int i=2; i>=0; i--) {
          try {
            using(FileStream fs=File.Open(_lfPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) {
              fs.Seek(0, SeekOrigin.End);
              byte[] ba=Encoding.UTF8.GetBytes(rez+"\r\n");
              fs.Write(ba, 0, ba.Length);
            }
            //using(StreamWriter lf=File.AppendText(_lfPath)) {
            //  lf.WriteLine(rez);
            //}
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
        _pStorage.Close();
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
      DateTime nowDT=DateTime.Now;
      _1SecTimer.Change(1050-nowDT.Millisecond, 1000);
      Topic nowTp=Topic.root.Get("/system/now");
      var ns=nowTp.Get<long>("second");
      ns.SetValue(nowDT.Second, new TopicChanged(TopicChanged.ChangeArt.Value, ns));
      if(nowDT.Second==0) {
        nowTp.Get<long>("minute").SetValue(nowDT.Minute, new TopicChanged(TopicChanged.ChangeArt.Value, ns));
        if(nowDT.Minute==0) {
          nowTp.Get<long>("hour").SetValue(nowDT.Hour, new TopicChanged(TopicChanged.ChangeArt.Value, ns));
          if(nowDT.Hour==0) {
            nowTp.Get<DayOfWeek>("wDay").SetValue(nowDT.DayOfWeek, new TopicChanged(TopicChanged.ChangeArt.Value, ns));
            nowTp.Get<long>("day").SetValue(nowDT.Day, new TopicChanged(TopicChanged.ChangeArt.Value, ns));
            if(nowDT.Day==1) {
              nowTp.Get<long>("month").SetValue(nowDT.Month, new TopicChanged(TopicChanged.ChangeArt.Value, ns));
              if(nowDT.Month==1) {
                nowTp.Get<long>("year").SetValue(nowDT.Year, new TopicChanged(TopicChanged.ChangeArt.Value, ns));
              }
            }
          }
        }
      }
    }

    private static void MQTT_Main_changed(Topic sender, TopicChanged param) {
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
          if(!param.Source.path.StartsWith("/system/now/")) {
            Log.Debug("! {0}={1}", param.Source.path, param.Source.GetValue());
          }
        } else if(!ir.path.StartsWith("/system/now/")) {
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

    private struct LogEntry {
      public LogLevel ll;
      public DateTime dt;
      public string msg;
    }
  }
}
