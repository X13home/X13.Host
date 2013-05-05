#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvalonDock;
using X13.PLC;
using System.Diagnostics;
using System.Threading;

namespace X13.CC {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    private X13.MQTT.MqClient _cl;
    private bool _docLoaded=false;
    private Process _engine; // for embedded mode
    private ManualResetEventSlim _engineReady;
    private Timer _1SecTimer;
    private DVar<DateTime> _now;
    private DVar<long> _nowOffset;

    private const string _settPath="../data/CC.xst";

    public MainWindow() {
      App.mainWindow=this;
      if(!Directory.Exists("../data")) {
        Directory.CreateDirectory("../data");
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

      Topic.Import(_settPath, "/local/cfg");
      var tmpD=Topic.root.Get<bool>("/local/cfg/Broker/enable");
      tmpD.saved=true;
      tmpD.value=false;
      tmpD=Topic.root.Get<bool>("/local/cfg/PLC/enable");
      tmpD.saved=true;
      tmpD.value=false;

      Topic brokerSettings=Topic.root.Get("/local/cfg/Client");
      brokerSettings.Subscribe("_URL", BrokerUrlChanged);
      BrokerState="OFFLINE";
      _clState=0;   // offline
      _cl=new X13.MQTT.MqClient();
      _cl.StatusChg+=MqClientStatusChanged;
      _cl.KeepAlive=10;

      var myId=Topic.root.Get<string>("/local/cfg/id");
      if(string.IsNullOrWhiteSpace(myId.value)) {
        myId.saved=true;
        myId.value=string.Format("{0}@{1}", Environment.UserName, Environment.MachineName);
      }

      InitializeComponent();
      //Topic.root.Subscribe("/#", root_changed);

      Log.Info("CC Start");
      DataContext = this;
      if(Settings.MainWindowWidth>0 && Settings.MainWindowHeight>0) {
        this.Width=Settings.MainWindowWidth;
        this.Height=Settings.MainWindowHeight;
        this.Left=Settings.MainWindowLeft;
        this.Top=Settings.MainWindowTop;
        this.WindowState=Settings.MainWindowState;
      } else {
        this.WindowStartupLocation=System.Windows.WindowStartupLocation.CenterScreen;
        this.Width=System.Windows.SystemParameters.PrimaryScreenWidth*0.6;
        this.Height=System.Windows.SystemParameters.PrimaryScreenHeight*0.8;
      }
      this.dockManager.ActiveDocumentChanged+=new EventHandler(dockManager_ActiveDocumentChanged);
    }

    private void _engine_OutputDataReceived(object sender, DataReceivedEventArgs e) {
      if(!string.IsNullOrEmpty(e.Data)) {
        Log.Info("Engine: {0}", e.Data);
        _engineReady.Set();
      }
    }

    private void BrokerUrlChanged(Topic arg1, TopicChanged arg2) {
      this.Dispatcher.BeginInvoke(new Action(() => {
        if(_clState==2) {
          _cl.Stop();
        }
        Topic brokerSettings=Topic.root.Get("/local/cfg/Client");
        string url=brokerSettings.Get<string>("_URL").value;
        if(string.IsNullOrWhiteSpace(url)) {
          return;
        }else if(url=="#local") {
          if(_engine==null || _engine.HasExited) {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            _engine = new Process();
            _engine.StartInfo.FileName = "Engine.exe";
            _engine.StartInfo.Arguments="";

            _engine.StartInfo.RedirectStandardInput=true;
            _engine.StartInfo.RedirectStandardOutput=true;
            _engine.StartInfo.RedirectStandardError=true;
            _engine.EnableRaisingEvents=true;
            _engine.StartInfo.UseShellExecute=false;
            _engine.StartInfo.CreateNoWindow = true;
            _engine.OutputDataReceived+=new DataReceivedEventHandler(_engine_OutputDataReceived);
            _engine.ErrorDataReceived+=new DataReceivedEventHandler(_engine_OutputDataReceived);

            _engineReady=new ManualResetEventSlim(false);
            _engine.Start();

            _engine.BeginErrorReadLine();
            _engine.BeginOutputReadLine();
            _engineReady.Wait(5000);
          } else {
            Thread.Sleep(1500);
          }
        }

        dockManager_Loaded(null, null);
      }));
    }

    private void Tick1Sec(object o) {
      DateTime nowDT=DateTime.Now.AddMilliseconds(_nowOffset.value);
      _1SecTimer.Change(1050-nowDT.Millisecond, 1000);
      _now=Topic.root.Get<DateTime>("/var/now");
      _now.SetValue(nowDT, new TopicChanged(TopicChanged.ChangeArt.Value, _now));
      _now.Get<long>("second").SetValue(nowDT.Second, new TopicChanged(TopicChanged.ChangeArt.Value, _now));
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


    void root_changed(Topic sender, TopicChanged param) {
      var ir=param.Initiator;
      switch(param.Art) {
      case TopicChanged.ChangeArt.Add:
        if(ir==null) {
          Log.Debug("+ {0}[{1}]", sender.path, sender.valueType);
        } else {
          Log.Debug("+ {0}[{1}] : {2}", sender.path, sender.valueType, ir.name);
        }
        break;
      case TopicChanged.ChangeArt.Value:
        if(ir==null) {
          if(!sender.path.StartsWith("/dev/.clock/")) {
            Log.Debug("! {0}={1}", sender.path, sender.GetValue());
          }
        } else if(!ir.path.StartsWith("/dev/.clock/")) {
          Log.Debug("! {0}={1} : {2}", sender.path, sender.GetValue(), ir.name);
        }
        break;
      case TopicChanged.ChangeArt.Remove:
        if(ir==null) {
          Log.Debug("- {0}", sender.path, param.Initiator);
        } else {
          Log.Debug("- {0} : {1}", sender.path, ir.name);
        }
        break;
      }
    }

    private void dockManager_ActiveDocumentChanged(object sender, EventArgs e) {
      LogramView doc=this.dockManager.ActiveDocument as LogramView;
      if(doc!=null) {
        App.currentLogram=doc.model;
      }
    }
    public string BrokerState {
      get { return (string)GetValue(brokerState); }
      set { SetValue(brokerState, value); }
    }

    public static readonly DependencyProperty brokerState =
            DependencyProperty.Register("BrokerState", typeof(string), typeof(MainWindow), new UIPropertyMetadata(null));

    private void dockManager_Loaded(object sender, RoutedEventArgs e) {
      DVar<string> brokerUrl=Topic.root.Get<string>("/local/cfg/Client/_URL");
      if(string.IsNullOrEmpty(brokerUrl.value)) {
        lock(this) {
          if(!dockManager.Documents.Any(z => z is SetupView)) {
            DockPane.Items.Add(GetContent("SetupView"));
          }
        }
      } else if(_clState==0) {
        _clState=1;
        BrokerState="Connecting";
        _cl.Start();
      } else if(_clState==2) {
        dockManager.DeserializationCallback=new DockingManager.DeserializationCallbackHandler(DSPane);
        if(Settings.Layout!=null) {
          MemoryStream ms=new MemoryStream(Settings.Layout);
          ms.Seek(0, SeekOrigin.Begin);
          dockManager.RestoreLayout(ms);
        }
        if(dockManager.Documents.Count>0) {
          dockManager.Documents[0].Activate();
        }
      }
    }
    private void ActivateView(object sender, RoutedEventArgs e) {
      MenuItem mi=e.Source as MenuItem;
      if(mi!=null) {
        ManagedContent rez=GetContent(mi.Tag as string);
        if(rez!=null) {
          rez.Show(dockManager);
          rez.Activate();
        }
      }
    }
    private void DSPane(object sender, DeserializationCallbackEventArgs e) {
      e.Content=GetContent(e.Name);
      if(e.Content==null) {
        Log.Warning("{0} not restored", e.Name);
      }
    }
    private ManagedContent GetContent(string name) {
      ManagedContent rez=null;
      if(string.IsNullOrEmpty(name)) {
      } else if(name=="PropertyPanel") {
        rez=dockManager.DockableContents.FirstOrDefault(p => p is PropertyView)??new PropertyView();
      } else if(name=="LogPanel") {
        rez=dockManager.DockableContents.FirstOrDefault(p => p is LogView)??new LogView();
      } else if(name=="DataStoragePanel") {
        rez=dockManager.DockableContents.FirstOrDefault(p => p is DataStorageView)??new DataStorageView();
      } else if(name=="SetupView") {
        rez=dockManager.Documents.FirstOrDefault(p => p is SetupView)??new SetupView();
      } else if(name=="Logram") {
        string baseDocTitle = "Logram_New_1Logram_1";
        int i = 1;
        string lName = baseDocTitle + i.ToString();

        while(dockManager.Documents.Any(d => d.Name == lName)) {
          i++;
          lName = baseDocTitle + i.ToString();
        }
        rez=new LogramView(lName);
      } else if(name.StartsWith("Logram_")) {
        rez=dockManager.Documents.FirstOrDefault(p => p is DocumentContent && p.Name==name)??new LogramView(name);
      }
      return rez;
    }
    private void Window_Closing(object sender, CancelEventArgs e) {
      {
        var node=Settings.Layout;
        MemoryStream ms=new MemoryStream();
        dockManager.SaveLayout(ms);
        Settings.Layout=ms.ToArray();
      }
      {
        Settings.MainWindowState=this.WindowState;
        Settings.MainWindowLeft=(int)this.Left;
        Settings.MainWindowTop=(int)this.Top;
        Settings.MainWindowWidth=(int)this.Width;
        Settings.MainWindowHeight=(int)this.Height;

      }
      System.Threading.Thread.Sleep(150);
      _cl.Stop();
      if(_engine!=null) {
        _engine.StandardInput.WriteLine(" ");
        _engine.WaitForExit(1500);
        _engine=null;
      }
      Topic.Export(_settPath, Topic.root.Get("/local/cfg"));
    }
    private int _clState=0;
    private void MqClientStatusChanged(bool connected) {
      Dispatcher.BeginInvoke(new Action(delegate {
        if(connected) {
          string userName=Topic.root.Get<string>("/local/cfg/Client/_username").value;
          if(userName!=null) {
            BrokerState=string.Format("{0}@{1}", userName, _cl.BrokerName);
          } else {
            BrokerState=_cl.BrokerName;
          }
          //_cl.Subscribe("/#", QoS.AtMostOnce);
          Log.Info("Connected to {0}", _cl.BrokerName);
          _clState=2;
          if(!_docLoaded) {
            _docLoaded=true;
            System.Threading.Thread.Sleep(1500);
            dockManager_Loaded(null, null);
          }
        } else{
          BrokerState="Connecting";
          _clState=1;
        }
      }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void DockPanel_MouseUp(object sender, MouseButtonEventArgs e) {
      if(_clState==0) {
        _cl.Start();
      } else if(_clState==2) {
        _cl.Stop();
      }
    }

    private void Export_Click(object sender, RoutedEventArgs e) {
      DataStorageView st=GetContent("DataStoragePanel") as DataStorageView;
      if(st==null) {
        return;
      }
      Topic head=st.Selected;
      Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
      dlg.Title=string.Format("Export {0}", head==Topic.root?"root":head.path);
      dlg.FileName = head==Topic.root?"root":head.name; // Default file name
      dlg.DefaultExt = ".xst"; // Default file extension
      dlg.Filter = "Exported storage (.xst)|*.xst"; // Filter files by extension

      if(dlg.ShowDialog() == true) {    // Show save file dialog box
        Topic.Export(dlg.FileName, head);  // Save document
      }
    }

    private void Import_Click(object sender, RoutedEventArgs e) {
      Microsoft.Win32.OpenFileDialog dlg=new Microsoft.Win32.OpenFileDialog();
      dlg.Title="Import";
      dlg.DefaultExt = ".xst"; // Default file extension
      dlg.Filter = "Exported storage (.xst)|*.xst"; // Filter files by extension
      dlg.CheckFileExists=true;
      if(dlg.ShowDialog()==true) {
        Topic.Import(dlg.FileName);
      }
    }

    private void ImportTo_Click(object sender, RoutedEventArgs e) {
      DataStorageView st=GetContent("DataStoragePanel") as DataStorageView;
      if(st==null) {
        return;
      }
      Topic head=st.Selected;
      Microsoft.Win32.OpenFileDialog dlg=new Microsoft.Win32.OpenFileDialog();
      dlg.Title=string.Format("Import to {0}", head==Topic.root?"root":head.path);
      dlg.DefaultExt = ".xst"; // Default file extension
      dlg.Filter = "Exported storage (.xst)|*.xst"; // Filter files by extension
      dlg.CheckFileExists=true;
      if(dlg.ShowDialog()==true) {
        Topic.Import(dlg.FileName, head.path);
      }
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e) {
      this.Close();
    }
  }
}
