#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.ComponentModel;
using System.Linq;
using AvalonDock;
using X13.WOUM;
using X13.PLC;

namespace X13.CC {
  /// <summary>
  /// Interaction logic for PropertyView.xaml
  /// </summary>
  public partial class PropertyView : DockableContent {
    private static PropertyView _instance;
    private static object _sel;

    public static object Selected {
      get { return _sel; }
      set {
        _sel=value;
        _instance.Dispatcher.BeginInvoke(new Action(_instance.SelectionChanged), System.Windows.Threading.DispatcherPriority.Input);
        SecurityView.SetSelected(value as Topic);
      }
    }

    public PropertyView() {
      _instance=this;
      InitializeComponent();
      this.DataContext = this;
      PropertyGrid1.SizeChanged+=new System.Windows.SizeChangedEventHandler(PropertyGrid1_SizeChanged);
    }
    private void PropertyPanel_LayoutUpdated(object sender, EventArgs e) {
      PropertyGrid1_SizeChanged(null, null);
    }

    private void PropertyGrid1_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e) {
      PropertyGrid1.NameColumnWidth=PropertyGrid1.ActualWidth/3;
    }
    private void SelectionChanged() {
        if(PropertyGrid1.SelectedObject is TopicViewProp) {
          (PropertyGrid1.SelectedObject as TopicViewProp).Close();
        }
      Topic ts=_sel as Topic;
      if(ts!=null) {
        PropertyGrid1.SelectedObject=new TopicViewProp(ts);
        PropertyGrid1.SetValue(Xceed.Wpf.Toolkit.PropertyGrid.PropertyGrid.SelectedObjectNameProperty, ts.path);
        Topic dt;
        if(ts.Exist("_declarer", out dt)) {
          PropertyGrid1.SetValue(Xceed.Wpf.Toolkit.PropertyGrid.PropertyGrid.SelectedObjectTypeNameProperty, (dt as DVar<string>).value);
        } else {
          PropertyGrid1.SetValue(Xceed.Wpf.Toolkit.PropertyGrid.PropertyGrid.SelectedObjectTypeNameProperty, ts.valueType==null?"Topic":ts.valueType.Name);
        }
        
      } else {
        PropertyGrid1.SelectedObject=null;
      }
    }
    private class TopicViewProp : ICustomTypeDescriptor, INotifyPropertyChanged {
      private Topic obj;
      private PropertyDescriptorCollection propsColl;
      
      public TopicViewProp(Topic item) {
        obj=item;
        obj.Subscribe("+", obj_changed);
        propsColl=new PropertyDescriptorCollection(null);
        foreach(PropertyDescriptor pr in TypeDescriptor.GetProperties(this.obj)) {
          if(obj.valueType!=null || pr.Category!="Content") {
            propsColl.Add(pr);
          }
        }
        foreach(Topic tp in obj.children) {
          if(tp.name=="_declarer" || tp.name=="_location" || tp.valueType==null) {
            continue;
          }
          PropertyDescriptor np=new DVarPropertyDescriptor(tp);
          propsColl.Add(np);
        }
      }

      public void Close() {
        obj.Unsubscribe("+", obj_changed);
      }
      private void obj_changed(Topic sender, TopicChanged param) {
        if(param.Art==TopicChanged.ChangeArt.Value) {
          if(PropertyChanged!=null) {
            if(sender==obj) {
              PropertyChanged(this, new PropertyChangedEventArgs("value"));
            } else if(sender!=null) {
              PropertyChanged(this, new PropertyChangedEventArgs(ExConverter.String2Name("P_", sender.name)));
            }
          }
        } else {
          PropertyView.Selected=obj;
        }
      }

      #region ICustomTypeDescriptor
      AttributeCollection ICustomTypeDescriptor.GetAttributes() {
        return TypeDescriptor.GetAttributes(this.obj);
      }

      string ICustomTypeDescriptor.GetClassName() {
        return TypeDescriptor.GetClassName(this.obj);
      }

      string ICustomTypeDescriptor.GetComponentName() {
        return TypeDescriptor.GetComponentName(this.obj);
      }

      TypeConverter ICustomTypeDescriptor.GetConverter() {
        return TypeDescriptor.GetConverter(this.obj);
      }

      EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() {
        return TypeDescriptor.GetDefaultEvent(this.obj);
      }

      PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() {
        return TypeDescriptor.GetDefaultProperty(this.obj);
      }

      object ICustomTypeDescriptor.GetEditor(Type editorBaseType) {
        return TypeDescriptor.GetEditor(this.obj, editorBaseType);
      }

      EventDescriptorCollection ICustomTypeDescriptor.GetEvents() {
        return TypeDescriptor.GetEvents(this.obj);
      }

      EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes) {
        return TypeDescriptor.GetEvents(this.obj, attributes);
      }

      PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() {
        return this.propsColl;
      }

      PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes) {
        return this.propsColl;
      }

      object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd) {
        return this.obj;
      }
      #endregion

      #region INotifyPropertyChanged
      public event PropertyChangedEventHandler PropertyChanged;
      #endregion INotifyPropertyChanged
    }
    private class DVarPropertyDescriptor : PropertyDescriptor {
      private Topic obj;

      public DVarPropertyDescriptor(Topic obj) : base(ExConverter.String2Name("P_", obj.name), 
        new Attribute[] { 
          new CategoryAttribute("Entrys"), 
          new BrowsableAttribute(true), 
          new ReadOnlyAttribute(false), 
          new DescriptionAttribute(GetDesc(obj)) 
        }){

        this.obj=obj;
      }

      private static string GetDesc(Topic obj) {
        var oView=TopicView.root.Get(obj);
        return oView!=null?oView.description:string.Empty;
      }
      public override string DisplayName {get { return obj.name; } }
      public override bool CanResetValue(object component) { return false; }
      public override Type ComponentType { get { return obj.GetType(); } }
      public override object GetValue(object component) { return obj.GetValue(); }
      public override bool IsReadOnly { get { return false; } }
      public override Type PropertyType { get { return obj.valueType; } }
      public override void ResetValue(object component) { }
      public override void SetValue(object component, object value) { obj.SetValue(value, new TopicChanged(TopicChanged.ChangeArt.Value)); }
      public override bool ShouldSerializeValue(object component) { return true; }
    }
  }
}
