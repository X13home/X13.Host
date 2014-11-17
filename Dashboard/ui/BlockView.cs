using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace X13{
  internal class BlockView : DrawingVisual {
    static BlockView() {
      FtFont=new Typeface("Times New Roman");
    }
    public static Typeface FtFont { get; private set; }

    private LogramView _owner;
    internal ItemViewModel _model;
    public BlockView(LogramView owner, ItemViewModel model) {
      _owner=owner;
      _model=model;
      Render(3);
      _owner.AddVisual(this);
    }
    public void Render(int chLevel) {
      int gs=LogramView.CellSize;

      this.Offset=new Vector((1.0+_model.posX)*gs, (0.5+_model.posY)*gs);
      FormattedText head=new FormattedText(_model.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, FtFont, gs*0.6, Brushes.Black);
      double width=Math.Round(1+(head.WidthIncludingTrailingWhitespace*2-gs/2)/gs, 0)*gs;
      double height=3*gs;

      base.VisualBitmapScalingMode=BitmapScalingMode.HighQuality;
      using(DrawingContext dc=this.RenderOpen()) {
        Pen border=new Pen(Brushes.Black, 1);
        dc.DrawRectangle(Brushes.White, null, new Rect(-1, 2, width+4, height+gs-2));
        dc.DrawRectangle(Brushes.AliceBlue, border, new Rect(3, gs-0.5, width-2, height+1));
        dc.DrawText(head, new Point((width-head.WidthIncludingTrailingWhitespace)/2, 1));
      }
    }
  }
}
