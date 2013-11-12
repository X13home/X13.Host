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
    private static X13.Plugins _plugins;

    static TopicSrc() {
      try {
        Topic.Import("../data/viewwpf.xst", "/local/cfg");
      }
      catch(Exception ex) {
        Log.Error(ex.ToString());
      }
      _plugins=new Plugins();

      var url=Topic.root.Get<string>("/local/cfg/Client/_URL");
      if(string.IsNullOrEmpty(url.value)) {
        url.saved=true;
        url.value="localhost";
      }
      _plugins.Init(false);
      _cl=_plugins["Client"] as MQTT.MqClient;
      _plugins.Start();
    }
    public static void Disconnect() {
      _plugins.Stop();
    }

    private Topic _model=null;
    public readonly string path;
    public TopicSrc(string path) {
      this.path=path;
      Topic.root.Subscribe(path, Source_changed);
    }
    public object value {
      get {
        object ret=null;

        if(_model!=null && _model.valueType!=null) {
          ret=_model.GetValue();
        }
        return ret;
      }
      set { }
    }
    public void Source_changed(Topic t, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Value) {
        if(_model==null || _model.valueType==null) {
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
