#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using X13.PLC;
using X13.WOUM;
using System.Windows.Input;
using System.Windows.Media.Imaging;


namespace X13.CC {
  internal abstract class uiItem : DrawingVisual {
    protected bool _selected;
    public abstract Topic GetModel();
    public virtual void Select(bool select) {
      if(_selected!=select) {
        _selected=select;
        Render(0);
      }
    }
    /// <summary>feel DrawingVisual</summary>
    /// <param name="chLevel">0 - locale, 1 - local & child, 2 - drag, 3- set position</param>
    public abstract void Render(int chLevel);
  }

  internal class uiPin : uiItem {
    private Vector _ownerOffset;
    private List<uiWire> _connections;
    public SchemaElement owner { get; private set; }
    /// <summary>0 - bidirectional, 1 - input, 2 - output</summary>
    public byte Direction { get; set; }
    private Topic model;

    public uiPin(SchemaElement owner, Topic model) {
      this.owner=owner;
      this.model=model;
      _connections=new List<uiWire>();
      this.brush=Brushes.LightGray;
    }

    public Brush brush { get; private set; }

    public void SetLocation(Vector center, int chLevel) {
      _ownerOffset=center;
      Render(chLevel);
    }
    public void AddConnection(uiWire w) {
      _connections.Add(w);
      model.saved=false;
    }
    public void RemoveConnection(uiWire w) {
      _connections.Remove(w);
      if(_connections.Count==0) {
        model.saved=true;
      }
    }
    public override void Render(int chLevel) {
      this.Offset=owner.Offset+_ownerOffset;
      switch(Type.GetTypeCode(model.valueType)) {
      case TypeCode.Object:
        this.brush=Brushes.Magenta;
        break;
      case TypeCode.String:
        this.brush=Brushes.Khaki;
        break;
      default:
        try {
          Decimal t=(Decimal)Convert.ChangeType(model.GetValue(), typeof(Decimal));
          if(t==0) {
            this.brush=Brushes.DarkGray;
          } else if(t==1) {
            this.brush=Brushes.Lime;
          } else {
            if((t%1.0m)!=0) {
              this.brush=new SolidColorBrush(Color.FromRgb(0, 40, 100));
            } else {
              this.brush=Brushes.Green;
            }
          }
        }
        catch(Exception) {
          this.brush=Brushes.LightGray;
        }
        break;
      }
      using(DrawingContext dc=this.RenderOpen()) {
        dc.DrawEllipse(this.brush, _selected?Schema.SelectionPen:null, new Point(0, 0), 3, 3);
      }
      if(chLevel>0) {
        foreach(uiWire w in _connections.ToArray()) {
          w.Render(chLevel);
        }
      }
    }
    public override Topic GetModel() {
      return model;
    }
  }

  internal class uiWire : uiItem {
    private Point _cur;
    private Schema _owner;
    private DVar<PiWire> model;
    private List<Point> _track=new List<Point>();

    public uiPin A { get; private set; }
    public uiPin B { get; private set; }

    public uiWire(DVar<PiWire> model, Schema owner) {
      _owner=owner;
      this.model=model;
      if(model.value.A is DVar<Topic>) {
        this.A=_owner._visuals.FirstOrDefault(z => (z is uiPin) && ((z as uiPin).owner is uiAlias) && ((z as uiPin).owner as uiAlias).model==model.value.A) as uiPin;
      } else {
        this.A=_owner._visuals.FirstOrDefault(z => (z is uiPin) && (z as uiPin).GetModel()==model.value.A) as uiPin;
      }
      if(model.value.B is DVar<Topic>) {
        this.B=_owner._visuals.FirstOrDefault(z => (z is uiPin) && ((z as uiPin).owner is uiAlias) && ((z as uiPin).owner as uiAlias).model==model.value.B) as uiPin;
      } else {
        this.B=_owner._visuals.FirstOrDefault(z => (z is uiPin) && (z as uiPin).GetModel()==model.value.B) as uiPin;
      }
      if(this.A!=null && this.B!=null) {
        A.AddConnection(this);
        B.AddConnection(this);
        Render(3);
        owner.AddVisual(this);
        model.changed+=model_changed;
        //} else {
        //  model.Remove();
      }
    }

