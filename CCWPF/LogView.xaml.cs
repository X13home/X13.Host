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

    public LogView() {
      _instance=this;
      AddLogEntryDelegate=new Action<LogEntry>(AddLogEntry);

      InitializeComponent();
      this.DataContext = this;
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
      if(Topic.root.Get<string>("/local/settings/Broker/_URL").value!="#local") {
        #region init filewatcher
        string brPath=Topic.root.Get<string>("/local/settings/Broker/_path");
        if(!string.IsNullOrEmpty(brPath)) {
          brPath=System.IO.Path.Combine(brPath, "..\\Log\\");
        } else {
          brPath="..\\Log\\";
        }
        if(Directory.Exists(brPath)) {
          _endPosition=0;
          var files=Directory.GetFiles(brPath, "*.log", SearchOption.TopDirectoryOnly).ToList();
          DateTime td=DateTime.Today;
          foreach(string f in files) {
            if(File.GetLastWriteTime(f)>td) {
              _brokerCurLog=f;
              break;
            }
          }
          if(!string.IsNullOrEmpty(_brokerCurLog)) {
            _fsWatcher=new FileSystemWatcher(System.IO.Path.GetDirectoryName(_brokerCurLog), "*.log");
            _fsWatcher.NotifyFilter=NotifyFilters.LastWrite;
            _fsWatcher.Changed+=new FileSystemEventHandler(_fsWatcher_Changed);
            _fsWatcher.Created+=new FileSystemEventHandler(_fsWatcher_Created);
            _fsWatcher.EnableRaisingEvents=true;
            _fsWatcher_Changed(null, new FileSystemEventArgs(WatcherChangeTypes.Changed, System.IO.Path.GetDirectoryName(_brokerCurLog), System.IO.Path.GetFileName(_brokerCurLog)));
          }
        }

        #endregion init filewatcher
      }
    }

    private void Log_Write(LogLevel logLevel, DateTime time, string text) {
      Log_Write(new LogEntry(time, logLevel, text));
    }

    private void Log_Write(LogEntry le) {
      Dispatcher.BeginInvoke(AddLogEntryDelegate, System.Windows.Threading.DispatcherPriority.ApplicationIdle, le);
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
      LogCollection.Insert(idx, en);

      if((_showDebug || en.ll!=LogLevel.Debug) && lvLog.Items.Count>1 && !LogPanel.IsFocused) {
        if(lvLog.SelectedItem==null) {
          lvLog.SelectedItem = lvLog.Items.GetItemAt(lvLog.Items.Count - 1);
          lvLog.ScrollIntoView(lvLog.SelectedItem);
          lvLog.SelectedItem=null;
        }
      }
    }

    public class LogEntry {
      public LogEntry(string p) {
        int idx=p.IndexOf('[');
        try {
          if(idx<7) {
            return;
          }
          dt=DateTime.Parse(p.Substring(0, idx));
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
        message=p.Substring(idx+4);
        local=false;
      }
      public LogEntry(DateTime time, LogLevel logLevel, string text) {
        this.dt = time;
        this.ll = logLevel;
        this.message = text;
        this.local=true;
      }

      public DateTime dt { get; private set; }
      public LogLevel ll { get; private set; }
      public string message { get; private set; }
      public bool local { get; private set; }
    }

    #region FileWatcher
    private FileSystemWatcher _fsWatcher;
    private string _brokerCurLog;
    private long _endPosition;
    private int _ln_state=0;

    private void _fsWatcher_Changed(object sender, FileSystemEventArgs e) {
      if(e.Name!= System.IO.Path.GetFileName(_brokerCurLog)) {
        return;
      }
      try {
        using(FileStream fs=File.Open(_brokerCurLog, FileMode.Open, FileAccess.Read, FileShare.Read)) {
          fs.Seek(_endPosition, SeekOrigin.Begin);
          List<byte> arr=new List<byte>(128);
          int b;
          while((b=fs.ReadByte())>0) {
            if((b==0x0D || b==0x0A) && (_ln_state==0 || _ln_state==1)) {
              _ln_state++;
              if(_ln_state==2) {
                _ln_state=0;
                b=fs.ReadByte();
                if(b==' ' || b=='\t') {
                  arr.Add(0x0D);
                  arr.Add(0x0A);
                } else {
                  Log_Write(new LogEntry(Encoding.UTF8.GetString(arr.ToArray())));
                  arr.Clear();
                  if(b==-1) {
                    continue;
                  }
                }
              } else {
                continue;
              }
            }
            arr.Add((byte)b);
          }
          _endPosition=fs.Position;
        }
      }
      catch(IOException) {
      }
      catch(Exception ex) {
        Log.Error("LogWatcher.ctor file={1} \nException :{0}", ex.Message, _brokerCurLog);
      }
    }
    private void _fsWatcher_Created(object sender, FileSystemEventArgs e) {
      DateTime td=DateTime.Today;
      if(System.IO.Path.GetFileName(_brokerCurLog)!=e.Name && File.GetLastWriteTime(e.FullPath)>td) {
        _brokerCurLog=e.FullPath;
      }
      _endPosition=0;
    }
    #endregion

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
