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
      get { return _settings.Get<bool>("Log/_ShowDebug").value; }
      set {
        _settings.Get<bool>("Log/_ShowDebug").saved=true;
        _settings.Get<bool>("Log/_ShowDebug").value=value;
      }
    }

    public static System.Windows.WindowState MainWindowState {
      get { return (System.Windows.WindowState)_settings.Get<int>("MainWindow/_State").value; }
      set {
        _settings.Get<int>("MainWindow/_State").saved=true;
        _settings.Get<int>("MainWindow/_State").value=(int)value; 
      }
    }
    public static int MainWindowTop {
      get { return _settings.Get<int>("MainWindow/_Top").value; }
      set {
        _settings.Get<int>("MainWindow/_Top").saved=true;
        _settings.Get<int>("MainWindow/_Top").value=value; 
      }
    }
    public static int MainWindowLeft {
      get { return _settings.Get<int>("MainWindow/_Left").value; }
      set {
        _settings.Get<int>("MainWindow/_Left").saved=true;
        _settings.Get<int>("MainWindow/_Left").value=value;
      }
    }
    public static int MainWindowHeight {
      get { return _settings.Get<int>("MainWindow/_Height").value; }
      set {
        _settings.Get<int>("MainWindow/_Height").saved=true;
        _settings.Get<int>("MainWindow/_Height").value=value; 
      }
    }
    public static int MainWindowWidth {
      get { return _settings.Get<int>("MainWindow/_Width").value; }
      set {
        _settings.Get<int>("MainWindow/_Width").saved=true;   
        _settings.Get<int>("MainWindow/_Width").value=value; 
      }
    }
    public static byte[] Layout {
      get { return _settings.Get<byte[]>("MainWindow/_layout").value; }
      set {
        _settings.Get<byte[]>("MainWindow/_layout").saved=true;
        _settings.Get<byte[]>("MainWindow/_layout").value=value;
      }
    }
  }
}