    public uiWire(uiPin start, Schema owner) {
      _owner=owner;
      this.A=start;
      Render(3);
      owner.AddVisual(this);
    }
    public void Update(Point p) {
      _cur=p;
      Render(2);
    }
    public void SetFinish(uiPin finish) {
      B=finish;
      A.AddConnection(this);
      B.AddConnection(this);
      string name;
      for(int i=1; _owner.model.Exist(name=string.Format("W{0:X4}", i)); i++)
        ;
      model=_owner.model.Get<PiWire>(name);
      model.saved=true;
      model.value=new PiWire();
      uiAlias al=A.owner as uiAlias;
      if(al!=null) {
        model.value.A=al.model;
      } else {
        model.value.A=A.GetModel();
      }
      al=B.owner as uiAlias;
      if(al!=null) {
        model.value.B=al.model;
      } else {
        model.value.B=B.GetModel();
      }
      model.changed+=model_changed;
      if(B.Direction==1) {
        model.value.Direction=1;
      } else if(A.Direction==1) {
        model.value.Direction=2;
      } else {
        model.value.Direction=0;
      }
      Render(3);
    }
    public override void Render(int chLevel) {
      if(chLevel>1 && _track.Count>0) {
        _owner.MapRemove(this);
      }
      if(chLevel>2 && B!=null) {
        FindPath(_track);
      }

      if(_track.Count==0 || chLevel==2) {
        _track.Clear();
        _track.Add(new Point(A.Offset.X, A.Offset.Y));
        if(B!=null) {
          _track.Add(new Point(B.Offset.X, B.Offset.Y));
        } else {
          _track.Add(_cur);
        }
      }

      using(DrawingContext dc=this.RenderOpen()) {
        Pen pn=new Pen(A.brush, 2.0);
        for(int i=0; i<_track.Count-1; i++) {
          dc.DrawLine(_selected?Schema.SelectionPen:pn, _track[i], _track[i+1]);
        }
      }
    }
    private void model_changed(Topic sender, TopicChanged param) {
      if(sender==model && param.Art==TopicChanged.ChangeArt.Remove) {
        model.changed-=model_changed;
        A.RemoveConnection(this);
        B.RemoveConnection(this);
        this.Dispatcher.BeginInvoke(new Action(() => {
          _owner.DeleteVisual(this);
        }));
        model.Remove();
      }
    }
    public override Topic GetModel() {
      return model;
    }

