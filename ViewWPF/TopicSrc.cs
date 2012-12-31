using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading;
using X13.PLC;
using X13.MQTT;

namespace X13.View {
  internal class TopicSrc : INotifyPropertyChanged {
    private static MqClient _cl;
    private static List<TopicSrc> _subs;
    private static bool _connected;
    private static Timer _checkConnectTimer;

    static TopicSrc() {
      try {
        Topic.Import("viewwpf.cfg", "/local/settings");
      }
      catch(Exception ex) {
        Log.Error(ex.ToString());
      }
      _checkConnectTimer=new Timer(CheckConnect, null, 20000, 900000);
      _cl=new MqClient(StatusChanged);
      Topic brokerSettings=Topic.root.Get("/local/settings/Broker");

      #region Load Security
      string securPath=brokerSettings.Get<string>("_path");
      if(string.IsNullOrEmpty(securPath)) {
        securPath=@"..\data\security.dat";
      } else {
        securPath=System.IO.Path.Combine(securPath, @"..\data\security.dat");
      }
      Topic.Import(securPath, "/local/security");

      _cl.UserName=brokerSettings.Get<string>("_username");
      brokerSettings.Get<string>("_username").saved=true;
      _cl.UserPass=brokerSettings.Get<string>("_password");
      brokerSettings.Get<string>("_password").saved=true;
      if(string.IsNullOrEmpty(_cl.UserName)) {
        Topic pt;
        if(Topic.root.Exist("/local/security/users/root", out pt)) {
          _cl.UserName="root";
          _cl.UserPass=(pt as DVar<string>).value;
        }
      }
      #endregion Load security

      string brokerUrl=brokerSettings.Get<string>("_URL").value;
      if(string.IsNullOrWhiteSpace(brokerUrl)) {
        brokerUrl="localhost";
        brokerSettings.Get<string>("_URL").saved=true;
        brokerSettings.Get<string>("_URL").value=brokerUrl;
      }
      _cl.Connect(brokerUrl);
      _subs=new List<TopicSrc>();
      Topic.root.Subscribe("/#", root_changed);
    }

    private static void root_changed(Topic t, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Add) {
        var tr=_subs.FirstOrDefault(z => z.path==t.path);
        if(tr!=null) {
          _subs.Remove(tr);
          t.changed+=tr.Source_changed;
        }
      }
    }
    private static void CheckConnect(object o) {
      if(!_connected && _cl!=null) {
        ThreadPool.QueueUserWorkItem((r) => {
          Thread.Sleep(5000);
          _cl.Connect(Topic.root.Get<string>("/local/settings/Broker/_URL").value);
        });
      }
    }
    private static void StatusChanged(bool st) {
      _connected=st;
      if(st) {
        lock(_subs) {
          foreach(var s in _subs) {
            SendSubscribe(s);
          }
        }
      //} else {
      //  ThreadPool.QueueUserWorkItem((o) => {
      //    Thread.Sleep(5000);
      //    _cl.Connect(Topic.root.Get<string>("/local/settings/Broker/_URL").value);
      //  });
      }
    }

    private static void SendSubscribe(TopicSrc s) {
      if(!s.path.StartsWith("/local")) {
        _cl.Subscribe(s.path, QoS.AtMostOnce);
      }
    }

    private Topic _model=null;
    public readonly string path;
    public TopicSrc(string path) {
      this.path=path;
      lock(_subs) {
        _subs.Add(this);
      }
      if(_connected) {
        SendSubscribe(this);
      }
    }
    public object value {
      get {
        object ret=null;

        if(_model!=null) {
          ret=_model.GetValue();
        }
        return ret;
      }
      set { }
    }
    public void Source_changed(Topic t, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Value) {
        if(_model==null) {
          _model=t;
        }
        if(PropertyChanged!=null) {
          PropertyChanged(this, new PropertyChangedEventArgs("value"));
        }
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;
  }
}
