using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X13 {
  internal class LogramView : Canvas {
    public static int CellSize=16;

    private ItemViewModel _vm;
    public List<Visual> _visuals;

    public LogramView()
      : base() {
      _visuals=new List<Visual>();
    }
    protected override void OnInitialized(EventArgs e) {
      _vm=this.DataContext as ItemViewModel;
      if(_vm!=null) {
        if(_vm.sizeX<=0) {
          _vm.sizeX=35;
        }
        this.Width=_vm.sizeX*CellSize;
        if(_vm.sizeY<=0) {
          _vm.sizeY=20;
        }
        this.Height=_vm.sizeY*CellSize;
        foreach(var ch in _vm.children) {
          var ui = new BlockView(this, ch);
        }
      }
      base.OnInitialized(e);
    }
    public void AddVisual(Visual item) {
      _visuals.Add(item);
      base.AddVisualChild(item);
      base.AddLogicalChild(item);
    }
    public void DeleteVisual(Visual item) {
      _visuals.Remove(item);
      base.RemoveVisualChild(item);
      base.RemoveLogicalChild(item);
    }
    protected override int VisualChildrenCount {
      get {
        return _visuals.Count;   // _backgroundVisual, _mSelectVisual
      }
    }
    protected override Visual GetVisualChild(int index) {
      return _visuals[index];
    }
    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e) {
      for(int i=_visuals.Count-1; i>=0; i--) {
        BlockView b=_visuals[i] as BlockView;
        if(b!=null) {
          var p=e.GetPosition(this);
          p.Offset(-b.Offset.X, -b.Offset.Y);
          if(b.ContentBounds.Contains(p) && b._model!=null) {
            Workspace.This.AddFile(b._model);
          }
        }
      }
      base.OnMouseLeftButtonUp(e);
    }
  }
}
