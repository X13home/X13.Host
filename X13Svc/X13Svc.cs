#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using CSWindowsServiceRecoveryProperty;
using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;

namespace X13.Svc {
  public partial class X13Svc : ServiceBase {
    static void Main(string[] args) {
      if(args.Length>0) {
        string name=Assembly.GetExecutingAssembly().Location;
        Log.Write+=new Action<LogLevel, DateTime, string>(Log_Write);

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
            ServiceRecoveryProperty.ChangeRecoveryProperty("X13.Svc", FailureActions, 60 * 60 * 24, "", false, "");
            Log.Info("The service recovery property is modified successfully");
          }
          catch(Exception ex) {
            Log.Error(ex.Message);
          }
          try {
            if(WindowsFirewall.IsEnabled()) {
              WindowsFirewall.AuthorizeProgram("X13Engine", name);
              Log.Info("Windows Firewall configuriert for {0}", name);
            } else {
              Log.Info("Windows Firewall disabled");
            }
          }
          catch(Exception ex) {
            Log.Error(ex.Message);
          }
          try {
            ServiceController svc =  new ServiceController("X13.Svc");
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
      } else {
        ServiceBase[] ServicesToRun;
        ServicesToRun = new ServiceBase[] 
			{ 
				new X13Svc() 
			};
        ServiceBase.Run(ServicesToRun);
        if(Engine.IsLinux) {
          System.Threading.Thread.Sleep(5000);   // for mono-service 
        }
      }
    }
    private static void Log_Write(LogLevel ll, DateTime dt, string msg) {
      Console.WriteLine("{0}", msg);
    }

    private X13.Engine _eng;
    public X13Svc() {
      InitializeComponent();
    }

    protected override void OnStart(string[] args) {
      _eng=new Engine();
      _eng.StartUp();
    }
    protected override void OnStop() {
      _eng.Shutdown();
    }
  }
}
