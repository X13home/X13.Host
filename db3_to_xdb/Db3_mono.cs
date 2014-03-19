#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Data.Sqlite;

namespace X13 {
  internal class Db3_mono : Idb3 {
    private SqliteConnection _connection;
    private IDataReader _reader;
    public bool Open() {
      string dbFilename=@"../data/persist.db3";
      if(!File.Exists(dbFilename)) {
        Log.Warning("{0} not found", dbFilename);
        return false;
      }
      try {

        _connection = new SqliteConnection();
        _connection.ConnectionString = string.Format("Version=3,uri=file:{0}", Path.GetFullPath(dbFilename));
        _connection.Open();
        IDbCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT path, type, val FROM topics ORDER BY path";
        _reader = cmd.ExecuteReader();
      }
      catch(Exception ex) {
        Log.Error("Db3_mono.Open - {0}", ex.Message);
        return false;
      }
      return true;
    }

    public bool Read(out string path, out string type, out string val) {
      try {
        if(_reader.Read()) {
          path = _reader.GetString(_reader.GetOrdinal("path"));
          type  = _reader.GetString(_reader.GetOrdinal("type"));
          val = _reader.GetString(_reader.GetOrdinal("val"));
        } else {
          path=string.Empty;
          type=string.Empty;
          val=string.Empty;
          return false;
        }
      }
      catch(Exception ex) {
        path=string.Empty;
        type=string.Empty;
        val=string.Empty;
        Log.Error("Db3_mono.Read - {0}", ex.Message);
        return false;
      }
      return true;
    }

    public void Close() {
      if(_connection!=null) {
        _connection.Close();
        _connection = null;
      }
    }
  }
}
