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

namespace X13.CC {
  /// <summary>
  /// Interaction logic for SetupView.xaml
  /// </summary>
  public partial class SetupView : DocumentContent  {
    private const string _enterUrlText="localhost";
    private DVar<string> _brokerUrl;
    public SetupView() {
      InitializeComponent();
      _brokerUrl=Topic.root.Get<string>("/local/cfg/Client/_URL");
      if(string.IsNullOrWhiteSpace(_brokerUrl.value)) {
        RemoteUrl.Text=_enterUrlText;
      } else {
        RemoteUrl.Text=_brokerUrl.value;
      }
    }

    private void RemoteUrl_GotFocus(object sender, RoutedEventArgs e) {
      if(RemoteUrl.Text==_enterUrlText) {
        RemoteUrl.Text=_brokerUrl.value;
      }
    }

    private void RemoteUrl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
      if(string.IsNullOrWhiteSpace(RemoteUrl.Text)) {
        RemoteUrl.Text=_enterUrlText;
      }
    }

    private void Button_Click(object sender, RoutedEventArgs e) {
      DVar<string> userName=Topic.root.Get<string>("/local/cfg/Client/_username");
      _brokerUrl.saved=true;
      userName.saved=true;
      if(EngineEmbedded.IsChecked.Value) {
        userName.value="local";
        _brokerUrl.value="#local";
      } else if(EngineInstall.IsChecked.Value) {
        biInstall.IsBusy=true;
        InstallService();
        userName.value="local";
        _brokerUrl.value="localhost";
        biInstall.IsBusy=false;
      } else {
        if(RemoteUrl.Text=="localhost") {
          userName.value="local";
        }
        _brokerUrl.value=RemoteUrl.Text;
      }
      System.Windows.Forms.Application.Restart();
      Application.Current.Shutdown();
    }

    private void InstallService() {
      var p = new Process();
      p.StartInfo.FileName = "X13Svc.exe";
      p.StartInfo.Arguments="/i";
      if(System.Environment.OSVersion.Version.Major >= 6) {
        p.StartInfo.Verb = "runas";
      }
      p.Start();
      p.WaitForExit();
    }
  }
}
