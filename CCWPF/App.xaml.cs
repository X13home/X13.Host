#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using X13.PLC;
using X13.WOUM;

namespace X13.CC {
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application {
    internal static MainWindow mainWindow { get; set; }
    internal static DVar<PiLogram> currentLogram { get; set; }
    internal static void OpenLogram(DVar<PiLogram> doc) {
      var c=mainWindow.dockManager.Documents.Where(z => z is LogramView).Cast<LogramView>().FirstOrDefault(z => z.model==doc);
      if(c==null) {
        c=new LogramView(ExConverter.String2Name(doc.path));
      }
      c.Show(mainWindow.dockManager);
      c.Activate();
      
    }
    internal static void CloseLogram(DVar<PiLogram> doc) {
      var c=mainWindow.dockManager.Documents.Where(z => z is LogramView).Cast<LogramView>().FirstOrDefault(z => z.model==doc);
      if(c!=null) {
        c.Close();
      }
    }
    //Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
    //AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

    public App() {
    }
  }
}
