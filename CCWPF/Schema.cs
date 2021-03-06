﻿#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using X13.PLC;

namespace X13.CC {
  internal class Schema : Canvas {
    #region Settings
    public static readonly Pen SelectionPen=new Pen(Brushes.Orange, 1);
    #endregion Settings

    private double _zoom=100.0;
    private Point ScreenStartPoint;
    private Point startOffset;
    private TransformGroup _transformGroup;
    private TranslateTransform _translateTransform;
    private ScaleTransform _zoomTransform;
    private DrawingVisual _backgroundVisual;
    private DrawingVisual _mSelectVisual;
    private uiItem _selected;
    private SchemaElement[] _mSelected;
    private bool _multipleSelection;
    private SortedList<uint, uiItem> _map;
    private bool move;

    public List<Visual> _visuals;
    public uiItem selected {
      get { return _selected; }
      private set {
        if(_selected!=null) {
          _selected.Select(false);
        }
        _selected=value;
        if(_selected!=null) {
          PropertyView.Selected=_selected.GetModel();
          _selected.Select(true);
        } else {
          PropertyView.Selected=model;
        }
        this.Focus();
      }
    }
    public DVar<PiLogram> model { get; private set; }

    public Schema() {
      _visuals=new List<Visual>();
      _backgroundVisual = new DrawingVisual();
      _mSelectVisual=new DrawingVisual();
      _translateTransform = new TranslateTransform();
      _zoomTransform = new ScaleTransform() { ScaleX=_zoom/100, ScaleY=_zoom/100 };
      _transformGroup = new TransformGroup();

      _transformGroup.Children.Add(_zoomTransform);
      _transformGroup.Children.Add(_translateTransform);
      RenderTransform = _transformGroup;
      AddVisualChild(_backgroundVisual);
      _map=new SortedList<uint, uiItem>();
      this.AllowDrop=true;
      this.Drop+=new DragEventHandler(Schema_Drop);
    }

    private void Schema_Drop(object sender, DragEventArgs e) {
      uint loc;
      {
        int gs=LogramView.CellSize;
        var pos=e.GetPosition(this);
        int topCell=(int)(pos.Y/gs)-1;
        if(topCell<0) {
          topCell=0;
        }
        int leftCell=(int)(pos.X/gs)-1;
        if(leftCell<0) {
          leftCell=0;
        }
        loc=(uint)((leftCell<<16) | (ushort)topCell);
      }
      var l=e.Data.GetFormats();
      if(e.Data.GetDataPresent(typeof(string))) {
        Topic cur=Topic.root.Get((string)e.Data.GetData(typeof(string)));

        string name=cur.name;
        int i=1;
        while(model.Exist(name)) {
          name=string.Format("{0}_{1}", cur.name, i++);
        }
        var it=model.Get<Topic>(name);
        var sLoc=it.Get<long>("_location");
        sLoc.saved=true;
        sLoc.value=loc;
        it.saved=true;
        it.value=cur;
      } else if(e.Data.GetDataPresent(typeof(PiStatement))) {
        PiStatement st=(PiStatement)e.Data.GetData(typeof(PiStatement));
        int i=1;
        string name;
        do {
          name=string.Format("A{0:D02}", i);
          i++;
        } while(model.Exist(name));
        try {
          var it=model.Get<PiStatement>(name);
          var sLoc=it.Get<long>("_location");
          sLoc.saved=true;
          sLoc.value=loc;
          it.saved=true;
          it.value=st;
        }
        catch(ObjectDisposedException) {
        }
      }
    }

