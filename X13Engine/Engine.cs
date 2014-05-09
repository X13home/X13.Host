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
      _logToConsole=true;
      var eng=new Engine();
      if(eng.StartUp(args.Length==1?args[0]:string.Empty)) {
        Console.ForegroundColor=ConsoleColor.Green;
      }
      Console.WriteLine("Engine running; press Enter to Exit");
      Console.ResetColor();
      Console.Read();
      eng.Shutdown();
      Console.ForegroundColor=ConsoleColor.Gray;
    }
    public static bool IsLinux {
      get {
        int p = (int)Environment.OSVersion.Platform;
        return (p == 4) || (p == 6) || (p == 128);
      }
    }
    private static Mutex _singleInstance;
    private static BlockingQueue<LogEntry> _log;
    private static bool _logToConsole;
    private static DateTime _startDT;
    private string _lfPath;
    private DateTime _firstDT;
    private DVar<LogLevel> _lThreshold;
    private DVar<long> _lHead;
    private DVar<bool> _debug;
    private Timer _statTimer;
    private string _cfgPath;
    private Plugins _plugins;

    public bool StartUp(string cfgPath=null) {
      string path=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      Directory.SetCurrentDirectory(path);
      if(!string.IsNullOrWhiteSpace(cfgPath)) {
        _cfgPath=cfgPath;
      } else {
        _cfgPath="../data/Engine.xst";
      }

      string siName=string.Format("Global\\X13.engine@{0}", Path.GetFullPath(_cfgPath).Replace('\\', '$'));
      _singleInstance=new Mutex(true, siName);

      _log=new BlockingQueue<LogEntry>(ProcessLog);
      if(!Directory.Exists("../log")) {
        Directory.CreateDirectory("../log");
      }
      if(!Directory.Exists("../data")) {
        Directory.CreateDirectory("../data");
      }
      AppDomain.CurrentDomain.UnhandledException+=new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

      var root=Topic.root;
      Topic.Import(_cfgPath, "/local/cfg");

      _lHead=root.Get<long>("/var/log");
      _lThreshold=root.Get<LogLevel>("/etc/log/threshold");

      Log.Write+=new Action<LogLevel, DateTime, string>(Log_Write);

      if(!_singleInstance.WaitOne(TimeSpan.Zero, true)) {
        Log.Error("only one instance at a time");
        _singleInstance=null;
        return false;
      }

      Topic.brokerMode=true;
      _plugins=new Plugins();
      _plugins.Init(true);

      _debug=Topic.root.Get<bool>("/local/cfg/repository/_verbose");
      if(_debug.value) {
        root.Subscribe("/#", MQTT_Main_changed);
      }

      string dbVersion=Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
      var dbVer=Topic.root.Get<string>("/etc/system/version");
      if(dbVer.value==null || string.Compare(dbVer.value, dbVersion)<0) {
        dbVer.saved=true;
        dbVer.value=dbVersion;
        _lHead.saved=true;
        _lHead.Get<string>("A0").saved=false;
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

      _plugins.Start();
      _startDT=DateTime.Now;
      _statTimer=new Timer(o => {
        SendStat(2);
      }, null, 7200000, 7200000);
      ThreadPool.QueueUserWorkItem(o => {
        SendStat(1);
        Topic.Export(_cfgPath, Topic.root.Get("/local/cfg"));
      });
      return true;
    }

    public void Shutdown() {
      if(_statTimer!=null) {
        _statTimer.Change(Timeout.Infinite, Timeout.Infinite);
      }
      if(_plugins!=null) {
        _plugins.Stop();
      }
      SendStat(0);
      _log.Dispose();
      Thread.Sleep(300);
      if(_singleInstance!=null) {
        _singleInstance.ReleaseMutex();
      }
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
      if(_logToConsole) {
        switch(en.ll) {
        case LogLevel.Debug:
          break;
        case LogLevel.Info:
          Console.ForegroundColor=ConsoleColor.White;
          Console.WriteLine(rez);
          break;
        case LogLevel.Warning:
          Console.ForegroundColor=ConsoleColor.Yellow;
          Console.WriteLine(rez);
          break;
        case LogLevel.Error:
          Console.ForegroundColor=ConsoleColor.Red;
          Console.WriteLine(rez);
          break;
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
      Console.ForegroundColor=ConsoleColor.Gray;
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

    public static void SendStat(int cmd) {
      var id=Topic.root.Get<string>("/etc/system/id");
      if(string.IsNullOrWhiteSpace(id.value)) {
        string ids=string.Empty;
        id.saved=true;
        foreach(System.Net.NetworkInformation.NetworkInterface adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) {
          System.Net.NetworkInformation.PhysicalAddress address = adapter.GetPhysicalAddress();
          ids=BitConverter.ToString(address.GetAddressBytes());
          if(!string.IsNullOrWhiteSpace(ids)) {
            break;
          }
        }
        if(string.IsNullOrWhiteSpace(ids)) {
          ids=Guid.NewGuid().ToString();
        }
        System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(ids));
        id.value=(new Guid(hash)).ToString();
      }
      string id_s=id.value.Substring(0, 8).ToUpper();
      string an=Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location).ToLower();
      string av=Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
      string ngl=Topic.root.Get<string>("/etc/PLC/default").value;
      string nlc=Topic.root.Get<string>("/local/cfg/id").value;
      string pl;
      if(cmd==1) {
        pl=string.Concat("start&el=", Environment.Version.ToString(4));
      } else if(cmd==0) {
        Topic zNode;
        if(Topic.brokerMode?Topic.root.Exist("/dev", out zNode):Topic.root.Exist("/plc", out zNode)) {
          pl="stop&el="+(zNode.children.Count()-1).ToString();
        } else {
          pl="stop";
        }
      } else {
        pl=string.Concat("uptime&el=", (DateTime.Now-_startDT).TotalDays.ToString("##0.00"));
      }
      try {
        string url=string.Format("v=1&tid=UA-40770280-3&cid={0}&an={1}&av={2}&t=event&ec={5}+{3}&cd={4}&ea={4}+{6}",
          id.value, an, av, ngl, nlc, id_s, pl);

        var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://www.google-analytics.com/collect");

        // request line
        request.Method = "POST";
        // request headers
        request.Referer=System.Net.Dns.GetHostName();
        request.UserAgent="Mozilla/5.0 (compatible; MSIE 9.0; "+Environment.OSVersion.ToString()+")";
        byte[] buf=Encoding.UTF8.GetBytes(url);
        request.ContentLength = buf.Length;
        var os = request.GetRequestStream();
        os.Write(buf, 0, buf.Length); //Push it out there
        os.Close();
        request.Timeout = 1500;
        // send request and receive response
        using(var response =(System.Net.HttpWebResponse)request.GetResponse()) {
          if(response.StatusCode!=System.Net.HttpStatusCode.OK) {
            Log.Debug("Engine.SendStat - {0}", response.StatusCode);
          }
        }
        request=null;
      }
      catch(Exception ex) {
        Log.Debug("Engine.SendStat - {0}", ex.Message);
      }
    }
    private struct LogEntry {
      public LogLevel ll;
      public DateTime dt;
      public string msg;
    }

  }
}
