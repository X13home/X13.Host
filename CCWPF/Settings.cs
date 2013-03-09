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
using System.Xml.Linq;
using System.IO;
using X13.PLC;

namespace X13.CC {
  internal class Settings {
    private static Topic _settings;

    static Settings() {
      _settings=Topic.root.Get("/local/settings");
    }

    public static bool LogShowDebug {
      get { return Topic.root.Get<bool>("/system/CC/Log/_ShowDebug").value; }
      set {
        Topic.root.Get<bool>("/system/CC/Log/_ShowDebug").saved=true;
        Topic.root.Get<bool>("/system/CC/Log/_ShowDebug").value=value;
      }
    }

    public static System.Windows.WindowState MainWindowState {
      get { return (System.Windows.WindowState)_settings.Get<long>("MainWindow/_State").value; }
      set {
        _settings.Get<long>("MainWindow/_State").saved=true;
        _settings.Get<long>("MainWindow/_State").value=(long)value; 
      }
    }
    public static int MainWindowTop {
      get { return (int)_settings.Get<long>("MainWindow/_Top").value; }
      set {
        var dv=_settings.Get<long>("MainWindow/_Top");
        dv.saved=true;
        dv.value=value; 
      }
    }
    public static int MainWindowLeft {
      get { return (int)_settings.Get<long>("MainWindow/_Left").value; }
      set {
        var dv=_settings.Get<long>("MainWindow/_Left");
        dv.saved=true;
        dv.value=value;
      }
    }
    public static int MainWindowHeight {
      get { return (int)_settings.Get<long>("MainWindow/_Height").value; }
      set {
        var dv=_settings.Get<long>("MainWindow/_Height");
        dv.saved=true;
        dv.value=value; 
      }
    }
    public static int MainWindowWidth {
      get { return (int)_settings.Get<long>("MainWindow/_Width").value; }
      set {
        var dv=_settings.Get<long>("MainWindow/_Width");
        dv.saved=true;   
        dv.value=value; 
      }
    }
    public static byte[] Layout {
      get {
        var dv=Topic.root.Get<string>("/system/CC/MainWindow/_layout");
        return string.IsNullOrEmpty(dv.value)?null:Convert.FromBase64String(dv.value); }
      set {
        var dv=Topic.root.Get<string>("/system/CC/MainWindow/_layout");
        dv.saved=true;
        dv.value=Convert.ToBase64String(value);
      }
    }
  }
}