    private static int[,] direction = new int[4, 2] { { -1, 0 }, { 0, -1 }, { 1, 0 }, { 0, 1 } };
    private void FindPath(List<Point> track) {
      int gs=LogramView.CellSize;
      PriorityQueue<PathFinderNode> mOpen= new PriorityQueue<PathFinderNode>(new ComparePFNode());
      List<PathFinderNode> mClose         = new List<PathFinderNode>();
      double mVert                  = 0;
      int mSearchLimit            = 3000;

      PathFinderNode parentNode;
      bool found  = false;
      int startX=(int)Math.Round(this.A.Offset.X/gs-1, 0);
      int startY=(int)Math.Round(this.A.Offset.Y/gs-1, 0);
      int finishX=(int)Math.Round(this.B.Offset.X/gs-1, 0);
      int finishY=(int)Math.Round(this.B.Offset.Y/gs-1, 0);
      mOpen.Clear();
      mClose.Clear();


      parentNode.G         = 0;
      parentNode.H         = 1;
      parentNode.F         = parentNode.G + parentNode.H;
      parentNode.X         = startX;
      parentNode.Y         = startY;
      parentNode.PX        = startX+Math.Sign(this.A.Offset.X-gs-startX*gs);
      parentNode.PY        = startY+Math.Sign(this.A.Offset.Y-gs-startY*gs);
      
      mOpen.Push(parentNode);
      while(mOpen.Count > 0) {
        parentNode = mOpen.Pop();

        if(parentNode.X == finishX && parentNode.Y == finishY) {
          mClose.Add(parentNode);
          found = true;
          break;
        }

        if(mClose.Count > mSearchLimit) {
          return;
        }

        mVert = (parentNode.Y - parentNode.PY);

        //Lets calculate each successors
        for(int i=0; i<4; i++) {
          PathFinderNode newNode;
          newNode.X = parentNode.X + direction[i, 0];
          newNode.Y = parentNode.Y + direction[i, 1];
          int newG=this.GetWeigt(newNode.X, newNode.Y, mVert!=0);
          if(newG>100)
            continue;
          newG+= parentNode.G;


          if(newG == parentNode.G) {
            //Unbrekeable
            continue;
          }
          // Дополнительная стоимиость поворотов
          if(((newNode.X - parentNode.X)!=0 && mVert!=0) || ((newNode.Y - parentNode.Y)!=0 && mVert==0)) {
            newG += 3; // 20;
          }

          int foundInOpenIndex = -1;
          for(int j=0; j<mOpen.Count; j++) {
            if(mOpen[j].X == newNode.X && mOpen[j].Y == newNode.Y) {
              foundInOpenIndex = j;
              break;
            }
          }
          if(foundInOpenIndex != -1 && mOpen[foundInOpenIndex].G <= newG)
            continue;

          int foundInCloseIndex = -1;
          for(int j=0; j<mClose.Count; j++) {
            if(mClose[j].X == newNode.X && mClose[j].Y == newNode.Y) {
              foundInCloseIndex = j;
              break;
            }
          }
          if(foundInCloseIndex != -1 && mClose[foundInCloseIndex].G <= newG)
            continue;

          newNode.PX      = parentNode.X;
          newNode.PY      = parentNode.Y;
          newNode.G       = newG;

          newNode.H       = 2+Math.Sign(Math.Abs(newNode.X - finishX) + Math.Abs(newNode.Y - finishY)-Math.Abs(newNode.PX - finishX) - Math.Abs(newNode.PY - finishY));
          newNode.F       = newNode.G + newNode.H;

          mOpen.Push(newNode);
        }

        mClose.Add(parentNode);

      }

      if(found) {
        track.Clear();
        PathFinderNode fNode = mClose[mClose.Count - 1];
        track.Add(new Point(gs+finishX*gs, gs+finishY*gs));
        for(int i=mClose.Count - 1; i>=0; i--) {
          if(fNode.PX == mClose[i].X && fNode.PY == mClose[i].Y || i == mClose.Count - 1) {
            bool vert=(fNode.PY-fNode.Y)!=0;
            if(_owner.MapGet(vert, fNode.PX, fNode.PY)==null) {
              _owner.MapSet(vert, fNode.PX, fNode.PY, this);
            }
            if(_owner.MapGet(vert, fNode.X, fNode.Y)==null) {
              _owner.MapSet(vert, fNode.X, fNode.Y, this);
            }
            fNode = mClose[i];
            if(track[0].X!=gs+fNode.PX*gs && track[0].Y!=gs+fNode.PY*gs) {
              track.Insert(0, new Point(gs+fNode.X*gs, gs+fNode.Y*gs));
            }
          }
        }
        if(track[0].X!=startX*gs+gs || track[0].Y!=startY*gs+gs) {
          track.Insert(0, new Point(gs+startX*gs, gs+startY*gs));
        }
      }
      // Visu
      //using(DrawingContext dc=this.RenderOpen()) {
      //  for(int i=0; i<mClose.Count; i++) {
      //    FormattedText txt=new FormattedText(mClose[i].F.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LogramView.FtFont, gs*0.3, Brushes.Violet);
      //    dc.DrawText(txt, new Point(gs/2+mClose[i].X*gs, gs/2+mClose[i].Y*gs));
      //    dc.DrawLine(new Pen(Brushes.RosyBrown, 1), new Point(gs+mClose[i].X*gs, gs+mClose[i].Y*gs), new Point(gs+mClose[i].PX*gs, gs+mClose[i].PY*gs));
      //  }
      //  Pen pn=new Pen(A.brush, 2.0);
      //  for(int i=0; i<_track.Count-1; i++) {
      //    dc.DrawLine(_selected?Schema.SelectionPen:pn, _track[i], _track[i+1]);
      //  }
      //}
    }
    private int GetWeigt(int X, int Y, bool vert) {
      int gs=LogramView.CellSize;

      if(X<0 || Y<0 || X*gs>=_owner.Width-gs || Y*gs>=_owner.Height-gs) {
        return 256;
      }
      var it=_owner.MapGet(vert, X, Y);
      if(it is SchemaElement) {
        it=_owner.MapGet(!vert, X, Y);
      }
      if(it==null) {
        return 2;
      } else if(it is uiPin) {
        if(it==this.A || it==this.B) {
          return 1;
        }
        return 101;
      } else if(it is uiWire) {
        var w=it as uiWire;
        if(w.A==this.A || w.A==this.B || w.B==this.A || w.B==this.B) {
          return 1;
        }
        return 101;
      } else if(it is SchemaElement) {
        return 101;
      }
      return 3;
    }

