#region license
//Copyright (c) 2011-2015 Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace X13.Agent2 {
  /// <summary>
  /// Interaktionslogik für MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    private System.Windows.Forms.NotifyIcon notifyIcon;
    private SayTimeRu _st;
    private X13.MQTT.MqClient _cl;

    public MainWindow() {
      InitializeComponent();
      notifyIcon = new System.Windows.Forms.NotifyIcon();
      notifyIcon.Click+=notifyIcon_Click;
      notifyIcon.Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/Agent2;component/Images/logo64.ico")).Stream);
      StateChanged += OnStateChanged;
    }
    private void Window_Loaded(object sender, RoutedEventArgs e) {
      notifyIcon.Visible = true;
      this.ShowInTaskbar = false;
      _cl=new X13.MQTT.MqClient("mqtt://olymp/");
      _st=new SayTimeRu();
      _cl.Subscribe("/var/Events/saytime", (p, j) => { if(j=="true") { _st.SayTime(); } });
      _cl.Subscribe("/var/Events/doorOpened", (p, j) => { if(j=="true") { SayTimeRu.PlayWav("Door41Opened"); } });
      _cl.Subscribe("/var/Events/doorClosed", (p, j) => { if(j=="true") { SayTimeRu.PlayWav("Door41Closed"); } });
	}

    void notifyIcon_Click(object sender, EventArgs e) {
      if(this.WindowState == WindowState.Minimized) {
        this.WindowState = WindowState.Normal;
      }
      this.Show();
    }

    private void OnStateChanged(object sender, EventArgs e) {
      if(this.WindowState == WindowState.Minimized) {
        this.ShowInTaskbar = false;
      } else {
        this.ShowInTaskbar = true;
      }
    }
    private void Window_Closed(object sender, EventArgs e) {
      try {
        if(notifyIcon != null) {
          notifyIcon.Visible = false;
          notifyIcon.Dispose();
          notifyIcon = null;
        }
      }
      catch(Exception ex) {
        Log.Warning("Window_Closed - {0}", ex);
      }
      Log.Finish();
    }
  }
}
