#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Community.CsharpSqlite;
using Community.CsharpSqlite.SQLiteClient;
using System.IO;
using System.Data;
using Newtonsoft.Json;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace X13.PLC {
  internal class PersistentStorage {
    private SqliteConnection _connection;
    private List<LazyAction> _actions;
    private Thread _thread;
    private ManualResetEvent _close;

    public bool Open(string dbFilename) {
      bool ret;

      _connection = new SqliteConnection();
      if(!Directory.Exists(Path.GetDirectoryName(dbFilename))) {
        Directory.CreateDirectory(Path.GetDirectoryName(dbFilename));
      }
      ret=File.Exists(dbFilename);
      _connection.ConnectionString = string.Format("Version=3,uri=file:{0}", dbFilename);
      _connection.Open();
      IDbCommand cmd = _connection.CreateCommand();
      if(!ret) {
        cmd.CommandText = "CREATE TABLE topics ( path TEXT, type TEXT, val TEXT )";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE UNIQUE INDEX topic_idx ON topics(path)";
        cmd.ExecuteNonQuery();
      }

      cmd.CommandText = "SELECT path, type, val FROM topics ORDER BY path";
      IDataReader reader = cmd.ExecuteReader();
      Topic mq=Topic.root.Get("/local/MQ");
      while(reader.Read()) {
        string v1 = reader.GetString(reader.GetOrdinal("path"));
        string v2 = reader.GetString(reader.GetOrdinal("type"));
        string v3 = reader.GetString(reader.GetOrdinal("val"));
        Topic cur;
        if(!Topic.root.Exist(v1, out cur) || (cur.valueType==null && !string.IsNullOrEmpty(v2))) {
          Type vt;
          if(string.IsNullOrEmpty(v2)) {
            vt=null;
          } else {
            vt=Type.GetType(v2);
            switch(Type.GetTypeCode(vt)) {
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
              vt=typeof(long);
              break;
            case TypeCode.Decimal:
            case TypeCode.Single:
              vt=typeof(double);
              break;
            }
          }
          cur=Topic.GetP(v1, vt, mq);
        }
        cur.saved=true;
        cur.FromJson(v3, mq);
      }
      _actions=new List<LazyAction>();
      _close=new ManualResetEvent(false);
      _thread=new Thread(new ThreadStart(PrThread));
      _thread.Priority=ThreadPriority.Lowest;
      _thread.Start();
      Topic.root.Subscribe("/#", MqChanged);
      return ret;
    }
    public void Close() {
      Topic.root.Unsubscribe("/#", MqChanged);
      _close.Set();
      _thread.Join(2500);
      if(_connection!=null) {
        _connection.Close();
        _connection = null;
      }
    }

    private void MqChanged(Topic sender, TopicChanged param) {
      if(param.Source!=null && !param.Source.path.StartsWith("/local") && (param.Art==TopicChanged.ChangeArt.Remove || (param.Art==TopicChanged.ChangeArt.Value && param.Source.saved))) {
        lock(_actions) {
          _actions.RemoveAll(z => z.art==param.Art && z.src==param.Source);
          _actions.Add(new LazyAction() { src=param.Source, art=param.Art, marker=DateTime.Now.Ticks });
        }
      }
    }
    private void PrThread() {
      try {
        LazyAction cur;
        while(!_close.WaitOne(1100)) {
          long th=DateTime.Now.AddMilliseconds(-4500).Ticks;
          while(true) {
            lock(_actions) {
              if(_actions.Any() && _actions[0].marker<th) {
                cur=_actions[0];
                _actions.RemoveAt(0);
              } else {
                break;
              }
            }
            Process(cur);
          }
        }
        lock(_actions) {
          for(int i=0;i<_actions.Count;i++) {
            Process(_actions[i]);
          }
        }
      }
      catch(Exception ex) {
        Log.Error("PersistenStorage.PrThread exception={0}", ex.ToString());
      }
    }
    private void Process(LazyAction act) {
      IDbCommand cmd=_connection.CreateCommand();
      cmd.Parameters.Add(new SqliteParameter { ParameterName = "@path", Value = act.src.path });
      if(act.art==PLC.TopicChanged.ChangeArt.Value) {
        cmd.CommandText="INSERT OR REPLACE INTO topics VALUES (@path, @type, @val);";
        string st=act.src.valueType==null?null:act.src.valueType.FullName;
        cmd.Parameters.Add(new SqliteParameter { ParameterName = "@type", Value = st });
        string sv=act.src.ToJson();
        cmd.Parameters.Add(new SqliteParameter { ParameterName = "@val", Value = sv });
        //Log.Debug("$+{0}[{1}]={2}", act.src.path, st, sv);
      } else if(act.art==PLC.TopicChanged.ChangeArt.Remove) {
        cmd.CommandText="DELETE FROM topics WHERE path=@path;";
        //Log.Debug("$-{0}", act.src.path);
      }
      cmd.ExecuteNonQuery();
    }

    private struct LazyAction {
      public PLC.TopicChanged.ChangeArt art;
      public Topic src;
      public long marker;
    }
  }
}
