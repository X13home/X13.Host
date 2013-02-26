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
      _brokerUrl=Topic.root.Get<string>("/local/settings/Broker/_URL");
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
      _brokerUrl.saved=true;
      if(EngineEmbedded.IsChecked.Value) {
        _brokerUrl.value="#local";
      } else if(EngineInstall.IsChecked.Value) {
        InstallService();
        _brokerUrl.value="localhost";
      } else {
        _brokerUrl.value=RemoteUrl.Text;
      }
      this.Close();
    }

    private void InstallService() {
      ProcessStartInfo pi=new ProcessStartInfo("X13Engine.exe", "/i");
      if(System.Environment.OSVersion.Version.Major >= 6) {
        pi.Verb = "runas";
      }
      Process p=Process.Start(pi);
      p.WaitForExit();
    }
  }
}