    private class ComparePFNode : IComparer<PathFinderNode> {
      public int Compare(PathFinderNode x, PathFinderNode y) {
        if(x.F > y.F)
          return 1;
        else if(x.F < y.F)
          return -1;
        return 0;
      }
    }
    private struct PathFinderNode {
      public int F;
      public int G;
      public int H;  // f = gone + heuristic
      public int X;
      public int Y;
      public int PX; // Parent
      public int PY;
    }
  }

  internal abstract class SchemaElement : uiItem {
    public Vector OriginalLocation { get; protected set; }
    public abstract void SetLocation(Vector loc, bool save);
  }

  internal class uiAlias : SchemaElement {
    private Schema _owner;
    private uiPin _pin;
    private int _oldX=-1;
    private int _oldY=-1;
    private int _oldH=0;

    public readonly DVar<Topic> model;

    public uiAlias(DVar<Topic> model, Schema owner) {
      this.model=model;
      _owner=owner;
      this._pin=new uiPin(this, model.value);
      Render(3);
      _owner.AddVisual(this);
      _owner.AddVisual(_pin);
      model.changed+=ModelChanged;
      model.Subscribe("_location", LocationChanged);
      if(model.value!=null) {
        model.value.changed+=value_changed;
      }
    }

    private void value_changed(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Value && sender!=null && sender==model.value) {
        _pin.Dispatcher.BeginInvoke(new Action<int>(_pin.Render), System.Windows.Threading.DispatcherPriority.DataBind, 1);
      }
    }

    public override void SetLocation(Vector loc, bool save) {
      if(save) {
        int gs=LogramView.CellSize;
        int topCell=(int)(loc.Y-gs/2)/gs;
        if(topCell<0) {
          topCell=0;
        }
        int leftCell=(int)(loc.X)/gs;
        if(leftCell<0) {
          leftCell=0;
        }
        var sLoc=model.Get<uint>("_location");
        sLoc.saved=true;
        sLoc.value=(uint)((leftCell<<16) | (ushort)topCell);
      } else {
        if(_oldX>=0 && _oldY>=0) {
          for(int inH=_oldH; inH>=0; inH--) {
            _owner.MapSet(true, _oldX, _oldY+inH, null);
            _owner.MapSet(false, _oldX, _oldY+inH, null);
          }
        }
        _oldX=-1;
        _oldY=-1;
        this.Offset=loc;
        _pin.Render(2);
      }
    }

