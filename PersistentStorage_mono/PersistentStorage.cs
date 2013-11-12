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
using System.IO;
using System.Data;
using Newtonsoft.Json;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.ComponentModel.Composition;
using System.Reflection;
using Mono.Data.Sqlite;

namespace X13.PLC {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 2)]
  [ExportMetadata("name", "PersistentStorage")]
  public class PersistentStorage : IPlugModul {
    private SqliteConnection _connection;
    private List<LazyAction> _actions;
    private Thread _thread;
    private ManualResetEvent _close;

    public void Init() {
      string dbFilename=@"../data/persist.db3";
      bool ret;

      _connection = new SqliteConnection();
      ret=File.Exists(dbFilename);
      _connection.ConnectionString = string.Format("Version=3,uri=file:{0}", Path.GetFullPath(dbFilename));
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
      Topic.paused=true;
      while(reader.Read()) {
        string v2=string.Empty;
        string v1 = reader.GetString(reader.GetOrdinal("path"));
        try {
          v2 = reader.GetString(reader.GetOrdinal("type"));
        }
        catch(Exception) {
        }
        string v3 = reader.GetString(reader.GetOrdinal("val"));
        Topic cur;
        if(!Topic.root.Exist(v1, out cur) || (cur.valueType==null && !string.IsNullOrEmpty(v2))) {
          Type vt;
          if(string.IsNullOrEmpty(v2)) {
            vt=null;
          } else {
            vt=X13.WOUM.ExConverter.FullName2Type(v2);
            //vt=Type.GetType(v2);
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
      Topic.root.Subscribe("/#", MqChanged);
    }
    public void Start() {
      Topic.paused=false;
      _thread.Start();
    }

    public void Stop() {
      if(_connection!=null) {
        Topic.root.Unsubscribe("/#", MqChanged);
        _close.Set();
        _thread.Join(3500);
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
        while(_connection==null) {
          Thread.Sleep(300);
        }
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
          for(int i=0; i<_actions.Count; i++) {
            Process(_actions[i]);
          }
        }
      }
      catch(Exception ex) {
        Log.Error("PersistenStorage.PrThread exception={0}", ex.ToString());
      }
    }
    private void Process(LazyAction act) {
      if(_connection==null) {
        return;
      }
      IDbCommand cmd=_connection.CreateCommand();
      cmd.Parameters.Add(new SqliteParameter { ParameterName = "@path", Value = act.src.path });
      if(act.art==TopicChanged.ChangeArt.Value) {
        cmd.CommandText="INSERT OR REPLACE INTO topics VALUES (@path, @type, @val);";
        string st=act.src.valueType==null?string.Empty:act.src.valueType.FullName;
        string sv=act.src.ToJson();
        if(act.src.valueType==typeof(JObject)) {
          var jo=JObject.Parse(sv);
          JToken jt1;
          if(jo.TryGetValue("+", out jt1)) {
            st=jt1.Value<string>();
          }
        }
        cmd.Parameters.Add(new SqliteParameter { ParameterName = "@type", Value = st });
        cmd.Parameters.Add(new SqliteParameter { ParameterName = "@val", Value = sv });
        //Log.Debug("$+{0}[{1}]={2}", act.src.path, st, sv);
      } else if(act.art==TopicChanged.ChangeArt.Remove) {
        cmd.CommandText="DELETE FROM topics WHERE path=@path;";
        //Log.Debug("$-{0}", act.src.path);
      }
      cmd.ExecuteNonQuery();
    }

    private struct LazyAction {
      public TopicChanged.ChangeArt art;
      public Topic src;
      public long marker;
    }

  }
}
