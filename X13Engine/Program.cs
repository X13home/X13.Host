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
using System.ServiceProcess;
using System.Text;
using System.Configuration.Install;
using System.Reflection;
using System.IO;
using CSWindowsServiceRecoveryProperty;
using System.Diagnostics;
using X13.Svc;

namespace X13 {
  static class Program {
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    static void Main(string[] args) {
      if(args.Length>0) {
        string name=Assembly.GetExecutingAssembly().Location;

        if(args[0]=="/i") {
          try {
            string[] args_i=new string[] { name, "/LogFile=..\\log\\install.log" };
            ManagedInstallerClass.InstallHelper(args_i);
            Log.Info("The service installed");

            List<SC_ACTION> FailureActions = new List<SC_ACTION>();

            // First Failure Actions and Delay (msec).
            FailureActions.Add(new SC_ACTION() {
              Type = (int)SC_ACTION_TYPE.RestartService,
              Delay = 1000 * 15
            });

            // Second Failure Actions and Delay (msec).
            FailureActions.Add(new SC_ACTION() {
              Type = (int)SC_ACTION_TYPE.RestartService,
              Delay = 1000 * 60 * 2
            });

            // Subsequent Failures Actions and Delay (msec).
            FailureActions.Add(new SC_ACTION() {
              Type = (int)SC_ACTION_TYPE.None,
              Delay = 1000 * 60 * 3
            });

            // Configure service recovery property.
            ServiceRecoveryProperty.ChangeRecoveryProperty("X13Engine", FailureActions, 60 * 60 * 24, "", false, "");
            Log.Info("The service recovery property is modified successfully");
          }
          catch(Exception ex) {
            Log.Error(ex.Message);
          }
          try {
            ServiceController svc =  new ServiceController("X13Engine");
            svc.Start();
            svc.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 3));
          }
          catch(Exception ex) {
            Log.Error(ex.Message);
          }

          return;
        }
        if(args[0]=="/u") {
          try {
            string[] args_i=new string[] { "/u", name, "/LogFile=..\\log\\uninstall.log" };
            ManagedInstallerClass.InstallHelper(args_i);
          }
          catch(Exception ex) {
            Log.Error(ex.Message);
          }
          return;
        }
        if(args[0]=="/c") {
          CSWindowsServiceRecoveryProperty.Win32.AttachConsole(-1); // ATTACH_PARENT_PROCESS = -1;
          var svc=new X13Svc();
          svc.StartUp();
          Console.WriteLine("System running; press ENTER to stop");
          Console.ReadKey();
          svc.Shutdown();
          return;
        }
      }

      ServiceBase[] ServicesToRun;
      ServicesToRun = new ServiceBase[] 
			{ 
				new X13Svc() 
			};
      ServiceBase.Run(ServicesToRun);
    }
  }
}
