#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AvalonDock;
using X13.PLC;

namespace X13.CC {
  /// <summary>
  /// Interaction logic for DataStorageView.xaml
  /// </summary>
  public partial class DataStorageView : DockableContent {

    public DataStorageView() {
      InitializeComponent();
      this.DataContext = this;

      Topic _root=Topic.root;
      _root.Subscribe("/#", _root_changed);
      {
        var ro_ch=TopicView.root.children;
        var tv1=TopicView.root.Get(_root.Get("/local"));
        var tv1_ch=tv1.children;
        tv1.Get(_root.Get("/local/settings"));
      }
      TopicView.root.IsExpanded=true;
      RootNodes=new ObservableCollection<TopicView>();
      RootNodes.Add(TopicView.root);
    }

    public ObservableCollection<TopicView> RootNodes {
      get { return (ObservableCollection<TopicView>)GetValue(rootNodes); }
      set { SetValue(rootNodes, value); }
    }

    public static readonly DependencyProperty rootNodes =
            DependencyProperty.Register("RootNodes", typeof(ObservableCollection<TopicView>), typeof(DataStorageView), new UIPropertyMetadata(null));

    public Topic Selected {
      get {
        TopicView s=tvDataStorage.SelectedItem as TopicView;
        return s!=null?s.ptr:Topic.root;
      }
    }

    private void _root_changed(Topic sender, TopicChanged param) {
      this.Dispatcher.BeginInvoke(new Action<Topic, TopicChanged.ChangeArt>(ProccessChanges), System.Windows.Threading.DispatcherPriority.Background, sender, param.Art);
    }
    private void ProccessChanges(Topic oCur, TopicChanged.ChangeArt art) {
      TopicView parent=TopicView.root.Get(oCur.parent, true, art!=TopicChanged.ChangeArt.Remove);
      if(parent!=null) {
        if(oCur.name==("_declarer") && art==TopicChanged.ChangeArt.Value) {
          parent.AttrChanged(oCur);
        } else {
          TopicView cur=parent.Get(oCur, false, art!=TopicChanged.ChangeArt.Remove);
          if(cur!=null) {
            if(art==TopicChanged.ChangeArt.Add) {
              cur.OnPropertyChanged("name");
            } else if(art==TopicChanged.ChangeArt.Remove) {
              parent.children.Remove(cur);
            }
          }
        }
      }
    }
    private void tvDataStorage_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
      TopicView s=tvDataStorage.SelectedItem as TopicView;
      if(s!=null) {
        PropertyView.Selected=s.ptr;
      }
    }