    public void Attach(DVar<PiLogram> model) {
      if(this.model!=model) {
        this.model=model;
        var w=model.Get<long>("_width");
        if(w.value==0) {
          w.saved=true;
          w.value=24;
        }
        this.Width=w.value*LogramView.CellSize;
        var h=model.Get<long>("_height");
        if(h.value<=0) {
          h.saved=true;
          h.value=24;
        }
        this.Height=h.value*LogramView.CellSize;
        _map.Clear();
        DrawingVisual cur;
        foreach(var p in model.children.Where(z => z.valueType==typeof(Topic)).Cast<DVar<Topic>>()) {
          try {
            cur=new uiAlias(p, this);
          }
          catch(Exception ex) {
            Log.Error("create uiAlias({0}) - {1}", p.path, ex.ToString());
          }
        }
        foreach(var p in model.children.Where(z => z.valueType==typeof(PiStatement)).Cast<DVar<PiStatement>>()) {
          try {
          cur=new uiStatement(p, this);
          }
          catch(Exception ex) {
            Log.Error("create uiStatement({0}) - {1}", p.path, ex.ToString());
          }
        }
        foreach(var p in model.children.Where(z => z.valueType==typeof(PiWire)).Cast<DVar<PiWire>>()) {
          try {
          cur=new uiWire(p, this);
          }
          catch(Exception ex) {
            Log.Error("create uiWire({0}) - {1}", p.path, ex.ToString());
          }
        }
        foreach(var p in model.children.Where(z => z.valueType==typeof(PiTracer)).Cast<DVar<PiTracer>>()) {
          try {
          cur=new uiTracer(p, this);
          }
          catch(Exception ex) {
            Log.Error("create uiTracer({0}) - {1}", p.path, ex.ToString());
          }
        }
        model.Subscribe("+", ModelChanged);
      }
    }
    public void MapRemove(uiItem val) {
      lock(_map) {
        foreach(var i in _map.Where(z => z.Value==val).ToArray()) {
          _map.Remove(i.Key);
          //Log.Debug("MapRemove({0}, {1}, {2}) = {3}", (i.Key&1)!=0?"V":"H", (i.Key>>1) & 0xFFFF, (i.Key>>17) & 0x7FFF, i.Value);
        }
      }
    }
    public void MapSet(bool vert, int x, int y, uiItem val) {
      uint idx=(uint)(((y&0x7FFF)<<17) | ((x&0xFFFF)<<1) | (vert?1:0));
      lock(_map) {
        if(val==null) {
          _map.Remove(idx);
        } else {
          _map[idx]=val;
        }
      }
      //RenderBackground();
      //Log.Debug("MapSet({0}, {1}, {2}) = {3}", vert?"V":"H", x, y, val);
    }
    public uiItem MapGet(bool vert, int x, int y) {
      uint idx=(uint)(((y&0x7FFF)<<17) | ((x&0xFFFF)<<1) | (vert?1:0));
      uiItem ret;
      lock(_map) {
        _map.TryGetValue(idx, out ret);
      }
      return ret;
    }

    public void AddVisual(Visual item) {
      if(item is SchemaElement) {
        int i=_visuals.FindIndex(z => z is uiPin || z is uiWire);
        _visuals.Insert(i>0?i:_visuals.Count, item);
      } else if(item is uiWire) {
        int i=_visuals.FindIndex(z => z is uiPin);
        _visuals.Insert(i>0?i:_visuals.Count, item);
      } else {
        _visuals.Add(item);
      }
      base.AddVisualChild(item);
      base.AddLogicalChild(item);
    }
    public void DeleteVisual(Visual item) {
      _visuals.Remove(item);
      base.RemoveVisualChild(item);
      base.RemoveLogicalChild(item);
      if(item is uiItem) {
        MapRemove(item as uiItem);
      }
    }