    public override void Render(int chLevel) {
      uint l=model.Get<uint>("_location");
      int gs=LogramView.CellSize;
      double height=0;
      base.OriginalLocation=new Vector((0.5+(short)(l>>16))*gs, (1.0+(short)l)*gs);
      this.Offset=OriginalLocation;

      using(DrawingContext dc=this.RenderOpen()) {
        FormattedText ft=new FormattedText(model.name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LogramView.FtFont, gs*0.6, Brushes.Black);
        height=Math.Round((ft.WidthIncludingTrailingWhitespace+gs*1.5)/gs, 0)*gs;
        dc.DrawRoundedRectangle(Brushes.AliceBlue, _selected?Schema.SelectionPen:new Pen(Brushes.Black, 1), new Rect(0, 0, gs, height-3), gs/4, gs/4);
        dc.PushTransform(new RotateTransform(-90, 5, height-gs/2));
        ft.MaxTextHeight=gs-3;
        ft.MaxTextWidth=height-gs/2-5;
        dc.DrawText(ft, new Point(5, height-gs/2-3));
        dc.Pop();
      }
      if(chLevel==3) {
        _oldY=(short)l;
        _oldX=(int)(l>>16);
        _oldH=(int)height/gs;
        for(int inH=_oldH; inH>=0; inH--) {
          _owner.MapSet(true, _oldX, _oldY+inH, this);
          _owner.MapSet(false, _oldX, _oldY+inH, this);
        }
        _owner.MapSet(true, _oldX, _oldY+_oldH, _pin);
      }

      if(chLevel>1) {
        _pin.SetLocation(new Vector(gs/2, height-3), chLevel);
      }
    }
    private void ModelChanged(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Add) {   // rename
        this.Dispatcher.BeginInvoke(new Action<int>(this.Render), 2);
      } else if(param.Art==TopicChanged.ChangeArt.Remove) {
        if(model.value!=null) {
          model.value.Unsubscribe("", value_changed);
        }
        this.Dispatcher.BeginInvoke(new Action(this.Remove));
      } else {
        this.Dispatcher.BeginInvoke(new Action<int>(this.Render), System.Windows.Threading.DispatcherPriority.DataBind, 3);
      }
    }
    private void LocationChanged(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Value) {
        this.Dispatcher.BeginInvoke(new Action<int>(this.Render), System.Windows.Threading.DispatcherPriority.DataBind, 3);
      }
    }
    private void Remove() {
      _owner.DeleteVisual(_pin);
      _owner.DeleteVisual(this);
    }

    public override Topic GetModel() {
      return model;
    }
  }

  internal class uiStatement : SchemaElement {
    private const int MAX_PINS=10;
    private List<uiPin> _pins;
    private Schema _owner;
    private int _oldX=-1;
    private int _oldY=-1;
    private int _oldW=0;
    private int _oldH=0;

    public uiStatement(DVar<PiStatement> model, Schema owner) {
      this._owner=owner;
      this.model=model;
      _pins=new List<uiPin>();
      model.changed+=model_changed;
      model.Subscribe("+", PinChanged);
      _owner.AddVisual(this);
      foreach(Topic mp in model.children.Where(z => !z.name.StartsWith("_"))) {
        uiPin p=new uiPin(this, mp);
        _pins.Add(p);
        _owner.AddVisual(p);
      }
      Render(3);
    }

    public readonly DVar<PiStatement> model;

    private void model_changed(Topic sender, TopicChanged param) {
        if(param.Art==TopicChanged.ChangeArt.Add) {
          this.Dispatcher.BeginInvoke(new Action(() => Render(3)));
        } else if(param.Art==TopicChanged.ChangeArt.Remove) {
          model.changed-=model_changed;
          model.Unsubscribe("+", PinChanged);
          this.Dispatcher.BeginInvoke(new Action(this.Remove));
          model.Remove();
        }
      }
    private void PinChanged(Topic sender, TopicChanged param) {
        if(!sender.name.StartsWith("_")) {
          this.Dispatcher.BeginInvoke(new Action(() => {
            uiPin p=_pins.FirstOrDefault(z => z.GetModel()==sender);
            if(param.Art!=TopicChanged.ChangeArt.Remove) {
              if(p==null) {
                sender.saved=true;
                p=new uiPin(this, sender);
                _pins.Add(p);
                _owner.AddVisual(p);
                Render(1);
              }
              p.Render(1);
            } else {
              if(p!=null) {
                _owner.DeleteVisual(p);
                _pins.Remove(p);
                Render(1);
              }
            }
          }));
        } else if(sender.parent==model && sender.name=="_location") {
          this.Dispatcher.BeginInvoke(new Action<int>(this.Render), System.Windows.Threading.DispatcherPriority.DataBind, 3);
        }
    }
    public override void Render(int chLevel) {
      int gs=LogramView.CellSize;

      DVar<string> declarer;
      Topic dt;
      if(!model.Exist("_declarer", out dt) || (declarer=Topic.root.Get<string>("/system/declarers/" + (dt as DVar<string>).value))==null) {
        return;
      }
      uint l=model.Get<uint>("_location");
      base.OriginalLocation=new Vector((1.0+(short)(l>>16))*gs, (0.5+(short)l)*gs);
      this.Offset=OriginalLocation;
      FormattedText head=new FormattedText(model.name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LogramView.FtFont, gs*0.6, Brushes.Black);
      FormattedText[] textIp=new FormattedText[MAX_PINS];
      uiPin[] pinIp=new uiPin[MAX_PINS];
      int cntIp=0;
      FormattedText[] textOp=new FormattedText[MAX_PINS];
      uiPin[] pinOp=new uiPin[MAX_PINS];
      int cntOp=0;
      int pos=0;
      double wi=0;
      double wo=0;
      foreach(var p in _pins) {
        DVar<string> pinDecl=declarer.all.FirstOrDefault(z => z.name==p.GetModel().name) as DVar<string>;
        if(pinDecl==null) {
          continue;
        }
        char pc=pinDecl.value[0];
        double cw;
        if(pc>='A' && pc<=(char)('A'+MAX_PINS)) {
          pos=pc-'A';
          if(cntIp<pos+1) {
            cntIp=pos+1;
          }
          pinIp[pos]=p;
          p.Direction=1; // input
          textIp[pos]=new FormattedText(p.GetModel().name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LogramView.FtFont, gs*0.6, Brushes.Black);
          cw=4+textIp[pos].WidthIncludingTrailingWhitespace;
          if(pos==0) {
            cw+=9;
          }
          wi=Math.Max(wi, cw);
        } else if(pc>='a' && pc<=(char)('a'+MAX_PINS)) {
          pos=pc-'a';
          if(cntOp<pos+1) {
            cntOp=pos+1;
          }
          pinOp[pos]=p;
          p.Direction=2; // output
          textOp[pos]=new FormattedText(p.GetModel().name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LogramView.FtFont, gs*0.6, Brushes.Black);
          cw=4+textOp[pos].WidthIncludingTrailingWhitespace;
          if(pos==0) {
            cw+=9;
          }
          wo=Math.Max(wo, cw);
        } else {
          continue;
        }
      }
      wi=Math.Round((2*wi)/gs, 0)*gs/2;
      wo=Math.Round((2*wo)/gs, 0)*gs/2;
      double width=Math.Round(Math.Max(head.WidthIncludingTrailingWhitespace*2-gs/2, wi+wo+gs)/gs, 0)*gs;
      double height=Math.Max(cntIp*gs, cntOp*gs);
      if(height==0) {
        Log.Error("{0} has no pins", model.path);
        return;
      }
      if(chLevel==3) {
        _oldY=(short)l;
        _oldX=(int)(l>>16);
        _oldW=(int)width/gs;
        _oldH=1+(int)height/gs;
        for(int inW=_oldW; inW>=0; inW--) {
          for(int inH=_oldH-1; inH>=0; inH--) {
            _owner.MapSet(true, _oldX+inW, _oldY+inH, this);
            _owner.MapSet(false, _oldX+inW, _oldY+inH, this);
          }
        }
      }
      base.VisualBitmapScalingMode=BitmapScalingMode.HighQuality;
      using(DrawingContext dc=this.RenderOpen()) {
        Pen border=_selected?Schema.SelectionPen:new Pen(Brushes.Black, 1);
        dc.DrawRectangle(Brushes.White, null, new Rect(-1, 2, width+4, height+gs-2));
        dc.DrawRectangle(Brushes.AliceBlue, border, new Rect(3, gs-0.5, wo>0?width-6:width-2, height+1));
        dc.DrawText(head, new Point((width-head.WidthIncludingTrailingWhitespace)/2, 1));
        BitmapImage ico=new BitmapImage(new Uri("pack://application:,,"+declarer.value));
        dc.DrawRectangle(null, border, new Rect(wi, gs-0.5, gs+1, gs+1));
        dc.DrawImage(ico, new Rect(wi+0.5, gs, gs, gs));
        int i;
        for(i=0; i<cntIp; i++) {
          if(textIp[i]!=null && pinIp[i]!=null) {
            dc.DrawText(textIp[i], new Point(7, (i+1)*gs+2));
            if(chLevel==3) {
              _owner.MapSet(false, _oldX, _oldY+1+i, pinIp[i]);
            }
          }
        }
        int inW=(int)width/gs;
        for(i=0; i<cntOp; i++) {
          if(textOp[i]!=null && pinOp[i]!=null) {
            dc.DrawText(textOp[i], new Point(width-7-textOp[i].WidthIncludingTrailingWhitespace, (i+1)*gs+2));
            if(chLevel==3) {
              _owner.MapSet(false, _oldX+inW, _oldY+1+i, pinOp[i]);
            }
          }
        }
      }
      if(chLevel>0) {
        int i;
        for(i=0; i<cntIp; i++) {
          if(pinIp[i]!=null) {
            pinIp[i].SetLocation(new Vector(3, i*gs+gs*1.5), chLevel);
          }
        }
        for(i=0; i<cntOp; i++) {
          if(pinOp[i]!=null) {
            pinOp[i].SetLocation(new Vector(width-3, i*gs+gs*1.5), chLevel);
          }
        }
      }
    }
    public override void SetLocation(Vector loc, bool save) {
      int gs=LogramView.CellSize;
      if(save) {
        int topCell=(int)(loc.Y)/gs;
        if(topCell<0) {
          topCell=0;
        }
        int leftCell=(int)(loc.X-gs/2)/gs;
        if(leftCell<0) {
          leftCell=0;
        }
        var sLoc=model.Get<uint>("_location");
        var actLoc=(uint)((leftCell<<16) | (ushort)topCell);
        if(actLoc==sLoc.value) {    // refresh wires
          this.Dispatcher.BeginInvoke(new Action<int>(this.Render), System.Windows.Threading.DispatcherPriority.DataBind, 3);
        } else {
          sLoc.saved=true;
          sLoc.value=actLoc;
        }
      } else {
        if(_oldX>=0 && _oldY>=0) {
          for(int inW=_oldW; inW>=0; inW--) {
            for(int inH=_oldH-1; inH>=0; inH--) {
              _owner.MapSet(true, _oldX+inW, _oldY+inH, null);
              _owner.MapSet(false, _oldX+inW, _oldY+inH, null);
            }
          }
        }
        _oldX=-1;
        _oldY=-1;
        this.Offset=loc;
        foreach(var p in _pins) {
          p.Render(2);
        }
      }
    }
    private void Remove() {
      foreach(var p in _pins) {
        _owner.DeleteVisual(p);
      }
      _owner.DeleteVisual(this);
    }

    public override Topic GetModel() {
      return model;
    }
  }
}