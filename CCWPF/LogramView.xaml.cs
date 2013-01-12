#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AvalonDock;
using X13.PLC;
using X13.WOUM;
using System.Collections.ObjectModel;
using System.Linq;

namespace X13.CC {
  /// <summary>Interaction logic for LogramView.xaml</summary>
  public partial class LogramView : DocumentContent {
    #region Settings
    private static Topic _settings;
    private static Lazy<Typeface> _ftFont=new Lazy<Typeface>(() => new Typeface("Times New Roman"));

    public static int CellSize {
      get {
        var s=_settings.Get<int>("_CellSize");
        if(s.value<=4) {
          s.saved=true;
          s.value=16;
        }
        return s.value;
      }
    }
    public static Typeface FtFont { get { return _ftFont.Value; } }
    #endregion Settings

    static LogramView() {
      _settings=Topic.root.Get("/local/settings/Logram");
      _statements=new ObservableCollection<StatementDescription>();
      Topic decls=Topic.root.Get("/system/declarers");
      //decls.Subscribe("+", DeclarerChanged);
      TopicChanged p=new TopicChanged(TopicChanged.ChangeArt.Add);
      foreach(Topic d in decls.children) {
        DeclarerChanged(d, p);
      }
    }
    private static void DeclarerChanged(Topic sender, TopicChanged param) {
      DVar<string> dec=sender as DVar<string>;
      Topic infoT;
      DVar<string> infoD;
      if(dec==null || !dec.Exist("_type", out infoT) || (infoD=(infoT as DVar<string>))==null || infoD.value!="X13.PLC.PiStatemen") {
        return;
      }

      StatementDescription stR=null;

      if(param.Art==TopicChanged.ChangeArt.Remove) {
        stR=_statements.FirstOrDefault(z => z.name==dec.name);
        if(stR!=null) {
          _statements.Remove(stR);
        }
      } else {
        if(param.Art==TopicChanged.ChangeArt.Value) {
          stR=_statements.FirstOrDefault(z => z.name==dec.name);
        }
        if(stR==null) {
          stR=new StatementDescription() { name=dec.name };
          _statements.Add(stR);
        }
        stR.image=dec.value;
        if(dec.Exist("_description", out infoT) && (infoD=(infoT as DVar<string>))!=null) {
          stR.info=infoD.value;
        }
      }
    }
    private class StatementDescription {
      public string name{get; set;}
      public string info { get; set; }
      public string image { get; set; }
    }

    private static ObservableCollection<StatementDescription> _statements;

    public DVar<PiLogram> model { get { return uiLogram.model; } }

    public LogramView(string lName) {
      this.Name=lName;
      DVar<PiLogram> m=Topic.root.Get("/plc").Get<PiLogram>(ExConverter.Name2String(lName));
      if(m.value==null) {
        m.saved=true;
        m.value=new PiLogram();
      }
      this.Title=m.name;
      InitializeComponent();
      this.DataContext = this;
      if(m!=null) {
        uiLogram.Attach(m);
      }
      this.statemebtsList.ItemsSource=_statements;
      m.changed+=ModelChanged;
    }

    private void ModelChanged(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Add) {   // rename
        Dispatcher.BeginInvoke(new Action(() => { this.Title=model.name; this.Name=ExConverter.String2Name(model.path); }), System.Windows.Threading.DispatcherPriority.Background);
      }
    }

    private Image _selectedImage;
    private Point _MouseDownPoint;
    private bool _readyToDrag;
    private void WrapPanel_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
      if((_selectedImage=e.OriginalSource as Image)!=null) {
        _MouseDownPoint=e.GetPosition(this);
        _readyToDrag=true;
      }
    }

    private void WrapPanel_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
      string tag;
      Point pos=e.GetPosition(this);
      if(_readyToDrag && _selectedImage!=null && (Math.Abs(_MouseDownPoint.X-pos.X)>SystemParameters.MinimumHorizontalDragDistance || Math.Abs(_MouseDownPoint.Y-pos.Y)>SystemParameters.MinimumVerticalDragDistance) && !string.IsNullOrEmpty(tag=_selectedImage.Tag as string)) {
        _readyToDrag=false;
        PiStatement st=new PiStatement(tag);
        DragDrop.DoDragDrop(_selectedImage, st, DragDropEffects.Copy);
      }
    }

    private void WrapPanel_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
      string tag;
      if(_selectedImage!=null && !string.IsNullOrEmpty(tag=_selectedImage.Tag as string)) {
        _readyToDrag=false;
        if(model!=null) {
          int i=1;
          string name;
          do {
            name=string.Format("A{0:D02}", i);
            i++;
          } while(model.Exist(name));
          var c=model.Get<PiStatement>(name);
          c.saved=true;
          c.value=new PiStatement(tag);
        }
        _selectedImage=null;
      }

    }
  }
}
