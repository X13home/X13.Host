using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.PLC {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class PiTracer : ITopicOwned {
    private DVar<PiTracer> _owner;
    private Topic _val;

    [Newtonsoft.Json.JsonProperty]
    public readonly string path;

    public PiTracer(string path) {
      this.path=path;
    }

    public void SetOwner(Topic owner) {
      if(string.IsNullOrEmpty(path) || !Topic.root.Exist(path, out _val)) {
        _val=null;
      }
      _owner=owner as DVar<PiTracer>;
      if(_val!=null) {
        if(_owner==null) {
          _val.changed-=val_changed;
        } else {
          _val.changed+=val_changed;
        }
      }
    }

    private void val_changed(Topic snd, TopicChanged p) {
      if(p.Art==TopicChanged.ChangeArt.Remove && _owner!=null) {
        _owner.Remove();
      }
    }
    public override string ToString() {
      object o;
      if(_val!=null && (o=_val.GetValue())!=null) {
        return string.Format("{0}={1}", _val.path, o.ToString());
      }
      return _val==null?string.Empty:(_val.path+"=null");
    }
  }
}
