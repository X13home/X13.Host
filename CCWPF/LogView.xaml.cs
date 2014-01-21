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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AvalonDock;
using System.Collections.ObjectModel;
using X13.PLC;
using System.IO;

namespace X13.CC {
  /// <summary>
  /// Interaction logic for LogView.xaml
  /// </summary>
  public partial class LogView : DockableContent {
    private static LogView _instance;
    private const int _halfLength=500;

    private ObservableCollection<LogEntry> LogCollection;
    private bool _showDebug;
    private Action<LogEntry> AddLogEntryDelegate;
    private DVar<long> _lHead;
    private DVar<string> _lDebug;
    private long _oldHead;
    private X13.WOUM.BlockingQueue<LogEntry> _log;
    private string _lfPath;

    public LogView() {
      _instance=this;
      AddLogEntryDelegate=new Action<LogEntry>(AddLogEntry);

      InitializeComponent();
      this.DataContext = this;
      if(!Directory.Exists("../log")) {
        Directory.CreateDirectory("../log");
      }

      var now=DateTime.Now;
      try {
        foreach(string f in Directory.GetFiles("../log/", "*_cc.log", SearchOption.TopDirectoryOnly)) {
          if(File.GetLastWriteTime(f).AddDays(3)<now)
            File.Delete(f);
        }
      }
      catch(System.IO.IOException) {
      }
      _lfPath="../log/"+now.ToString("yyMMdd")+"_cc.log";

      _log=new X13.WOUM.BlockingQueue<LogEntry>(ProcessLog);
      Log.Write+=new Action<LogLevel, DateTime, string>(Log_Write);

      LogCollection=new ObservableCollection<LogEntry>();
      lvLog.ItemsSource=CollectionViewSource.GetDefaultView(LogCollection);
      this._showDebug=Settings.LogShowDebug;
      tbShowDebug.IsChecked=_showDebug;
      if(_showDebug) {
        (lvLog.ItemsSource as System.ComponentModel.ICollectionView).Filter=(o) => true;
      } else {
        (lvLog.ItemsSource as System.ComponentModel.ICollectionView).Filter=(o) => (o as LogEntry).ll!=LogLevel.Debug;
      }
      _lHead=Topic.root.Get<long>("/var/log");
      _oldHead=(_lHead.value+1)%100;
      _lHead.changed+=_lHead_changed;
      _lDebug=_lHead.Get<string>("A0");
      _lDebug.changed+=_lDebug_changed;
    }

    private void _lDebug_changed(Topic sender, TopicChanged args) {
      if(_lDebug.value!=null) {
        Log_Write(new LogEntry(_lDebug.value));
      }
    }

    private void _lHead_changed(Topic sender, TopicChanged args) {
      Topic lEntry;
      while(_oldHead!=_lHead.value) {
        if(_lHead.Exist(_oldHead.ToString("D2"), out lEntry)) {
          Log_Write(new LogEntry((lEntry as DVar<string>).value));
        }
        _oldHead=(_oldHead+1)%100;
      }
    }

