using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace X13 {
  internal class ItemViewModel : ViewModelBase {
    private static char[] _delmiter=new char[] { '/' };
    private static Jurassic.ScriptEngine _engine;
    public static readonly ItemViewModel root;

    static ItemViewModel() {
      _engine=new Jurassic.ScriptEngine();
      root=new ItemViewModel(null, "/") { posX=0, posY=0, sizeX=25, sizeY=20, view=Projection.LO };
    }

    private string _name;
    private ObservableCollection<ItemViewModel> _children;
    private ItemViewModel _parent;
    private object _obj;

    private ItemViewModel(ItemViewModel parent, string name) {
      _name=name;
      _parent=parent;
      if(name!="Alpha") {
        _obj=Jurassic.Library.JSONObject.Parse(_engine, "{ \"A\": 5, \"B\": 13.4, \"C\": { \"CA\" : \"test\", \"CB\" : null } }");
      } else {
        _obj=(DateTime.Now.Ticks&0x7FFF)/100.0;
      }
    }

    public IEnumerable<ItemViewModel> children {
      get {
        if(_children==null) {
          _children=new ObservableCollection<ItemViewModel>();
          _children.Add(new ItemViewModel(this, "Alpha") { posX=5, posY=3, sizeX=25, sizeY=20, view=Projection.LO, json="1" });
          _children.Add(new ItemViewModel(this, "Beta") { posX=15, posY=3, sizeX=25, sizeY=20, view=Projection.IN, json="0x97" });
          if(_name!="Delta") {
            _children.Add(new ItemViewModel(this, "Gamma") { posX=5, posY=12, sizeX=25, sizeY=20, view=Projection.LO, json="Hello "+_name+"!!!" });
          }
          _children.Add(new ItemViewModel(this, "Delta") { posX=15, posY=12, sizeX=25, sizeY=20, view=Projection.LO, json=null });
        }
        return _children;
      }
    }
    
    public IEnumerable<Jurassic.Library.PropertyNameAndValue> Properties {
      get {
        var ob=_obj as Jurassic.Library.ObjectInstance;
        return ob==null?null:ob.Properties;
      }
    }

    public string Name { get { return _name; } }
    public object Value { get { return _obj; } set { _obj=value; } }
    public string path { get { return _parent==null?"/":(_parent==root?"/"+_name:_parent.path+"/"+_name); } }
    public string contentId { get { return view.ToString()+":"+path; } }
    public Projection view { get; set; }
    public string json { get; set; }
    public int posX { get; set; }
    public int posY { get; set; }
    public int sizeX { get; set; }
    public int sizeY { get; set; }

    internal ItemViewModel Get(string p) {
      ItemViewModel cur;
      ItemViewModel next=null;
      if(!string.IsNullOrEmpty(p) && p.StartsWith("/")) {
        cur=root;
      } else {
        cur=this;
      }
      if(string.IsNullOrEmpty(p)) {
        return cur;
      }
      string[] pe=p.Split(_delmiter, StringSplitOptions.RemoveEmptyEntries);
      for(int i=0; i<pe.Length; i++, cur=next) {
        next=cur.children.FirstOrDefault(z => z._name==pe[i]);    // create & fill if null
        bool chExist=next!=null;
        if(!chExist) {
          lock(cur) {
            next=cur._children.FirstOrDefault(z => z._name==pe[i]);
            chExist=next!=null;
            if(!chExist) {
              if(pe[i]=="+" || pe[i]=="#") {
                throw new ArgumentException("path ("+path+") is not valid");
              }
              next=new ItemViewModel(cur, pe[i]);
              cur._children.Add(next);
            }
          }
        }
      }
      return cur;
    }
  }
  public enum Projection {
    /// <summary>InspectorView</summary>
    IN,
    /// <summary>LogramView</summary>
    LO,
    /// <summary>SourceView</summary>
    SO
  }
}
