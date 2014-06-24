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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.ServiceProcess;
using System.Text;

namespace X13.Svc {
  public partial class X13Svc : ServiceBase {
    static void Main(string[] args) {
      if(args.Length>0) {
        if(!Directory.Exists("../log")) {
          Directory.CreateDirectory("../log");
        }
        Log.Write+=new Action<LogLevel, DateTime, string>(Log_Write);
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
            ServiceRecoveryProperty.ChangeRecoveryProperty("X13.Svc", FailureActions, 60 * 60 * 24, "", false, "");
            Log.Info("The service recovery property is modified successfully");
          }
          catch(Exception ex) {
            Log.Error(ex.Message);
          }
          try {
            if(WindowsFirewall.IsEnabled()) {
              WindowsFirewall.AuthorizeProgram("X13Svc", name);
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
        } else if(args[0]=="/u") {
          try {
            string[] args_i=new string[] { "/u", name, "/LogFile=..\\log\\uninstall.log" };
            ManagedInstallerClass.InstallHelper(args_i);
          }
          catch(Exception ex) {
            Log.Error(ex.Message);
          }
          return;
        } else if(args[0]=="/update") {
          Log.Info("update start");
          string[][] list=null;
          try {
#pragma warning disable 0618
            if(Engine.IsLinux) { // mono doesn't ship with any trusted root
              Log.Info("Certificate policy ignory");
              ServicePointManager.CertificatePolicy=new AllowApi();
            }
#pragma warning restore 0618
            string content=null;
            using(WebClient client = new WebClient()) {
              content=client.DownloadString(@"http://github.com/X13home/x13home.github.com/raw/master/Download/versions.csv");
            }
            if(content==null) {
              Log.Warning("update list is empty");
              return;
            }
            List<string[]> tmp=new List<string[]>();
            var sa=content.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            for(int n=1; n<sa.Length; n++) { // [0] - header
              var it=sa[n].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
              if(it!=null && it.Length==4) {
                tmp.Add(it);
              }
            }
            list=tmp.ToArray();
          }
          catch(Exception ex) {
            Log.Error("update get versions.csv - {0}", ex.Message);
            return;
          }
          if(list==null) {
            Log.Warning("update list is empty");
            return;
          }
          Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

          List<Tuple<string, string, string>> task=new List<Tuple<string, string, string>>();
          for(int i=0; i<list.Length; i++) {
            try {
              if(!File.Exists(list[i][0])) {
                continue;
              }
              FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(list[i][0]);
              if(string.Compare(fvi.FileDescription, list[i][1], true)!=0 || Version.Parse(fvi.FileVersion)>=Version.Parse(list[i][2])) {
                continue;
              }
              string tmpfn=Path.GetFileName(Path.GetTempFileName());
              using(WebClient client = new WebClient()) {
                client.DownloadFile(list[i][3], tmpfn);
              }
              try {
                if(!Directory.Exists("bak")) {
                  Directory.CreateDirectory("bak");
                }

                File.Replace(tmpfn, list[i][0], "bak/"+list[i][1]+".bak");
                Log.Info("update {0} version: {1} -> {2}", list[i][0], fvi.FileVersion, list[i][2]);
              }
              catch(FileNotFoundException) {
                Log.Info("update {0}[delayed] version: {1} -> {2}", list[i][0], fvi.FileVersion, list[i][2]);
                task.Add(new Tuple<string, string, string>(tmpfn, list[i][0], "bak/"+list[i][1]+".bak"));
              }
            }
            catch(Exception ex) {
              Log.Warning("update {0} - {1}", list[i][0], ex.Message);
            }
          }
          if(task.Count>0) {
            using(StreamWriter shScript = File.CreateText("upd.sh")) {
              shScript.WriteLine("#!/bin/sh\n");
              shScript.WriteLine("echo\n");
              shScript.WriteLine("echo update kernel\n");
              shScript.WriteLine("sleep 2\n");
              for(int i=0; i<task.Count; i++) {
                shScript.WriteLine("mv {0} {1}\n", task[i].Item2, task[i].Item3);
                shScript.WriteLine("mv {0} {1}\n", task[i].Item1, task[i].Item2);
                shScript.WriteLine("echo {0} updated\n", task[i].Item2);
              }
              shScript.WriteLine("echo update complete. Press any key\n");
              shScript.Flush();
            }
            Log.Info("update[delayed] complete");

            Process shScriptProcess = new Process();
            shScriptProcess.StartInfo = new ProcessStartInfo("/bin/sh", "upd.sh");
            shScriptProcess.Start();
          } else {
            Log.Info("update complete");
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
      string text= string.Format("{0}[{1}]\t{2}", dt.ToString("HH:mm:ss.ff"), ll.ToString(), msg);
      string fn="../log/"+dt.ToString("yyMMdd")+".log";
      Console.WriteLine(text);
      try {
        File.AppendAllText(fn, text+"\r\n");
      }
      catch(Exception) {
      }
    }
    private class AllowApi : ICertificatePolicy {
      public bool CheckValidationResult(ServicePoint srvPoint, System.Security.Cryptography.X509Certificates.X509Certificate certificate, WebRequest request, int error) {
        if(error == 0)
          return true;
        // only ask for trust failure (you may want to handle more cases)
        if(error == -2146762486 && request!=null) {
          Log.Info("ignory certificate for {0}", request.RequestUri.Host);
          return true;
        }
        return false;
      }
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