    private void RenderBackground() {
      int gs=LogramView.CellSize;
      using(DrawingContext dc = _backgroundVisual.RenderOpen()) {
        Pen pen = new Pen(Brushes.LightGray, 0.5d);
        pen.DashStyle=new DashStyle(new double[] { 3, gs*2-3 }, 1.5);
        for(double x = gs; x < this.Width; x += gs) {
          dc.DrawLine(pen, new Point(x, 0), new Point(x, this.Height));
        }
        for(double y = gs; y < this.Height; y += gs) {
          dc.DrawLine(pen, new Point(0, y), new Point(this.Width, y));
        }
        //foreach(var kv in _map) {
        //  int x=gs/2+gs*((int)(kv.Key>>1) & 0xFFFF);
        //  int y=gs/2+gs*((int)(kv.Key>>17) & 0x7FFF);
        //  if((kv.Key&1)!=0) {
        //    FormattedText txt=new FormattedText(kv.Value.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LogramView.FtFont, gs*0.3, Brushes.Violet);
        //    dc.DrawText(txt, new Point(x, y));
        //  } else {
        //    FormattedText txt=new FormattedText(kv.Value.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LogramView.FtFont, gs*0.3, Brushes.DarkMagenta);
        //    dc.DrawText(txt, new Point(x, y+gs/2));
        //  }
        //}
      }
    }
    private void ModelChanged(Topic sender, TopicChanged param) {
      if(sender!=null && sender.parent==model) {
        Dispatcher.BeginInvoke(new Action<Topic, TopicChanged.ChangeArt>(ModelItemChanged), System.Windows.Threading.DispatcherPriority.DataBind, sender, param.Art);
      }
    }
    private void ModelItemChanged(Topic source, TopicChanged.ChangeArt art) {
      if(art==TopicChanged.ChangeArt.Add) {
        return;
      }
      try {
        DrawingVisual cur;

        if(art==TopicChanged.ChangeArt.Value) {
          if(source.name=="_width") {
            var w=model.Get<long>("_width");
            if(w.value==0) {
              w.saved=true;
              w.value=24;
            }
            this.Width=w.value*LogramView.CellSize;
          } else if(source.name=="_height") {
            var h=model.Get<long>("_height");
            if(h.value<=0) {
              h.saved=true;
              h.value=24;
            }
            this.Height=h.value*LogramView.CellSize;
          } else if(source.valueType==typeof(Topic)) {
            if(!_visuals.Where(z => z is uiAlias).Cast<uiAlias>().Any(z => z.model==source)) {
              cur=new uiAlias(source as DVar<Topic>, this);
            }
          } else if(source.valueType==typeof(PiStatement)) {
            if(!_visuals.Where(z => z is uiStatement).Cast<uiStatement>().Any(z => z.model==source)) {
              System.Threading.Thread.Sleep(180);     // filling of the fields executed by the broker
              cur=new uiStatement(source as DVar<PiStatement>, this);
            }

          } else if(source.valueType==typeof(PiWire)) {
            if(!_visuals.Where(z => z is uiWire).Cast<uiWire>().Any(z => z.GetModel()==source)) {
              cur=new uiWire(source as DVar<PiWire>, this);
            }
          } else if(source.valueType==typeof(PiTracer)) {
            if(!_visuals.Where(z => z is uiTracer).Cast<uiTracer>().Any(z => z.GetModel()==source)) {
              cur=new uiTracer(source as DVar<PiTracer>, this);
            }
          }
        }
      }
      catch(Exception ex) {
        Log.Error("Schema.ModelItemChanged({0}, {1}) - {2}", art, source.path, ex.Message);
      }
    }

