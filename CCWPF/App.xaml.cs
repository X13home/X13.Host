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
using System.Linq;
using System.Windows;
using X13.WOUM;
using X13.PLC;
using System.Reflection;
using System.IO;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

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

#pragma warning disable 169
    [ImportMany(typeof(IPlugModul))]
    private IEnumerable<Lazy<IPlugModul, IPlugModulData>> _modules;
#pragma warning restore 169

    public App() {
      string path=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      var catalog = new AggregateCatalog();
      catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
      catalog.Catalogs.Add(new DirectoryCatalog(path));
      CompositionContainer _container = new CompositionContainer(catalog);
      try {
        _container.ComposeParts(this);
      }
      catch(CompositionException ex) {
        Log.Error("Load plugins - {0}", ex.ToString());
        throw;
      }
    }
  }
}
