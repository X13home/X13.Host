using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;

namespace X13.Agent3 {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    private Timer _1sek;
    private bool _setted;
    private DateTime _today;
    private string _lfPath;
    private DateTime _firstDT;

    public MainWindow() {
      if(!Directory.Exists("../log")) {
        Directory.CreateDirectory("../log");
      }
      Log.Write += Log_Write;
      AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

      TopicSrc.Get("/local");
      InitializeComponent();
      _1sek = new Timer(Tick, null, 500, 1000);
    }

    private void Window_Closed(object sender, EventArgs e) {
      TopicSrc.Close();
      Thread.Sleep(100);
      Log.Finish();
    }

    private void Tick(object o) {
      DateTime nowDT = DateTime.Now;

      TopicSrc.Get("/local/vars/TimeLong", true).value = nowDT.ToLongTimeString();
      if(nowDT.Second == 0 || !_setted) {
        Transport.Update();
        for(int i = 0; i < 7; i++) {
          var t = Transport.At(i);
          if(t != null) {
            TopicSrc.Get(string.Format("/local/vars/Route{0}Info", i), true).value = string.Format("{0:HH:mm}    {1}", t.dt, t.route.name);
            TopicSrc.Get(string.Format("/local/vars/Route{0}Wait", i), true).value = string.Format("{0:f0} мин", (t.dt - nowDT).TotalMinutes);
          } else {
            TopicSrc.Get(string.Format("/local/vars/Route{0}Info", i), true).value = string.Empty;
            TopicSrc.Get(string.Format("/local/vars/Route{0}Wait", i), true).value = string.Empty;
          }
        }
        if(nowDT.Minute == 0 || !_setted) {
          ThreadPool.QueueUserWorkItem(GetWeatherForecast);
          if(nowDT.Hour == 0 || !_setted) {
            TopicSrc.Get("/local/vars/DateLong", true).value = nowDT.ToLongDateString();
            _today = nowDT.Date;
            this.Dispatcher.Invoke(new Action<DateTime>(this.DrawCalender), _today);
            if(!_setted) {
              _setted = true;
            }
          }
        }
      }
    }

