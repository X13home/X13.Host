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

    static TopicSrc() {
      try {
        Topic.Import("../data/viewwpf.xst", "/local/cfg");
      }
      catch(Exception ex) {
        Log.Error(ex.ToString());
      }
      _cl=new MqClient();

      var myId=Topic.root.Get<string>("/local/cfg/id");
      if(string.IsNullOrWhiteSpace(myId.value)) {
        myId.saved=true;
        myId.value=string.Format("{0}_view@{1}", Environment.UserName, Environment.MachineName);
      }
      var url=Topic.root.Get<string>("/local/cfg/Client/_URL");
      if(string.IsNullOrEmpty(url.value)) {
        url.value="localhost";
      }
      _cl.Start();
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
