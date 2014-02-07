#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;

namespace X13.PLC {
  [Export(typeof(IStatement))]
  [ExportMetadata("declarer", "Interpolation3")]
  public class Interpolation3 : IStatement {
    private DVar<double> _x;  //input
    private DVar<double> _y;  //output
    private DVar<string> _csv;
    private CubicSpline _cubSpl;
    public void Load() {
      var t1=Topic.root.Get<string>("/etc/declarers/func/Interpolation3");
      t1.value="pack://application:,,/ExpStatements;component/Images/fu_intr3.png";
      t1.Get<string>("X").value="Ag";
      t1.Get<string>("Y").value="ag";
      t1.Get<string>("_description").value="g3Cubic interpolation";

      t1.Get<string>("rename").value="|R";
      t1.Get<string>("remove").value="}D";

    }

    public void Init(DVar<PiStatement> model) {
      _cubSpl=new CubicSpline();
      _x=BiultInStatements.AddPin<double>(model, "X");
      _y=BiultInStatements.AddPin<double>(model, "Y");
      _csv=BiultInStatements.AddPin<string>(model, "_csv");
    }

    public void Calculate(DVar<PiStatement> model, Topic source) {
      if(source==_csv) {
        _cubSpl.Reset();
        string[] dat=null;
        if(File.Exists(_csv.value)) {
          try {
            dat=File.ReadAllLines(_csv.value);
          }
          catch(Exception ex) {
            Log.Warning("{0}.Load({1}) - {2}", model.path, _csv.value, ex.Message);
          }
        } else {
          Log.Warning("{0}.Load({1}) not exist", model.path, _csv.value);
        }
        if(dat==null || dat.Length==0) {
          return;
        }
        double x, y;
        string[] lt;
        for(int i=0; i<dat.Length; i++) {
          lt=dat[i].Split(';');
          if(lt==null || lt.Length!=2) {
            continue;
          }
          if(double.TryParse(lt[0], out x)) {
            if(double.TryParse(lt[1], out y)) {
              _cubSpl.AddNode(x, y);
            } else {
              Log.Warning("{0}.Load({1}) wrong Y value [{2}]{3}", model.path, _csv.value, i, dat[i]);
              continue;
            }
          } else {
            Log.Warning("{0}.Load({1}) wrong X value [{2}]{3}", model.path, _csv.value, i, dat[i]);
            continue;
          }
        }
        _cubSpl.BuildSpline();
      }
      _y.value=_cubSpl.Func(_x.value);
    }

    public void DeInit() {
    }
    /// <summary>Интерполирование функций естественными кубическими сплайнами</summary>
    /// <remarks>http://ru.wikipedia.org/wiki/%D0%9A%D1%83%D0%B1%D0%B8%D1%87%D0%B5%D1%81%D0%BA%D0%B8%D0%B9_%D1%81%D0%BF%D0%BB%D0%B0%D0%B9%D0%BD</remarks>
    private class CubicSpline {
      private List<SplineTuple> _splines; // Сплайн
      private bool _builded;

      public CubicSpline() {
        _splines=new List<SplineTuple>();
        _builded=false;
      }
      // Структура, описывающая сплайн на каждом сегменте сетки
      private class SplineTuple {
        public double a, b, c, d, x;
        public static int Compare(SplineTuple i, SplineTuple j) {
          return i.x.CompareTo(j.x);
        }
      }

      public void Reset() {
        lock(this) {
          _splines.Clear();
          _builded=false;
        }
      }
      /// <summary></summary>
      /// <param name="x">узел сетки, кратные узлы запрещены</param>
      /// <param name="y">значения функции</param>
      public void AddNode(double x, double y) {
        if(_builded) {
          throw new ApplicationException();
        }
        _splines.Add(new SplineTuple() { x=x, a=y, c=0.0 });
      }

      // Построение сплайна
      public void BuildSpline() {
        int n=_splines.Count;
        _splines.Sort(SplineTuple.Compare);

        // Решение СЛАУ относительно коэффициентов сплайнов c[i] методом прогонки для трехдиагональных матриц
        // Вычисление прогоночных коэффициентов - прямой ход метода прогонки
        double[] alpha = new double[n - 1];
        double[] beta = new double[n - 1];
        alpha[0] = beta[0] = 0.0;
        for(int i = 1; i < n - 1; ++i) {
          double h_i = _splines[i].x - _splines[i - 1].x, h_i1 = _splines[i + 1].x - _splines[i].x;
          double A = h_i;
          double C = 2.0 * (h_i + h_i1);
          double B = h_i1;
          double F = 6.0 * ((_splines[i + 1].a - _splines[i].a) / h_i1 - (_splines[i].a - _splines[i - 1].a) / h_i);
          double z = (A * alpha[i - 1] + C);
          alpha[i] = -B / z;
          beta[i] = (F - A * beta[i - 1]) / z;
        }

        // Нахождение решения - обратный ход метода прогонки
        for(int i = n - 2; i > 0; --i)
          _splines[i].c = alpha[i] * _splines[i + 1].c + beta[i];

        // Освобождение памяти, занимаемой прогоночными коэффициентами
        beta = null;
        alpha = null;

        // По известным коэффициентам c[i] находим значения b[i] и d[i]
        for(int i = n - 1; i > 0; --i) {
          double h_i = _splines[i].x - _splines[i - 1].x;
          _splines[i].d = (_splines[i].c - _splines[i - 1].c) / h_i;
          _splines[i].b = h_i * (2.0 * _splines[i].c + _splines[i - 1].c) / 6.0 + (_splines[i].a - _splines[i - 1].a) / h_i;
        }
        _builded=true;
      }

      /// <summary>Вычисление значения интерполированной функции в произвольной точке</summary>
      /// <param name="x">узел сетки</param>
      /// <returns>значения функции</returns>
      public double Func(double x) {
        if(_splines == null)
          return double.NaN; // Если сплайны ещё не построены - возвращаем NaN

        int n = _splines.Count;
        SplineTuple s;

        if(x <= _splines[0].x) { // Если x меньше точки сетки x[0] - пользуемся первым эл-тов массива
          s = _splines[1];
        } else if(x >= _splines[n - 1].x) { // Если x больше точки сетки x[n - 1] - пользуемся последним эл-том массива
          s = _splines[n - 1];
        } else { // Иначе x лежит между граничными точками сетки - производим бинарный поиск нужного эл-та массива {
          int i = 0, j = n - 1;
          while(i + 1 < j) {
            int k = i + (j - i) / 2;
            if(x <= _splines[k].x)
              j = k;
            else
              i = k;
          }
          s = _splines[j];
        }

        double dx = (x - s.x);
        // Вычисляем значение сплайна в заданной точке по схеме Горнера (в принципе, "умный" компилятор применил бы схему Горнера сам, но ведь не все так умны, как кажутся)
        return s.a + (s.b + (s.c / 2.0 + s.d * dx / 6.0) * dx) * dx;
      }
    }
  }
}
