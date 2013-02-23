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

namespace X13.CC {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    private X13.MQTT.MqClient _cl;
    private int _tryCounter;
    private bool _docLoaded=false;

    private const string _settPath="ccwpf.cfg";

    public MainWindow() {
      App.mainWindow=this;
      _tryCounter=3;
      Topic.Import(_settPath, "/local/settings");
      Topic brokerSettings=Topic.root.Get("/local/settings/Broker");
      //Topic.root.Subscribe("/#", root_changed);
      brokerSettings.Subscribe("_URL", BrokerUrlChanged);
      BrokerState="OFFLINE";
      _clState=0;   // offline
      _cl=new X13.MQTT.MqClient(MqClientStatusChanged);

      #region Load Security
      string securPath=brokerSettings.Get<string>("_path");
      if(string.IsNullOrEmpty(securPath)) {
        securPath=@"..\data\security.dat";
      } else {
        securPath=System.IO.Path.Combine(securPath, @"..\data\security.dat");
      }
      Topic.Import(securPath, "/local/security");

      _cl.UserName=brokerSettings.Get<string>("_username");
      brokerSettings.Get<string>("_username").saved=true;
      _cl.UserPass=brokerSettings.Get<string>("_password");
      brokerSettings.Get<string>("_password").saved=true;
      if(string.IsNullOrEmpty(_cl.UserName)) {
        Topic tp;
        if(Topic.root.Exist("/local/security/users/root", out tp)) {
          _cl.UserName="root";
          _cl.UserPass=(tp as DVar<string>).value;
        }
      }
      #endregion Load security
      InitializeComponent();

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
        this.Width=1100;
        this.Height=700;
      }
      this.dockManager.ActiveDocumentChanged+=new EventHandler(dockManager_ActiveDocumentChanged);
    }

    private void BrokerUrlChanged(Topic arg1, TopicChanged arg2) {
      this.Dispatcher.BeginInvoke(new Action(() => {
        if(_clState==2) {
          _cl.Disconnect();
        }
        dockManager_Loaded(null, null);
      }));
    }

    void root_changed(PLC.Topic sender, PLC.TopicChanged param) {
      var ir=param.Initiator;
      switch(param.Art) {
      case X13.PLC.TopicChanged.ChangeArt.Add:
        if(ir==null) {
          Log.Debug("+ {0}[{1}]", sender.path, sender.valueType);
        } else {
          Log.Debug("+ {0}[{1}] : {2}", sender.path, sender.valueType, ir.name);
        }
        break;
      case X13.PLC.TopicChanged.ChangeArt.Value:
        if(sender.path!="/system/now/second") {
          if(ir==null) {
            Log.Debug("! {0}={1}", sender.path, sender.GetValue());
          } else {
            Log.Debug("! {0}={1} : {2}", sender.path, sender.GetValue(), ir.name);
          }
        }
        break;
      case X13.PLC.TopicChanged.ChangeArt.Remove:
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
      DVar<string> brokerUrl=Topic.root.Get<string>("/local/settings/Broker/_URL");
      if(string.IsNullOrEmpty(brokerUrl.value)) {
        lock(this) {
          if(!dockManager.Documents.Any(z => z is SetupView)) {
            DockPane.Items.Add(GetContent("SetupView"));
          }
        }
      } else if(_clState==0) {
        _clState=1;
        BrokerState="Connecting";
          _cl.Connect(brokerUrl.value);
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
      } else if(name=="SetupView"){
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
      _cl.Disconnect();
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
      Topic.Export(_settPath, Topic.root.Get("/local/settings"));
    }
    private int _clState=0;
    private void MqClientStatusChanged(bool connected) {
      Dispatcher.BeginInvoke(new Action(delegate {
        if(connected) {
          if(_cl.UserName!=null) {
            BrokerState=string.Format("{0}@{1}", _cl.UserName, _cl.BrokerName);
          } else {
            BrokerState=_cl.BrokerName;
          }
          _cl.Subscribe("/#", QoS.AtMostOnce);
          Log.Info("Connected to {0}", _cl.BrokerName);
          _tryCounter=3;
          _clState=2;
          if(!_docLoaded) {
            _docLoaded=true;
            System.Threading.Thread.Sleep(1500);
            dockManager_Loaded(null, null);
          }
        } else if(--_tryCounter>0) {
          BrokerState="Connecting";
          _clState=1;
          System.Threading.ThreadPool.QueueUserWorkItem((o) => {
            System.Threading.Thread.Sleep(1200);
            _cl.Connect(Topic.root.Get<string>("/local/settings/Broker/_URL"));
          }
        );
        } else {
          _clState=0;
          BrokerState="OFFLINE";
        }
      }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void DockPanel_MouseUp(object sender, MouseButtonEventArgs e) {
      if(_clState==0) {
        _cl.Connect(Topic.root.Get<string>("/local/settings/Broker/_URL").value);
      } else if(_clState==2) {
        _cl.Disconnect();
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