    private void StackPanel_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
      StackPanel p;
      TopicView cur;
      if((p=sender as StackPanel)!=null && (cur=p.DataContext as TopicView)!=null) {
        PropertyView.Selected=cur.ptr;
        var actions=cur.GetActions();
        p.ContextMenu.Items.Clear();

        ItemCollection items;
        for(int i=0; i<actions.Count; i++) {
          items=p.ContextMenu.Items;
          string[] lvls=actions[i].menuItem.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
          for(int j=0; j<lvls.Length; j++) {
            MenuItem mi = FindMenuItem(items, lvls[j]);
            if(mi==null) {
              mi=new MenuItem();
              mi.Header=lvls[j];
              mi.DataContext=sender;
              items.Add(mi);
            }

            if(j==lvls.Length-1) {
              mi.Tag=actions[i];
              mi.Click+=new RoutedEventHandler(mi_Click);
              mi.ToolTip=actions[i].description;
            }
            items=mi.Items;
          }
        }

        if(p.ContextMenu.Items.Count==0) {
          e.Handled=true;
        }
      } else {
        e.Handled=true;
      }
    }
    internal static MenuItem FindMenuItem(ItemCollection items, string name) {
      MenuItem rez=null;

      foreach(var cur in items) {
        rez=cur as MenuItem;
        if(rez!=null && (rez.Header as string)==name) {
          return rez;
        }
      }
      return null;
    }
    private void mi_Click(object sender, RoutedEventArgs e) {
      e.Handled=true;
      MenuItem ci=sender as MenuItem;
      if(ci==null) {
        return;
      }
      Topic cur=((ci.DataContext as StackPanel).DataContext as TopicView).ptr;
      switch(((TopicView.ItemActionStr)ci.Tag).action) {
      case ItemAction.createNodeMask:
      case ItemAction.createBoolMask:
      case ItemAction.createLongMask:
      case ItemAction.createDoubleMask:
      case ItemAction.createStringMask:
        AddItem(ci.DataContext as StackPanel, ((TopicView.ItemActionStr)ci.Tag).action);
        break;
      case ItemAction.createBoolDef:
        cur.Get<bool>(ci.Header as string);
        break;
      case ItemAction.createLongDef:
        cur.Get<long>(ci.Header as string);
        break;
      case ItemAction.createDoubleDef:
        cur.Get<double>(ci.Header as string);
        break;
      case ItemAction.createStringDef:
        cur.Get<string>(ci.Header as string);
        break;
      case ItemAction.createObjectDef:
        cur.Get<object>(ci.Header as string);
        break;
      case ItemAction.open:
        if(cur!=null && cur.valueType==typeof(PiLogram)) {
          App.OpenLogram(cur as DVar<PiLogram>);
        }
        break;
      case ItemAction.addToLogram:
        AddToLogram(cur);
        break;
      case ItemAction.remove:
        if(cur!=null && MessageBox.Show(string.Format("Remove {0}[{1}]", cur.path, cur.valueType!=null?cur.valueType.Name:"Topic"), "Remove item", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No, MessageBoxOptions.RightAlign)==MessageBoxResult.Yes) {
          if(cur.valueType==typeof(PiLogram)) {
            App.CloseLogram(cur as DVar<PiLogram>);
          }
          cur.Remove();
        }
        break;
      }
    }
    private void tvDataStorage_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
      TreeView p;
      TopicView cur;
      if((p=sender as TreeView)!=null && p.Items.Count>0 && (cur=p.SelectedItem as TopicView)!=null) {
        PropertyView.Selected=cur.ptr;
        var actions=cur.GetActions();
        if(cur!=null && cur.ptr!=null && cur.ptr.valueType==typeof(PiLogram) && actions.Any(z => z.action==ItemAction.open)) {
          App.OpenLogram(cur.ptr as DVar<PiLogram>);
        } else if(cur!=null && cur.ptr!=null && actions.Any(z => z.action==ItemAction.addToLogram)) {
          AddToLogram(cur.ptr);
        }
      }
    }
    private void StackPanel_MouseLeave(object sender, MouseEventArgs e) {
      var cur=tvDataStorage.SelectedItem as TopicView;
      if(e.LeftButton==MouseButtonState.Pressed && cur!=null && cur.ptr!=null && cur.GetActions().Any(z => z.action==ItemAction.addToLogram)) {
          DragDrop.DoDragDrop(tvDataStorage, cur.ptr.path, DragDropEffects.Link);
      }
    }


    private static void AddToLogram(Topic cur) {
      if(cur!=null && App.currentLogram!=null) {
        string name=cur.name;
        int i=1;
        while(App.currentLogram.Exist(name)) {
          name=string.Format("{0}_{1}", cur.name, i++);
        }
        var it=App.currentLogram.Get<Topic>(name);
        it.saved=true;
        it.value=cur;
      }
    }
    private void AddItem(StackPanel sp, ItemAction act) {
      TopicView vo=sp.DataContext as TopicView;
      TreeViewItem to=FindAncestorOrSelf<TreeViewItem>(sp);

      if(vo!=null && to!=null) {
        vo.IsExpanded=true;
        TopicView vc=new TopicView(act, vo);
        to.BringIntoView();
      }
    }

    private void TextBox_Loaded(object sender, RoutedEventArgs e) {
      (sender as TextBox).SelectAll();
      (sender as TextBox).Focus();
    }
    private void TextBox_LostFocus(object sender, RoutedEventArgs e) {
      TextBox tb;
      TopicView tv;
      if((tb=sender as TextBox)!=null && (tv=tb.DataContext as TopicView)!=null) {
        tv.Remove();
      }
    }
    private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
      TextBox tb;
      TopicView tv;
      if((tb=sender as TextBox)==null || (tv=tb.DataContext as TopicView)==null) {
        return;
      }
      if(e.Key==Key.Escape) {
        TextBox_LostFocus(sender, null);
        e.Handled=true;
      } else if(e.Key==Key.Enter) {
        tv.Create(tb.Text);
        e.Handled=true;
      }
    }

    public static DependencyObject GetParent(DependencyObject obj) {
      if(obj == null)
        return null;

      ContentElement ce = obj as ContentElement;
      if(ce != null) {
        DependencyObject parent = ContentOperations.GetParent(ce);
        if(parent != null)
          return parent;

        FrameworkContentElement fce = ce as FrameworkContentElement;
        return fce != null ? fce.Parent : null;
      }

      return VisualTreeHelper.GetParent(obj);
    }
    public static T FindAncestorOrSelf<T>(DependencyObject obj)
        where T : DependencyObject {
      while(obj != null) {
        T objTest = obj as T;
        if(objTest != null)
          return objTest;
        obj = GetParent(obj);
      }

      return null;
    }

  }
  public class TopicView : INotifyPropertyChanged {
    public static TopicView root { get; private set; }
    static TopicView() {
      root=new TopicView(Topic.root);
    }
    private ObservableCollection<TopicView> _children;
    private ItemAction _action;
    private TopicView _parent;
    private DVar<string> _declarer;

    private TopicView(Topic origin) {
      this.ptr=origin;
      DefDeclarer();
    }

    private void DefDeclarer() {
      Topic dt;
      if(ptr.Exist("_declarer", out dt)) {
        string dp=(dt as DVar<string>).value;
        Topic ds=Topic.root.Get("/system/declarers");
        if(!string.IsNullOrEmpty(dp) && ds.Exist(dp, out dt)) {
          _declarer=dt as DVar<string>;
        } else {
          _declarer=null;
        }
      }
    }
    public TopicView(ItemAction act, TopicView parent) {
      _parent=parent;
      _action=act;
      ptr=null;
      edited=true;
      _parent.children.Insert(0, this);
    }

    public TopicView Get(Topic oCur, bool createChildren=false, bool create=true) {
      if(oCur==null || oCur==Topic.root || oCur.path.StartsWith("/local/MQ") || oCur.name==("_declarer") || oCur.name=="_location") {  // || oCur.name.StartsWith("_")
        return root;
      }
      TopicView cur=root;
      TopicView next=null;
      List<Topic> originPath=new List<Topic>();
      Topic tmp=oCur;
      while(tmp!=Topic.root) {
        if(tmp==this.ptr) {
          cur=this;
          break;
        }
        originPath.Insert(0, tmp);
        tmp=tmp.parent;
      }

      for(int i=0; i<originPath.Count; i++, cur=next) {
        if(!createChildren && cur._children==null) {
          return null;
        }
        next=cur.children.FirstOrDefault(tv => tv.ptr!=null && tv.ptr.name==originPath[i].name);
        if(next==null) {
          if(!create) {
            return null;
          }
          next=new TopicView(originPath[i]);
          next._parent=cur;
          int j;
          for(j=0; j<cur.children.Count; j++) {
            if(next.name.CompareTo(cur.children[j].name)<0) {
              break;
            }
          }
          cur.children.Insert(j, next);
        } else if(next.ptr!=originPath[i]) {
          next.ptr=originPath[i];
        }
      }
      return cur;
    }
    public Topic ptr;
    public string name { get { return ptr!=null?(ptr==Topic.root?"/":ptr.name):"new item"; } set { } }
    public string image {
      get {
        if(_declarer!=null && !string.IsNullOrEmpty(_declarer.value)) {
          return _declarer.value;
        }
        if(ptr!=null) {
          TypeCode typeCode=Type.GetTypeCode(ptr.valueType);
          switch(typeCode) {
          case TypeCode.Object:
            if(ptr.valueType==typeof(Topic)) {
              return "/CC;component/Images/ty_ref.png";
            } else if(ptr.valueType==typeof(PiStatement)) {
              return "/CC;component/Images/ty_func.png";
            }
            break;
          case TypeCode.Boolean:
            return "/CC;component/Images/ty_bool.png";
          case TypeCode.Byte:
            return "/CC;component/Images/ty_i08.png";
          case TypeCode.Int16:
          case TypeCode.SByte:
          case TypeCode.UInt16:
            return "/CC;component/Images/ty_i16.png";
          case TypeCode.UInt32:
          case TypeCode.Int32:
            return "/CC;component/Images/ty_i32.png";
          case TypeCode.Single:
            return "/CC;component/Images/ty_f01.png";
          case TypeCode.Double:
            return "/CC;component/Images/ty_f02.png";
          case TypeCode.Decimal:
            return "/CC;component/Images/ty_f04.png";
          case TypeCode.String:
            return "/CC;component/Images/ty_str.png";
          case TypeCode.Empty:
            return "/CC;component/Images/ty_topic.png";
          }
        }
        return "/CC;component/Images/ty_obj.png";
      }
    }
    public string description {
      get {
        Topic dt;
        if(ptr.Exist("_description", out dt)) {
          return dt as DVar<string>;
        }

        if(_declarer!=null && _declarer.Exist("_description", out dt)) {
          return dt as DVar<string>;
        }
        string decl;
        if(ptr.parent!=null && ptr.parent.Exist("_declarer", out dt) && !string.IsNullOrWhiteSpace(decl=(dt as DVar<string>))) {
          Topic td=Topic.root.Get("/system/declarers/"+decl);
          DVar<string> ti=td.all.FirstOrDefault(z => z.name==ptr.name && z.valueType==typeof(string)) as DVar<string>;
          if(ti!=null && ti.Exist("_description", out dt)) {
            return dt as DVar<string>;
          }
        }
        return string.Empty;
      }
    }
    public ObservableCollection<TopicView> children {
      get {
        if(_children==null) {
          _children=new ObservableCollection<TopicView>();
          if(ptr!=null) {
            foreach(Topic t in ptr.children.Where(t1 => !t1.path.StartsWith("/local/MQ") && t1.name!="_declarer")) {
              TopicView cur=new TopicView(t);
              cur._parent=this;
              _children.Add(cur);
            }
          }
        }
        return _children;
      }
    }
    public bool edited { get; private set; }
    internal List<ItemActionStr> GetActions() {
      List<ItemActionStr> actions=new List<ItemActionStr>();

      if(_declarer!=null) {
        List<RcUse> resource=new List<RcUse>();
        var ar=_declarer.all.Where(z => z!=null && z.valueType==typeof(string) && !z.name.StartsWith("_")).Cast<DVar<string>>().Where(z => z.value!=null && z.value.Length>=2).OrderBy(z => z.name).OrderBy(z => (ushort)z.value[0]).ToList();

        foreach(var ch in ptr.children) {   // check used resources
          var dec=ar.FirstOrDefault(z => z.name==ch.name);
          if(dec!=null) {
            foreach(string curRC in dec.value.Substring(2).Split(',').Where(z => !string.IsNullOrWhiteSpace(z) && z.Length>1)) {
              int pos;
              if(!int.TryParse(curRC.Substring(1), out pos)) {
                continue;
              }
              for(int i=pos-resource.Count; i>=0; i--) {
                resource.Add(RcUse.None);
              }
              if(curRC[0]==(char)RcUse.Exclusive) {
                resource[pos]=RcUse.Exclusive;
              } else if(curRC[0]==(char)RcUse.Shared && resource[pos]==RcUse.None) {
                resource[pos]=RcUse.Shared;
              }
            }
          }
        }

        foreach(var tpI in ar) {
          bool busy=ptr.children.Any(z => z.name==tpI.name);
          if(busy) {      // don't show already exist variable
            continue;
          }
          foreach(string curRC in tpI.value.Substring(2).Split(',').Where(z => !string.IsNullOrWhiteSpace(z) && z.Length>1)) {
            int pos;
            if(!int.TryParse(curRC.Substring(1), out pos)) {
              continue;
            }
            if(pos<resource.Count && curRC[0]!=(char)RcUse.None && resource[pos]==RcUse.Exclusive) {
              busy=true;
              break;
            }

          }
          if(!busy) {
            Topic dt;
            string ptc=tpI.path.Substring(_declarer.path.Length);
            string desc=tpI.Exist("_description", out dt)?(dt as DVar<string>).value:string.Empty;
            actions.Add(new ItemActionStr(ptc, (ItemAction)tpI.value[1], desc));
          }
        }
      } else {
        actions.Add(new ItemActionStr("Add/Node", ItemAction.createNodeMask, null));
        actions.Add(new ItemActionStr("Add/bool", ItemAction.createBoolMask, null));
        actions.Add(new ItemActionStr("Add/long", ItemAction.createLongMask, null));
        actions.Add(new ItemActionStr("Add/double", ItemAction.createDoubleMask, null));
        actions.Add(new ItemActionStr("Add/string", ItemAction.createStringMask, null));
        if(ptr.valueType!=null && Type.GetTypeCode(ptr.valueType)!=TypeCode.Object) {
          actions.Add(new ItemActionStr("Attach to Logram", ItemAction.addToLogram, null));
        }
        actions.Add(new ItemActionStr("remove", ItemAction.remove, null));
      }
      return actions;
    }
    private bool isExpanded;
    public bool IsExpanded {
      get { return isExpanded; }
      set {
        isExpanded = value;
        OnPropertyChanged("IsExpanded");
      }
    }
    private enum RcUse : ushort {
      None='0',
      Exclusive='X',
      Shared='S',
    }
    public void Remove() {
      if(_parent!=null) {
        _parent.children.Remove(this);
        if(ptr!=null) {
          _parent.Get(ptr);
        }
      }
    }
    public void Create(string name) {
      if(_parent!=null && _action!=ItemAction.empty) {
        this.Remove();
        switch(_action) {
        case ItemAction.createNodeMask:
          _parent.ptr.Get(name);
          break;
        case ItemAction.createBoolMask:
          _parent.ptr.Get<bool>(name);
          break;
        case ItemAction.createLongMask:
          _parent.ptr.Get<long>(name);
          break;
        case ItemAction.createDoubleMask:
          _parent.ptr.Get<double>(name);
          break;
        case ItemAction.createStringMask:
          _parent.ptr.Get<string>(name);
          break;
        }
      }
    }

    internal struct ItemActionStr {
      public ItemActionStr(string menuItem, ItemAction action, string desc) {
        this.menuItem=menuItem;
        this.action=action;
        this.description=desc;
      }
      public readonly string menuItem;
      public readonly ItemAction action;
      public readonly string description;
    }
    internal void OnPropertyChanged(string name) {
      if(PropertyChanged!=null) {
        PropertyChanged(this, new PropertyChangedEventArgs(name));
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    internal void AttrChanged(Topic oCur) {
      if(oCur.name=="_declarer") {
        DefDeclarer();
        OnPropertyChanged("image");
      }
    }
  }
}
