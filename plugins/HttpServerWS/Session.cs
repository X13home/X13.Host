using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13.Plugins {
  internal class Session : IDisposable {
    private static List<WeakReference> sessions;
    private static DVar<bool> _verbose;

    static Session() {
      sessions=new List<WeakReference>();
      _verbose=Topic.root.Get<bool>("/etc/HttpServer/_verbose");
    }
    public static Session Get(string sid, System.Net.IPEndPoint ep, bool create=true) {
      Session s;
      if(string.IsNullOrEmpty(sid) || (s=sessions.Where(z => z.IsAlive).Select(z => z.Target as Session).FirstOrDefault(z => z!=null && z.id==sid && z.ip.Equals(ep.Address)))==null) {
        if(create) {
          s=new Session(ep);
          sessions.Add(new WeakReference(s));
        } else {
          s=null;
        }
      }
      return s;
    }

    private Session(System.Net.IPEndPoint ep) {
      Topic r=Topic.root.Get("/etc/HttpServer/clients");
      this.id = Guid.NewGuid().ToString();
      this.ip = ep.Address;
      int i=1;
      string pre=ip.ToString();
      while(r.Exist(pre+i.ToString())) {
        i++;
      }
      _owner=r.Get<string>(pre+i.ToString());
      owner.saved=false;
      try {
        var he=System.Net.Dns.GetHostEntry(this.ip);
        _host=string.Format("{0}[{1}]", he.HostName, this.ip.ToString());
        var tmp=he.HostName.Split('.');
        if(tmp!=null && tmp.Length>0 && !string.IsNullOrEmpty(tmp[0])) {
          i=1;
          while(r.Exist(tmp[0]+"-"+i.ToString())) {
            i++;
          }
          _owner.Move(r, tmp[0]+"-"+i.ToString());
        }
      }
      catch(Exception) {
        _host=string.Format("[{0}]", this.ip.ToString());
      }
      this.owner.value=_host;
      if(_verbose.value) {
        Log.Info("{0} session[{2}] - {1}", owner.name, this._host, this.id);
      }
    }
    private string _host;
    private DVar<string> _owner;
    public readonly string id;
    public readonly System.Net.IPAddress ip;
    public string userName;
    public DVar<string> owner { get { return _owner; } }
    public void Close() {
      sessions.RemoveAll(z => !z.IsAlive || z.Target==this);
      Dispose();
    }
    public override string ToString() {
      return (string.IsNullOrEmpty(userName)?"anonymus":userName)+"@"+_host;
    }
    public void Dispose() {
      var o=Interlocked.Exchange(ref _owner, null);
      if(o!=null && !o.disposed) {
        o.Remove();
      }
    }
  }
}
