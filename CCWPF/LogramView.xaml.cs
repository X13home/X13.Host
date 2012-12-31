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

namespace X13.CC {
  /// <summary>Interaction logic for LogramView.xaml</summary>
  public partial class LogramView : DocumentContent {
    #region Settings
    private static Topic _settings;
    private static Lazy<Typeface> _ftFont=new Lazy<Typeface>(() => new Typeface("Times New Roman"));

    static LogramView() {
      _settings=Topic.root.Get("/local/settings/Logram");
    }

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

      m.changed+=ModelChanged;
    }

    private void ModelChanged(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Add) {   // rename
        Dispatcher.BeginInvoke(new Action(() => { this.Title=model.name; this.Name=ExConverter.String2Name(model.path); }), System.Windows.Threading.DispatcherPriority.Background);
      }
    }

    private Button _selectedButton;
    private Point _MouseDownPoint;
    private bool _readyToDrag;
    private void WrapPanel_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
      Image img;
      if((_selectedButton=e.OriginalSource as Button)!=null || ((img=e.OriginalSource as Image)!=null && (_selectedButton=img.Parent as Button)!=null)) {
        _MouseDownPoint=e.GetPosition(this);
        _readyToDrag=true;
      }
    }

    private void WrapPanel_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
      string tag;
      Point pos=e.GetPosition(this);
      if(_readyToDrag && _selectedButton!=null && (Math.Abs(_MouseDownPoint.X-pos.X)>SystemParameters.MinimumHorizontalDragDistance || Math.Abs(_MouseDownPoint.Y-pos.Y)>SystemParameters.MinimumVerticalDragDistance) && !string.IsNullOrEmpty(tag=_selectedButton.Tag as string)) {
        _readyToDrag=false;
        PiStatement st=new PiStatement(tag);
        DragDrop.DoDragDrop(_selectedButton, st, DragDropEffects.Copy);
      }
    }

    private void WrapPanel_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
      string tag;
      if(_selectedButton!=null && !string.IsNullOrEmpty(tag=_selectedButton.Tag as string)) {
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
        _selectedButton=null;
      }

    }
  }
}