    private const int wtsCount = 140;
    private void GetWeatherForecast(object o) {
      double xMax = 0;
      for(int tr = 0; tr < 3; tr++) {
        try {
          HttpWebRequest myHttpWebRequest = (HttpWebRequest)HttpWebRequest.Create("http://informer.gismeteo.ru/xml/10865_1.xml");
          HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
          StreamReader myStreamReader = new StreamReader(myHttpWebResponse.GetResponseStream(), Encoding.GetEncoding(1251));
          XmlDocument doc = new XmlDocument();
          doc.LoadXml(myStreamReader.ReadToEnd());
          int i = 0;
          DateTime curDT = DateTime.Today.AddHours(DateTime.Now.Hour);
          var t = new CubicSpline();
          var pr = new CubicSpline();
          string[] dict0 = new string[] { "n.moon", "d.sun", "d.sun", "n.moon" };
          string[] dict1 = new string[] { "", ".c1", ".c2", ".c3", ".r2", ".r4", ".s2", "s4", ".r3.st", "", "", ".r1", ".r3", ".s1", ".s3", ".r2", "", "" };
          string[] dictWind = new string[] { "С {0} м/с", "СВ {0} м/с", "В {0} м/с", "ЮВ {0} м/с", "Ю {0} м/с", "ЮЗ {0} м/с", "З {0} м/с", "СЗ {0} м/с" };
          var cache = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.CacheIfAvailable);
          foreach(XmlNode n in doc.SelectNodes("/MMWEATHER/REPORT/TOWN/FORECAST")) {
            if(i > 3)
              break;
            string wfDTStr = string.Format("{0}-{1}-{2} {3}:00", n.Attributes["year"].Value, n.Attributes["month"].Value, n.Attributes["day"].Value, n.Attributes["hour"].Value);
            DateTime wfDT = DateTime.ParseExact(wfDTStr, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            double x = 48.0 + (wfDT - curDT).TotalHours;
            StringBuilder url = new StringBuilder(@"http://i.gismeteo.com/static/images/icons/new/");
            url.Append(dict0[int.Parse(n.Attributes["tod"].Value)]);
            XmlNode n1 = n.SelectSingleNode("PHENOMENA");
            int c = int.Parse(n1.Attributes["cloudiness"].Value);
            int p = int.Parse(n1.Attributes["precipitation"].Value);
            int rp = int.Parse(n1.Attributes["rpower"].Value);
            int sp = int.Parse(n1.Attributes["spower"].Value);
            if(rp == 0)
              p += 7;
            url.Append(dict1[c]);
            url.Append(dict1[p]);
            if(sp == 1)
              url.Append(".st");
            url.Append(".png");
            TopicSrc.Get(string.Format("/local/vars/weather/wf{0}img", i), true).value = url.ToString();
            if(x >= 50) {
              n1 = n.SelectSingleNode("TEMPERATURE");
              t.AddNode(x, (double.Parse(n1.Attributes["max"].Value) + double.Parse(n1.Attributes["min"].Value)) / 2.0);

              n1 = n.SelectSingleNode("PRESSURE");
              pr.AddNode(x, (double.Parse(n1.Attributes["max"].Value) + double.Parse(n1.Attributes["min"].Value)) / 2.0);
            }
            n1 = n.SelectSingleNode("WIND");
            TopicSrc.Get(string.Format("/local/vars/weather/wf{0}w", i), true).value = string.Format(dictWind[int.Parse(n1.Attributes["direction"].Value)], (int.Parse(n1.Attributes["max"].Value) + int.Parse(n1.Attributes["min"].Value)) / 2);
            xMax = x;
            i++;
          }
          string wlPath = TopicSrc.Get("/local/cfg/Broker/_path", true).value as string;
          if(string.IsNullOrEmpty(wlPath)) {
            wlPath = @"..\log\weather.log";
          } else {
            wlPath = System.IO.Path.Combine(wlPath, @"..\log\weather.log");
          }

          if(File.Exists(wlPath)) {
            var csv = File.ReadAllLines(wlPath);
            for(int l = csv.Length - 1; l > 0; l--) {
              var c_i = csv[l].Split(',');
              DateTime wfDT = DateTime.ParseExact(c_i[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).AddMinutes(-30);
              double x = 48.0 + (wfDT - curDT).TotalHours;
              double y;
              if(double.TryParse(c_i[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) {
                t.AddNode(x, y);
              }
              if(double.TryParse(c_i[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) {
                pr.AddNode(x, y);
              }
            }
          }
          int cnt = (int)(xMax * 2) - 1;
          double[] t_v = new double[cnt];
          double[] p_v = new double[cnt];

          for(i = 0; i < cnt; i++) {
            t_v[i] = t.Func(i / 2.0);
            p_v[i] = pr.Func(i / 2.0);
          }
          Dispatcher.Invoke(new Action<double[], double[]>(DrawT_Pr), System.Windows.Threading.DispatcherPriority.Background, t_v, p_v);
          break;
        }
        catch(Exception) {
          Thread.Sleep(5000);
        }
      }
    }

    private void DrawT_Pr(double[] t, double[] p) {
      int cnt = t.Length;
      int i = 0;
      double tMin = t.Skip(i).Min();
      double tMax = t.Skip(i).Max();
      //double tCur=(double)Topic.root.Get<decimal>("/Public/Temperature").value;
      //if((tMax+tMin)/2<tCur) {
      //  tMax=tCur*2-tMin;
      //} else {
      //  tMin=tCur*2-tMax;
      //}

      double pMin = p.Skip(i).Min();
      double pMax = p.Skip(i).Max();
      //double pCur=(double)Topic.root.Get<decimal>("/Public/Pressure").value;
      //if((pMax+pMin)/2<pCur) {
      //  pMax=pCur*2-pMin;
      //} else {
      //  pMin=pCur*2-pMax;
      //}

      double xStep = (wtDiagram.ActualWidth - 80) / (wtsCount + 4);
      double tStep = (wtDiagram.ActualHeight - 10) / (tMax - tMin);
      double pStep = (wtDiagram.ActualHeight - 10) / (pMax - pMin);
      double yOffset = wtDiagram.ActualHeight - 5;
      PathFigureCollection tPathFigureCollection = new PathFigureCollection();
      PathFigureCollection pPathFigureCollection = new PathFigureCollection();

      this.wtDiagram.Children.Clear();

      AddText2wtDiagram(string.Format("{0:F1} °C", tMin), wtDiagram.ActualWidth - 35, 100, Brushes.DarkBlue);
      AddText2wtDiagram(string.Format("{0:F1} °C", (tMin + tMax) / 2), wtDiagram.ActualWidth - 35, 50, Brushes.DarkBlue);
      AddText2wtDiagram(string.Format("{0:F1} °C", tMax), wtDiagram.ActualWidth - 35, 0, Brushes.DarkBlue);
      AddText2wtDiagram(string.Format("{0:F0} mmHg", pMin), 0, 100, Brushes.DarkGreen);
      AddText2wtDiagram(string.Format("{0:F0} mmHg", (pMin + pMax) / 2), 0, 50, Brushes.DarkGreen);
      AddText2wtDiagram(string.Format("{0:F0} mmHg", pMax), 0, 0, Brushes.DarkGreen);

      this.wtDiagram.Children.Add(new Line() { X1 = 40, Y1 = 5, X2 = wtDiagram.ActualWidth - 40, Y2 = 6, Stroke = Brushes.Black, StrokeThickness = 1.5 });
      this.wtDiagram.Children.Add(new Line() { X1 = 40, Y1 = 56, X2 = wtDiagram.ActualWidth - 40, Y2 = 56, Stroke = Brushes.Black, StrokeThickness = 1.5 });
      this.wtDiagram.Children.Add(new Line() { X1 = 40, Y1 = 106, X2 = wtDiagram.ActualWidth - 40, Y2 = 106, Stroke = Brushes.Black, StrokeThickness = 1.5 });

      for(; i < cnt - 1; i++) {
        AddSegment(t, tMin, xStep, tStep, yOffset, tPathFigureCollection, i);
        AddSegment(p, pMin, xStep, pStep, yOffset, pPathFigureCollection, i);
      }

      wtDiagram.Children.Add(new System.Windows.Shapes.Path() {
        Stroke = Brushes.DarkBlue,
        StrokeThickness = 1.5,
        Data = new PathGeometry() {
          Figures = tPathFigureCollection
        }
      });
      wtDiagram.Children.Add(new System.Windows.Shapes.Path() {
        Stroke = Brushes.DarkGreen,
        StrokeThickness = 1.5,
        Data = new PathGeometry() {
          Figures = pPathFigureCollection
        }
      });
      DateTime curH = DateTime.Today.AddHours(DateTime.Now.Hour);
      tbWeath1.Text = curH.AddHours(-45).ToString("HH:mm");
      tbWeath2.Text = curH.AddHours(-39).ToString("HH:mm");
      tbWeath3.Text = curH.AddHours(-33).ToString("HH:mm");
      tbWeath4.Text = curH.AddHours(-27).ToString("HH:mm");
      tbWeath5.Text = curH.AddHours(-21).ToString("HH:mm");
      tbWeath6.Text = curH.AddHours(-15).ToString("HH:mm");
      tbWeath7.Text = curH.AddHours(-9).ToString("HH:mm");
      tbWeath8.Text = curH.AddHours(-3).ToString("HH:mm");
      tbWeath9.Text = curH.AddHours(3).ToString("HH:mm");
      tbWeath10.Text = curH.AddHours(9).ToString("HH:mm");
      tbWeath11.Text = curH.AddHours(15).ToString("HH:mm");
      tbWeath12.Text = curH.AddHours(21).ToString("HH:mm");
      //wtDiagram.Children.Add(new Line() { X1=40+(wtDiagram.ActualWidth-80)/1.5, Y1=0, X2=40+(wtDiagram.ActualWidth-80)/1.5, Y2=109, Stroke=Brushes.Black, StrokeThickness=2 });
      //wtDiagram.Children.Add(new Line() { X1=40+(wtDiagram.ActualWidth-80)/3, Y1=0, X2=40+(wtDiagram.ActualWidth-80)/3, Y2=109, Stroke=Brushes.Black, StrokeThickness=2 });
    }
    private void AddText2wtDiagram(string text, double x, double y, Brush br) {
      TextBlock textBlock = new TextBlock();
      textBlock.Text = text;
      textBlock.Foreground = br;
      textBlock.FontFamily = this.FontFamily;
      textBlock.FontSize = 8;
      Canvas.SetLeft(textBlock, x);
      Canvas.SetTop(textBlock, y);
      wtDiagram.Children.Add(textBlock);
    }
    private void AddSegment(double[] t, double tMin, double xStep, double tStep, double yOffset, PathFigureCollection tPathFigureCollection, int i) {
      tPathFigureCollection.Add(
        new PathFigure(
          new Point(40 + xStep / 2 + xStep * (i), yOffset - (t[i] - tMin) * tStep),
          new PathSegment[]{ new LineSegment(
              new Point(40+xStep/2+xStep*(i+1), yOffset-(t[i+1]-tMin)*tStep), 
              true) },
          false));

    }

    private void DrawCalender(DateTime DT) {
      DateTime today = DateTime.Now.Date;
      DateTime cur = new DateTime(DT.AddMonths(-1).Year, DT.AddMonths(-1).Month, 1);
      List<Holiday> holidays = new List<Holiday>(8);
      cur = cur.AddDays(1 - (cur.DayOfWeek == DayOfWeek.Sunday ? 7 : (double)cur.DayOfWeek));
      int month = 0;
      int jPrev = 0;

      Brush bBrush;
      Brush fBrush;
      MonthColor(cur, DT, out bBrush, out fBrush);

      Label wt;
      grCalender.Children.Clear();
      int i, j;
      for(j = 0; j < 15; j++) {
        for(i = 0; i < 7; i++) {
          if(j == 0) {
            wt = new Label();
            wt.FontWeight = FontWeights.Heavy;
            wt.SetValue(Label.ContentProperty, (object)(cur.ToString("ddd")));
            grCalender.Children.Add(wt);
            Grid.SetColumn(wt, 0);
            Grid.SetRow(wt, i + 1);
          }
          if(cur.Day == 1) {
            MonthColor(cur, DT, out bBrush, out fBrush);
          }

          if(i == 0 && cur.Month != month) {
            if(month != 0) {  // && cur.Month>=_today.Month
              var bu = new Button();
              int offset = (cur.Year - DT.Year) * 12 + cur.Month - DT.Month - 1;
              bu.FontWeight = FontWeights.Heavy;
              if(offset < 0) {
                bu.Content = "◄  " + cur.AddMonths(-1).ToString("MMMM yy");
              } else if(offset > 0) {
                bu.Content = cur.AddMonths(-1).ToString("MMMM yy") + "  ►";
              } else {
                bu.Content = cur.AddMonths(-1).ToString("MMMM yy");
              }
              bu.SetValue(Label.HorizontalContentAlignmentProperty, (object)HorizontalAlignment.Center);
              Brush f, b;
              MonthColor(cur.AddMonths(-1), DT, out b, out f);
              bu.Foreground = f;
              bu.Background = b;
              bu.BorderBrush = Brushes.Black;
              bu.BorderThickness = new Thickness(0, 0, 0, 0);
              bu.Click += Month_Click;
              bu.Tag = offset;

              grCalender.Children.Add(bu);
              Grid.SetColumn(bu, jPrev);
              Grid.SetRow(bu, 0);
              Grid.SetColumnSpan(bu, j - jPrev + 1);
            }
            month = cur.Month;
            jPrev = j + 1;
          }
          if(i == 0 && j == 14) {
            wt = new Label();
            wt.Background = bBrush;
            wt.BorderBrush = Brushes.Black;
            wt.BorderThickness = new Thickness(1.0, 0, 0, 0);
            grCalender.Children.Add(wt);
            Grid.SetColumn(wt, jPrev);
            Grid.SetRow(wt, 0);
          }
          Holiday.HolidayType hType;
          var h = Holiday.Find(cur);
          if(h != null) {
            if(h.begin == cur) {
              holidays.Add(h);
            }
            hType = h.type;
          } else {
            hType = Holiday.HolidayType.none;
          }
          wt = new Label();
          wt.SetValue(Label.ContentProperty, (object)(cur.ToString("dd")));
          wt.SetValue(Label.HorizontalContentAlignmentProperty, (object)HorizontalAlignment.Center);
          switch(hType) {
          case Holiday.HolidayType.legal:
            wt.Foreground = new SolidColorBrush(Colors.Red);
            break;
          case Holiday.HolidayType.school:
            wt.Foreground = new SolidColorBrush(Colors.Blue);
            break;
          case Holiday.HolidayType.memo:
            wt.Foreground = new SolidColorBrush(Colors.Green);
            break;
          case Holiday.HolidayType.termin:
            wt.Foreground = new SolidColorBrush(Colors.Orange);
            break;
          default:
            wt.Foreground = fBrush;
            break;
          }
          wt.Background = bBrush;
          wt.BorderBrush = Brushes.Black;
          if(cur.Day == 1 && i != 0) {
            wt.BorderThickness = new Thickness(1.0, 1.0, 0, 0);
          } else if(cur.Day < 8) {
            wt.BorderThickness = new Thickness(1.0, 0, 0, 0);
          }
          if(today == cur) {
            wt.BorderThickness = new Thickness(1);
          }
          wt.Tag = cur;
          wt.MouseDown += Day_MouseDown;
          grCalender.Children.Add(wt);
          Grid.SetColumn(wt, j + 1);
          Grid.SetRow(wt, i + 1);
          cur = cur.AddDays(1);
        }
      }
      j = 0;
      i = 0;
      while(8 - i + j < holidays.Count && holidays[j].end.Date < DateTime.Today) {
        if((holidays[j].type & Holiday.HolidayType.school) != Holiday.HolidayType.none) {
          wt = new Label();
          wt.FontSize = 12;
          wt.SetValue(Label.ContentProperty, holidays[j].ToString());
          wt.SetValue(Label.HorizontalContentAlignmentProperty, (object)HorizontalAlignment.Left);
          wt.Foreground = Brushes.Blue;
          wt.BorderBrush = Brushes.Black;
          wt.BorderThickness = new Thickness(0, 0, 0, 0.8);
          grCalender.Children.Add(wt);
          Grid.SetColumn(wt, 16);
          Grid.SetRow(wt, i);
          i++;
        }
        j++;
      }
      for(; j < holidays.Count && i < 8; j++, i++) {
        var h = holidays[j];
        if(h.edited) {
          var gr = new Grid();
          ColumnDefinition c1 = new ColumnDefinition();
          c1.Width = new GridLength(1, GridUnitType.Auto);
          gr.ColumnDefinitions.Add(c1);
          c1 = new ColumnDefinition();
          c1.Width = new GridLength(1, GridUnitType.Star);
          gr.ColumnDefinitions.Add(c1);
          c1 = new ColumnDefinition();
          c1.Width = new GridLength(1, GridUnitType.Auto);
          gr.ColumnDefinitions.Add(c1);
          c1 = new ColumnDefinition();
          c1.Width = new GridLength(1, GridUnitType.Auto);
          gr.ColumnDefinitions.Add(c1);
          wt = new Label();
          wt.FontSize = 16;
          wt.Content = holidays[j].ToString(true);
          wt.SetValue(Label.HorizontalContentAlignmentProperty, (object)HorizontalAlignment.Left);
          wt.Foreground = new SolidColorBrush(Colors.Orange);

          gr.Children.Add(wt);
          Grid.SetColumn(wt, 0);

          var ed = new TextBox();
          ed.Text = h.titel;
          ed.Tag = h;
          ed.VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
          gr.Children.Add(ed);
          Grid.SetColumn(ed, 1);
          Grid.SetRow(ed, 0);

          var b1 = new Button();
          b1.Content = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Agent3;component/Images/ok.png")), Width = 22, Height = 22 };
          b1.Tag = ed;
          b1.Margin = new Thickness(5, 1, 5, 1);
          b1.Click += AddMemo_Click;
          b1.Background = Brushes.White;
          gr.Children.Add(b1);
          Grid.SetColumn(b1, 2);
          Grid.SetRow(b1, 0);

          var b2 = new Button();
          b2.Content = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Agent3;component/Images/cancel.png")), Width = 22, Height = 22 };
          b2.Tag = ed;
          b2.Margin = new Thickness(15, 1, 15, 1);
          b2.Background = Brushes.White;
          b2.Click += DelMemo_Click;
          gr.Children.Add(b2);
          Grid.SetColumn(b2, 3);
          Grid.SetRow(b2, 0);

          grCalender.Children.Add(gr);
          Grid.SetColumn(gr, 16);
          Grid.SetRow(gr, i);

          ed.SelectAll();
          ed.Focus();
        } else {
          wt = new Label();
          wt.FontSize = 12;
          wt.SetValue(Label.ContentProperty, h.ToString());
          wt.SetValue(Label.HorizontalContentAlignmentProperty, (object)HorizontalAlignment.Left);
          switch(h.type) {
          case Holiday.HolidayType.legal:
            wt.Foreground = new SolidColorBrush(Colors.Red);
            break;
          case Holiday.HolidayType.school:
            wt.Foreground = new SolidColorBrush(Colors.Blue);
            break;
          case Holiday.HolidayType.memo:
            wt.Foreground = new SolidColorBrush(Colors.Green);
            break;
          case Holiday.HolidayType.termin:
            wt.Foreground = new SolidColorBrush(Colors.Orange);
            break;
          default:
            wt.Foreground = fBrush;
            break;
          }
          if(i < 7) {
            wt.BorderBrush = Brushes.Black;
            wt.BorderThickness = new Thickness(0, 0, 0, 0.8);
          }
          if(holidays[j].begin <= today && holidays[j].end >= today) {
            wt.FontWeight = FontWeights.UltraBlack;
            wt.Background = Brushes.MistyRose;
          }

          grCalender.Children.Add(wt);
          Grid.SetColumn(wt, 16);
          Grid.SetRow(wt, i);
        }
      }
    }
    private void MonthColor(DateTime cur, DateTime DT, out Brush mBrush, out Brush fBrush) {
      switch((cur.Year * 12 + cur.Month) - (DT.Year * 12 + DT.Month)) {
      case -1:
        fBrush = Brushes.Black;
        mBrush = new SolidColorBrush(Color.FromRgb(0xBE, 0xE0, 0xFF));
        break;
      case 0:
        fBrush = Brushes.Black;
        mBrush = Brushes.White;
        break;
      case 1:
        fBrush = Brushes.Black;
        mBrush = new SolidColorBrush(Color.FromRgb(0xBE, 0xE0, 0xFF));
        break;
      default:
        fBrush = Brushes.Aquamarine;
        mBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));  //Invisieble
        break;
      }
    }
    private void Month_Click(object sender, RoutedEventArgs e) {
      int offset = (int)((sender as Button).Tag);
      if(offset == 0) {
        _today = DateTime.Now.Date;
      } else {
        _today = _today.AddMonths(offset);
      }
      DrawCalender(_today);
    }
    private void Day_MouseDown(object sender, MouseButtonEventArgs e) {
      DateTime cur = (DateTime)((sender as Label).Tag);
      var h = Holiday.Find(cur, true);
      h.edited = true;
      DrawCalender(_today);
	  System.Diagnostics.Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.System) + System.IO.Path.DirectorySeparatorChar + "osk.exe");
	}
    private void AddMemo_Click(object sender, RoutedEventArgs e) {
      var ed = ((sender as Button).Tag as TextBox);
      Holiday h;
      if(ed != null && (h = ed.Tag as Holiday) != null) {
        h.titel = ed.Text;
        h.edited = false;
        var t = TopicSrc.Get(h.path, true);
        t.saved = true;
        t.value = h.titel;
      }
      DrawCalender(_today);
    }
    private void DelMemo_Click(object sender, RoutedEventArgs e) {
      var ed = ((sender as Button).Tag as TextBox);
      Holiday h;
      if(ed != null && (h = ed.Tag as Holiday) != null) {
        Holiday.Remove(h);
        var t = TopicSrc.Get(h.path, false);
        if(t != null) {
          t.Remove();
        }
      }
      DrawCalender(_today);
    }

    private void Log_Write(LogLevel ll, DateTime dt, string msg) {
      char ll_c;
      switch(ll) {
      case LogLevel.Error:
        ll_c = 'E';
        break;
      case LogLevel.Warning:
        ll_c = 'W';
        break;
      case LogLevel.Info:
        ll_c = 'I';
        break;
      default:
        ll_c = 'D';
        break;
      }
      string rez = string.Concat(dt.ToString("HH:mm:ss.ff"), "[", ll_c, "] ", msg);
      LogLevel lt = LogLevel.Debug;
      if((int)ll >= (int)lt) {
        if(_lfPath == null || _firstDT != dt.Date) {
          _firstDT = dt.Date;
          try {
            foreach(string f in Directory.GetFiles("../log/", "*.log", SearchOption.TopDirectoryOnly)) {
              if(File.GetLastWriteTime(f).AddDays(20) < _firstDT)
                File.Delete(f);
            }
          }
          catch(System.IO.IOException) {
          }
          _lfPath = "../log/" + _firstDT.ToString("yyMMdd") + "_a3.log";
        }
        for(int i = 2; i >= 0; i--) {
          try {
            using(FileStream fs = File.Open(_lfPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) {
              fs.Seek(0, SeekOrigin.End);
              byte[] ba = Encoding.UTF8.GetBytes(rez + "\r\n");
              fs.Write(ba, 0, ba.Length);
            }
            break;
          }
          catch(System.IO.IOException) {
            Thread.Sleep(15);
          }
        }
      }
    }
    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
      try {
        Log.Error("unhandled Exception {0}", e.ExceptionObject.ToString());
      }
      catch {
      }
    }

	private void imDoor_MouseDown(object sender, MouseButtonEventArgs e) {
	  var im=sender as FrameworkElement;
	  TopicSrc t;
	  if(im!=null && (t=im.DataContext as TopicSrc)!=null) {
		t.value=true;
	  }
	}

	private void imDoor_MouseUp(object sender, MouseButtonEventArgs e) {
	  var im=sender as FrameworkElement;
	  TopicSrc t;
	  if(im!=null && (t=im.DataContext as TopicSrc)!=null) {
		t.value=false;
	  }
	}
  }
}