    private void Log_Write(LogLevel logLevel, DateTime time, string text) {
      var le=new LogEntry(time, logLevel, text);
      Log_Write(le);
#if DEBUG
      _log.Enqueue(le);
#endif
    }
    private void ProcessLog(LogEntry en) {
      string rez=null;
      switch(en.ll) {
      case LogLevel.Error:
        rez=string.Format("{0:HH:mm:ss.ff}[{2}] {1}", en.dt, en.msg, en.local?"e":"E");
        break;
      case LogLevel.Warning:
        rez=string.Format("{0:HH:mm:ss.ff}[{2}] {1}", en.dt, en.msg, en.local?"w":"W");
        break;
      case LogLevel.Info:
        rez=string.Format("{0:HH:mm:ss.ff}[{2}] {1}", en.dt, en.msg, en.local?"i":"I");
        break;
      case LogLevel.Debug:
        rez=string.Format("{0:HH:mm:ss.ff}[{2}] {1}", en.dt, en.msg, en.local?"d":"D");
        break;
      }


      if(en.ll!=LogLevel.Debug ||  _showDebug) {
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
            System.Threading.Thread.Sleep(15);
          }
        }
      }
    }

    private void Log_Write(LogEntry le) {
      Dispatcher.BeginInvoke(AddLogEntryDelegate, System.Windows.Threading.DispatcherPriority.Input, le);
    }
    private void AddLogEntry(LogEntry en) {
      int hCnt=LogCollection.Count-_halfLength;
      if(hCnt>0 && LogCollection[hCnt].ll==LogLevel.Debug) {
        LogCollection.RemoveAt(hCnt);
      }
      if(LogCollection.Count>_halfLength*2) {
        LogCollection.RemoveAt(0);
      }
      int idx=LogCollection.Count;
      while(idx>0 && LogCollection[idx-1].dt>en.dt) {
        idx--;
      }
      if(idx>1 
        && LogCollection[idx-1].ll==en.ll && LogCollection[idx-1]._msg==en._msg 
        && LogCollection[idx-2].ll==en.ll && LogCollection[idx-2]._msg==en._msg) {
        en.cnt=LogCollection[idx-1].cnt+1;
        LogCollection[idx-1]=en;
      } else {
        LogCollection.Insert(idx, en);
      }

      if((_showDebug || en.ll!=LogLevel.Debug) && lvLog.Items.Count>1 && !LogPanel.IsFocused) {
        if(lvLog.SelectedItem==null) {
          lvLog.SelectedItem = lvLog.Items.GetItemAt(lvLog.Items.Count - 1);
          lvLog.ScrollIntoView(lvLog.SelectedItem);
          lvLog.SelectedItem=null;
        }
      }
    }

    public class LogEntry {
      internal string _msg;
      public LogEntry(string p) {
        int idx=p.IndexOf('[');
        try {
          if(idx<10) {
            return;
          }
          dt=DateTime.Parse(p.Substring(3, idx-3));
          int day;
          if(int.TryParse(p.Substring(0, 2), out day)) {
            day-=dt.Day;
            if(day>0) {
              dt=dt.AddMonths(-1).AddDays(day);
            } else {
              dt=dt.AddDays(day);
            }
          }
          switch(p[idx+1]) {
          case 'D':
            ll=LogLevel.Debug;
            break;
          case 'I':
            ll=LogLevel.Info;
            break;
          case 'W':
            ll=LogLevel.Warning;
            break;
          default:
            ll=LogLevel.Error;
            break;
          }
        }
        catch(Exception ex) {
          Log.Warning("LogEntry.ctor({0}) - {1}", p, ex.Message);
        }
        msg=p.Substring(idx+4);
        local=false;
      }
      public LogEntry(DateTime time, LogLevel logLevel, string text) {
        this.dt = time;
        this.ll = logLevel;
        this.msg = text;
        this.local=true;
      }

      public DateTime dt { get; private set; }
      public LogLevel ll { get; private set; }
      public string msg { get { return cnt==0?_msg: string.Format("{0} [{1}]", _msg, cnt); } private set { _msg=value; } }
      public bool local { get; private set; }
      public int cnt;
    }

    private void DockableContent_MouseLeave(object sender, MouseEventArgs e) {
      lvLog.SelectedItem=null;
    }

    private void ToggleButton_Changed(object sender, RoutedEventArgs e) {

      _showDebug=tbShowDebug.IsChecked==true;  // bool? => bool
      Settings.LogShowDebug=_showDebug;
      if(_showDebug) {
        (lvLog.ItemsSource as System.ComponentModel.ICollectionView).Filter=(o) => true;
      } else {
        (lvLog.ItemsSource as System.ComponentModel.ICollectionView).Filter=(o) => (o as LogEntry).ll!=LogLevel.Debug;
      }
    }
  }

  internal class GridColumnSpringConverter : IMultiValueConverter {
    public object Convert(object[] values, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
      return values.Cast<double>().Aggregate((x, y) => x -= y) - 26;
    }
    public object[] ConvertBack(object value, System.Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) {
      throw new System.NotImplementedException();
    }
  }
}