    protected override void OnKeyUp(KeyEventArgs e) {
      if(e.Key==Key.Delete) {
        Topic t;
        if(_mSelected!=null) {
          foreach(var el in _mSelected) {
            el.Select(false);
            if((t=el.GetModel())!=null) {
              t.Remove();
            }
          }
          _mSelected=null;
        } else if(selected!=null && (t=_selected.GetModel())!=null) {
          t.Remove();
        }
        PropertyView.Selected=null;
      } else if(e.Key==Key.C && Keyboard.IsKeyDown(Key.LeftCtrl)) {
        mi_Copy(null, null);
      } else if(e.Key==Key.V && Keyboard.IsKeyDown(Key.LeftCtrl)) {
        mi_Paste(null, null);
      }
      base.OnKeyUp(e);
    }
    protected override int VisualChildrenCount {
      get {
        return _visuals.Count+(_multipleSelection?2:1);   // _backgroundVisual, _mSelectVisual
      }
    }
    protected override Visual GetVisualChild(int index) {
      if(index==0) {
        return _backgroundVisual;
      } else if(index==_visuals.Count+1) {
        return _mSelectVisual;
      }
      return _visuals[index-1];
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e) {
      if(Keyboard.IsKeyDown(Key.LeftCtrl)) {
        if(e.Delta<0?_zoom>40:_zoom<250) {
          var p=e.GetPosition(this);
          _zoom+=e.Delta/30.0;
          _zoomTransform.CenterX=p.X;
          _zoomTransform.CenterY=p.Y;
          _zoomTransform.ScaleY=_zoom/100.0;
          _zoomTransform.ScaleX=_zoom/100.0;
        }
        e.Handled = true;
      } else {
        base.OnMouseWheel(e);
      }
    }
    protected override void OnMouseDown(MouseButtonEventArgs e) {
      if(e.LeftButton==MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.LeftCtrl)) {
        ScreenStartPoint=e.GetPosition((IInputElement)this.Parent);
        startOffset = new Point(_translateTransform.X, _translateTransform.Y);
        CaptureMouse();
        Cursor = Cursors.ScrollAll;
        e.Handled=true;
      } else {
        ScreenStartPoint=e.GetPosition(this);
        if(_mSelected==null) {
          selected=GetVisual(ScreenStartPoint.X, ScreenStartPoint.Y);
          if(selected==null) {
            base.OnMouseDown(e);
          }
        }
      }
    }
    protected override void OnMouseMove(MouseEventArgs e) {
      var cp=e.GetPosition(this);
      if(IsMouseCaptured && Keyboard.IsKeyDown(Key.LeftCtrl)) {
        var pnt=(IInputElement)this.Parent;
        Point p=e.GetPosition(pnt);
        double toX=startOffset.X+p.X-ScreenStartPoint.X;
        double toY=startOffset.Y+p.Y-ScreenStartPoint.Y;
        _translateTransform.X=toX;
        _translateTransform.Y=toY;
      } else if(e.LeftButton==MouseButtonState.Pressed && (move || (Math.Abs(cp.X-ScreenStartPoint.X)>SystemParameters.MinimumHorizontalDragDistance || Math.Abs(cp.Y-ScreenStartPoint.Y)>SystemParameters.MinimumVerticalDragDistance))) {
        move=true;
        if(selected!=null) {
          SchemaElement el;
          uiWire w;
          uiPin pin;
          if((el=selected as SchemaElement)!=null) {
            el.SetLocation(new Vector(el.OriginalLocation.X+(cp.X-ScreenStartPoint.X), el.OriginalLocation.Y+(cp.Y-ScreenStartPoint.Y)), false);
          } else if((pin=selected as uiPin)!=null) {
            w=new uiWire(selected as uiPin, this);
            w.Update(ScreenStartPoint);
            selected=w;
          } else if((w=selected as uiWire)!=null && w.B==null) {
            w.Update(cp);
          }
        } else if(_mSelected!=null) {
          foreach(var el in _mSelected.Where(z=>!(z is uiTracer))) {
            el.SetLocation(new Vector(el.OriginalLocation.X+(cp.X-ScreenStartPoint.X), el.OriginalLocation.Y+(cp.Y-ScreenStartPoint.Y)), false);
          }
        } else {
          if(!_multipleSelection) {
            _multipleSelection=true;
            base.AddVisualChild(_mSelectVisual);
          }
          using(DrawingContext dc=_mSelectVisual.RenderOpen()) {
            dc.DrawRectangle(null, Schema.SelectionPen, new Rect(ScreenStartPoint, cp));
          }
        }
      } else {
        base.OnMouseMove(e);
      }
    }
    protected override void OnMouseUp(MouseButtonEventArgs e) {
      int gs=LogramView.CellSize;
      if(e.ChangedButton==MouseButton.Right && e.RightButton==MouseButtonState.Released) {
        Topic cur;
        TopicView tv;
        if(_mSelected!=null) {
          var cm=(this.Parent as Grid).ContextMenu;
          cm.Items.Clear();
          MenuItem mi=new MenuItem();
          mi.Header="Copy";
          mi.Click+=new RoutedEventHandler(mi_Copy);
          cm.Items.Add(mi);
          cm.IsOpen=true;
        } else if(selected!=null && (cur=selected.GetModel())!=null && (tv=TopicView.root.Get(cur, true))!=null) {
          var cm=(this.Parent as Grid).ContextMenu;
          var actions=tv.GetActions();
          cm.Items.Clear();

          ItemCollection items;
          for(int i=0; i<actions.Count; i++) {
            switch(actions[i].action) {
            case ItemAction.addToLogram:
            case ItemAction.createBoolMask:
            case ItemAction.createDoubleMask:
            case ItemAction.createLongMask:
            case ItemAction.createNodeMask:
            case ItemAction.createStringMask:
            case ItemAction.createByteArrMask:
            case ItemAction.open:
              continue;
            }
            items=cm.Items;
            string[] lvls=actions[i].menuItem.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for(int j=0; j<lvls.Length; j++) {
              MenuItem mi = DataStorageView.FindMenuItem(items, lvls[j]);
              if(mi==null) {
                mi=new MenuItem();
                mi.Header=lvls[j];
                mi.DataContext=tv;
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
          if(cur.valueType==typeof(PiStatement)) {
            MenuItem mi=new MenuItem();
            mi.Header="Copy";
            mi.Click+=new RoutedEventHandler(mi_Copy);
            cm.Items.Add(mi);
          } else if(selected is uiPin) {
            MenuItem mi=new MenuItem();
            mi.Header="Trace";
            mi.Click+=new RoutedEventHandler(mi_Trace);
            cm.Items.Add(mi);
          }
          if(cm.Items.Count>0) {
            cm.IsOpen=true;
          }
        } else if(Clipboard.ContainsText()) {
          var cm=(this.Parent as Grid).ContextMenu;
          cm.Items.Clear();
          MenuItem mi=new MenuItem();
          mi.Header="Paste";
          mi.Click+=new RoutedEventHandler(mi_Paste);
          cm.Items.Add(mi);
          cm.IsOpen=true;
        }
        return;
      } else if(e.ChangedButton==MouseButton.Left && e.LeftButton==MouseButtonState.Released) {
        if(_mSelected!=null && _mSelected.Length>0) {
          var cp=e.GetPosition(this);
          double r=0, d=0;
          foreach(var el in _mSelected.Where(z => !(z is uiTracer))) {
            if(move) {
              el.SetLocation(new Vector(el.OriginalLocation.X+(cp.X-ScreenStartPoint.X), el.OriginalLocation.Y+(cp.Y-ScreenStartPoint.Y)), true);
              d=Math.Max(d, el.Offset.Y+el.ContentBounds.Bottom);
              r=Math.Max(r, el.Offset.X+el.ContentBounds.Right);
            }
            el.Select(false);
          }
          _mSelected=null;
          if(move) {
            if(d+gs>this.Height) {
              model.Get<long>("_height").value=1+(int)(d)/gs;
            }
            if(r+gs>this.Width) {
              model.Get<long>("_width").value=1+(int)(r)/gs;
            }
          }
        }
        if(IsMouseCaptured) {
          Cursor = Cursors.Arrow;
          ReleaseMouseCapture();
        } else if(selected!=null) {
          SchemaElement el;
          uiWire w;
          var cp=e.GetPosition(this);
          if((el=selected as SchemaElement)!=null && move) {
            el.SetLocation(new Vector(el.OriginalLocation.X+(cp.X-ScreenStartPoint.X), el.OriginalLocation.Y+(cp.Y-ScreenStartPoint.Y)), true);
            if(selected.Offset.Y+selected.ContentBounds.Bottom+gs>this.Height) {
              model.Get<long>("_height").value=1+(int)(selected.Offset.Y+selected.ContentBounds.Bottom)/gs;
            }
            if(selected.Offset.X+selected.ContentBounds.Right+gs>this.Width) {
              model.Get<long>("_width").value=1+(int)(selected.Offset.X+selected.ContentBounds.Right)/gs;
            }
          } else if((w=selected as uiWire)!=null && w.GetModel()==null) {
            uiPin finish=GetVisual(cp.X, cp.Y) as uiPin;
            if(finish!=null && finish!=w.A) {
              w.SetFinish(finish);
            } else {
              this.DeleteVisual(w);
            }
          }
        } else if(_multipleSelection) {
          var cp=e.GetPosition(this);
          _multipleSelection=false;
          base.RemoveVisualChild(_mSelectVisual);
          var objs=new List<SchemaElement>();
          GeometryHitTestParameters parameters;
          {
            double l, t, w, h;
            if(cp.X-ScreenStartPoint.X<0) {
              l=cp.X;
              w=ScreenStartPoint.X-cp.X;
            } else {
              l=ScreenStartPoint.X;
              w=cp.X-ScreenStartPoint.X;
            }
            if(cp.Y-ScreenStartPoint.Y<0) {
              t=cp.Y;
              h=ScreenStartPoint.Y-cp.Y;
            } else {
              t=ScreenStartPoint.Y;
              h=cp.Y-ScreenStartPoint.Y;
            }
            parameters=new GeometryHitTestParameters(new RectangleGeometry(new Rect(l, t, w, h)));
          }

          VisualTreeHelper.HitTest(this, null, new HitTestResultCallback((hr) => {
            var rez=(GeometryHitTestResult)hr;
            var vis=hr.VisualHit as SchemaElement;
            if(vis!=null && rez.IntersectionDetail==IntersectionDetail.FullyInside) {
              objs.Add(vis);
            }
            return HitTestResultBehavior.Continue;
          }), parameters);
          if(objs.Count>0) {
            if(objs.Count==1) {
              selected=objs[0];
            } else {
              _mSelected=objs.ToArray();
              foreach(var el in _mSelected) {
                el.Select(true);
              }
            }
          }
        } else {
          base.OnMouseUp(e);
        }
        move=false;
      } else {
        base.OnMouseUp(e);
      }
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
      base.OnRenderSizeChanged(sizeInfo);
      if(Width>0 && Height>0) {
        RenderBackground();
      }
    }

    private uiItem GetVisual(double x, double y) {
      int gs=LogramView.CellSize;

      List<uiItem> objs=new List<uiItem>();
      GeometryHitTestParameters parameters=new GeometryHitTestParameters(new RectangleGeometry(new Rect(x-gs/4, y-gs/4, gs/2, gs/2)));
      VisualTreeHelper.HitTest(this, null, new HitTestResultCallback((hr) => {
        var rez=(GeometryHitTestResult)hr;
        var vis=hr.VisualHit as uiItem;
        if(vis!=null) {
          objs.Add(vis);
        }
        return HitTestResultBehavior.Continue;
      }), parameters);
      uiItem ret=null;
      if(objs.Count>0) {
        ret=objs.FirstOrDefault(z => z is uiPin);
        if(ret==null) {
          ret=objs.FirstOrDefault(z => z is uiWire);
          if(ret==null) {
            ret=objs.FirstOrDefault(z => z is SchemaElement);
          }
        }
      }
      return ret;
    }

    public void mi_Click(object sender, RoutedEventArgs e) {
      e.Handled=true;
      MenuItem ci=sender as MenuItem;
      TopicView tv;
      Topic cur;
      if(ci==null || (tv=ci.DataContext as TopicView)==null || (cur=tv.ptr)==null) {
        return;
      }
      switch(((TopicView.ItemActionStr)ci.Tag).action) {
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
      case ItemAction.createByteArrDef:
        cur.Get<ByteArray>(ci.Header as string);
        break;
      case ItemAction.createObjectDef:
        cur.Get<object>(ci.Header as string);
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
    public void mi_Copy(object sender, RoutedEventArgs e) {
      IEnumerable<Topic> ms;
      Topic sel;
      if(_mSelected!=null) {
        ms=_mSelected.Select(z => z.GetModel()).Where(z => z!=null && z.valueType!=typeof(PiTracer));
      } else if(_selected!=null && (sel=_selected.GetModel())!=null && sel.valueType==typeof(PiStatement)) {
        ms=(new Topic[] { sel });
      } else {
        return;
      }
      XDocument doc=new XDocument(new XElement("root"));
      
      foreach(var t in ms) {
        Topic.Export(doc.Root, t);
      }
      var ws=this.model.children.Where(z => z.valueType==typeof(PiWire)).Cast<DVar<PiWire>>().Where(z => z!=null);
      foreach(var w in ws.Where(z => (ms.Any(z1 => z1==z.value.A) || ms.Any(z1 => z1==z.value.A.parent)) && (ms.Any(z2 => z2==z.value.B) || ms.Any(z2 => z2==z.value.B.parent)))) {
        Topic.Export(doc.Root, w);
      }

      string exp=doc.ToString(SaveOptions.OmitDuplicateNamespaces);
      Clipboard.SetText(exp);
    }
    public void mi_Paste(object sender, RoutedEventArgs e) {
      string inp=Clipboard.GetText();
      if(string.IsNullOrWhiteSpace(inp)) {
        return;
      }
      MemoryStream ms=new MemoryStream(Encoding.UTF8.GetBytes(inp));
      ms.Seek(0, SeekOrigin.Begin);
      try {
        using(var sr=new StreamReader(ms)) {
          Topic.Import(sr, this.model.path);
        }
      }
      catch(Exception ex) {
        Log.Error(ex.Message);
      }
    }
    public void mi_Trace(object sender, RoutedEventArgs e) {
      uiPin p=selected as uiPin;
      if(p==null) {
        return;
      }
      Topic pp;
      string name;

      if(p.owner is uiAlias) {
        pp=(p.owner as uiAlias).GetModel();
        name="?"+pp.name;
      } else {
        pp=p.GetModel();
        name=string.Format("?{0}?{1}", pp.parent.name, pp.name);
      }
      var td=model.Get<PiTracer>(name);
      td.saved=true;
      td.value=new PiTracer(pp.path);
    }
  }
}
