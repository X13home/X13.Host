#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using AvalonDock;
using X13.PLC;
using System;

namespace X13.CC {
  /// <summary>
  /// Interaction logic for SetupView.xaml
  /// </summary>
  public partial class SetupView : DocumentContent  {
    private const string _enterUrlText="localhost";
    private DVar<string> _clUrl;
    private DVar<string> _clUser;
    private DVar<string> _clPass;
    public SetupView() {
      InitializeComponent();
      _clUser=Topic.root.Get<string>("/local/cfg/Client/_username");
      _clUser.saved=true;
      if(!string.IsNullOrEmpty(_clUser.value)) {
        this.Username.Text=_clUser.value;
      } else {
        this.Username.Text="local";
      }
      _clPass=Topic.root.Get<string>("/local/cfg/Client/_password");
      _clPass.saved=true;
      if(!string.IsNullOrEmpty(_clPass.value)) {
        this.Password.Text=_clPass.value;
      }
      _clUrl=Topic.root.Get<string>("/local/cfg/Client/_URL");
      _clUrl.saved=true;
      if(string.IsNullOrWhiteSpace(_clUrl.value)) {
        RemoteUrl.Text=_enterUrlText;
      } else {
        RemoteUrl.Text=_clUrl.value;
      }
    }

    private void RemoteUrl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
      if(string.IsNullOrWhiteSpace(RemoteUrl.Text)) {
        RemoteUrl.Text=_enterUrlText;
      }
    }

    private void Button_Click(object sender, RoutedEventArgs e) {
      biInstall.IsBusy=true;
      string url;
      if(EngineEmbedded.IsChecked.Value) {
        url="#local";
        _clUrl.value=url;
        _clUser.value="local";
        biInstall.BusyContent="Start Embedded engine";
      } else if(EngineInstall.IsChecked.Value) {
        url="$local";
        _clUser.value="local";
        _clUrl.value="localhost";
        biInstall.BusyContent="Install Service";
      } else {
        url=RemoteUrl.Text;
        _clUser.value=Username.Text;
        _clPass.value=Password.Text;
        _clUrl.value=url;
        biInstall.BusyContent="Connecting";
      }
      System.Threading.ThreadPool.QueueUserWorkItem(ProcUrl, url);
    }

    private void ProcUrl(object o) {
      string url=o as string;
      if(url=="#local") {
        App.mainWindow.StartEmbeddedEngine();
      } else if(url=="$local") {
        var p = new Process();
        p.StartInfo.FileName = "X13Svc.exe";
        p.StartInfo.Arguments="/i";
        if(System.Environment.OSVersion.Version.Major >= 6) {
          p.StartInfo.Verb = "runas";
        }
        p.Start();
        p.WaitForExit();
      }
      if(!string.IsNullOrEmpty(_clUrl.value)) {
        App.mainWindow._cl.Init();
        App.mainWindow._cl.Start();
      }
      this.Dispatcher.BeginInvoke(new Action(() => {
        biInstall.IsBusy=false;
        this.Close();
      }));
    }
  }
}
