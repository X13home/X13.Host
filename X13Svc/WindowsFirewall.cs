using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.Svc {
  internal class WindowsFirewall {
    private static NetFwTypeLib.INetFwMgr WinFirewallManager() {
      Type type = Type.GetTypeFromProgID("HNetCfg.FwMgr", false);
      return Activator.CreateInstance(type) as NetFwTypeLib.INetFwMgr;
    }
    public static bool IsEnabled() {
      NetFwTypeLib.INetFwMgr mgr = WinFirewallManager();
      return mgr.LocalPolicy.CurrentProfile.FirewallEnabled;
    }
    public static bool IsAutorized(string path) {
      NetFwTypeLib.INetFwAuthorizedApplications applications; 
      //INetFwAuthorizedApplication application;

      NetFwTypeLib.INetFwMgr mgr = WinFirewallManager();
      applications = (NetFwTypeLib.INetFwAuthorizedApplications)mgr.LocalPolicy.CurrentProfile.AuthorizedApplications;

      foreach(NetFwTypeLib.INetFwAuthorizedApplication application in applications) {
        if(application.Enabled && application.ProcessImageFileName==path) {
          return true;
        }
      }
      return false;
    }
    public static bool AuthorizeProgram(string title, string path) {
      Type type = Type.GetTypeFromProgID("HNetCfg.FwAuthorizedApplication");
      NetFwTypeLib.INetFwAuthorizedApplication authapp = Activator.CreateInstance(type)
          as NetFwTypeLib.INetFwAuthorizedApplication;
      authapp.Name = title;
      authapp.ProcessImageFileName = path;
      authapp.Scope = NetFwTypeLib.NET_FW_SCOPE_.NET_FW_SCOPE_LOCAL_SUBNET;
      //authapp.IpVersion = NET_FW_IP_VERSION_.NET_FW_IP_VERSION_V4;
      authapp.Enabled = true;

      NetFwTypeLib.INetFwMgr mgr = WinFirewallManager();
      try {
        mgr.LocalPolicy.CurrentProfile.AuthorizedApplications.Add(authapp);
      }
      catch(Exception ex) {
        System.Diagnostics.Trace.Write(ex.Message);
        return false;
      }
      return true;
    }
  }
}
