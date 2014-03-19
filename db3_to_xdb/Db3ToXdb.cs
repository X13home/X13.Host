#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  internal class Db3ToXdb {
    static void Main(string[] args) {
      Idb3 src;
      Log.Info("opening source database");
      int p = (int)Environment.OSVersion.Platform;
      bool isLinux=((p == 4) || (p == 6) || (p == 128));
      if(isLinux) {
        src=new Db3_mono();
      } else {
        src=new Db3_win();
      }
      if(!src.Open()) {
        Log.Error("open source database - failed");
      } else {
        int cnt=0;
        string path, type, val;
        while(src.Read(out path, out type, out val)) {
          switch(type) {
          case "System.Int64":
            type="long";
            break;
          case "System.Boolean":
            type="bool";
            break;
          case "System.String":
            type="string";
            break;
          case "System.Double":
            type="double";
            break;
          case "System.DateTime":
            type="DateTime";
            break;
          case "X13.PLC.Topic":
          case "X13.Topic":
            type="Topic";
            break;
          case "X13.PLC.PiStatement":
            type="Statement";
            break;
          case "X13.PLC.PiWire":
            type="Wire";
            break;
          case "":
            val=string.Empty;
            break;
          }
          Topic.Add(path, type, val);
          cnt++;
        }
        src.Close();
        Log.Info("source database closed. Read {0} records", cnt);

        //string pl="                                                                + ";
        Topic cur;
        Stack<Topic> prt=new Stack<Topic>();
        prt.Push(Topic.root);
        Log.Info("opening destination database");
        cnt=0;
        Xdb dst=new Xdb();
        dst.Open();
        while(prt.Count>0) {
          cur=prt.Pop();
          //Log.Debug("{0}{1}<{2}>={3}", pl.Substring(pl.Length-cur.lvl*2, cur.lvl*2), cur.name, cur.type, cur.val);
          dst.Write(cur);
          cnt++;
          if(cur.children!=null) {
            foreach(var tmp in cur.children.OrderByDescending(z => z.name)) {
              prt.Push(tmp);
            }
          }
        }
        dst.Close();
        Log.Info("destination database closed. Writed {0} records", cnt);
      }
      Log.Warning("Press 'Enter' to exit");
      Console.ReadLine();
    }
  }
}
