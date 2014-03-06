#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13.PLC {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 2)]
  [ExportMetadata("name", "PersistentStorage")]
  public class PersistentStorage : IPlugModul {
    /// <summary>data stored in this record</summary>
    private const uint FL_SAVED_I  =0x01000000;
    /// <summary>data stored as a separate record</summary>
    private const uint FL_SAVED_E  =0x02000000;
    /// <summary>mask</summary>
    private const uint FL_SAVED_A  =0x07000000;
    private const uint FL_LOCAL    =0x08000000;
    private const uint FL_SAVED    =0x20000000;
    private const uint FL_RECORD   =0x40000000;
    private const uint FL_REMOVED  =0x80000000;
    private const int FL_REC_LEN   =0x00FFFFFF;
    private const int FL_DATA_LEN  =0x3FFFFFFF;

    private Dictionary<Topic, Record> _tr;
    private List<FRec> _free;
    private FileStream _file;
    private List<Record> _refitParent;
    private ManualResetEvent _fileOp;
    private LinkedList<Tuple<long, byte[]>> _toSave;
    private long _fileLength;
    private bool _terminate;
    private DateTime _nextBak;
    private Topic _sign;

    public PersistentStorage() {
      _tr=new Dictionary<Topic, Record>();
      _free=new List<FRec>();
      _toSave=new LinkedList<Tuple<long, byte[]>>();
      _fileOp=new ManualResetEvent(false);
    }
    public void Init() {
      Topic.paused=true;
      _sign=Topic.root.Get("/local/cfg/PersistentStorage");

      if(!Directory.Exists("../data")) {
        Directory.CreateDirectory("../data");
      }
      _file=new FileStream("../data/persist.xdb", FileMode.OpenOrCreate, FileAccess.ReadWrite);
      if(_file.Length<0x40) {
        _file.Write(new byte[0x40], 0, 0x40);
        _file.Flush(true);
      } else {
        _file.Position=0x40;
        long curPos;
        byte[] lBuf=new byte[4];
        _refitParent=new List<Record>();
        Topic t;

        do {
          curPos=_file.Position;
          _file.Read(lBuf, 0, 4);
          uint fl_size=BitConverter.ToUInt32(lBuf, 0);
          int len=(int)fl_size&(((fl_size&FL_RECORD)!=0)?FL_REC_LEN:FL_DATA_LEN);

          if(len==0) {
            Log.Warning("PersistentStorage: Empty record at 0x{0:X8}", curPos);
            len=1;
          } else if((fl_size & FL_REMOVED)!=0) {
            AddFree((uint)(curPos>>4), (int)fl_size);
          } else if((fl_size&FL_RECORD)!=0) {
            byte[] buf=new byte[len];
            lBuf.CopyTo(buf, 0);
            _file.Read(buf, 4, len-4);
            ushort crc1=BitConverter.ToUInt16(buf, len-2);
            ushort crc2=Crc16.ComputeChecksum(buf, len-2);
            if(crc1!=crc2) {
              throw new ApplicationException("PersistentStorage: CRC Error at 0x"+curPos.ToString("X8"));
            }
            var r=new Record((uint)(curPos>>4), buf);

            if(r.parent==0) {
              if(r.name=="/") {
                t=AddTopic(null, r);
              } else {
                t=null;
              }
            } else if(r.parent<r.pos) {
              t=_tr.FirstOrDefault(z => z.Value.pos==r.parent).Key;
              if(t!=null) {
                t=AddTopic(t, r);
              }
            } else {
              t=null;
            }
            if(t==null) {
              int idx=indexPPos(_refitParent, r.parent);
              _refitParent.Insert(idx+1, r);
            }
          }
          _file.Position=curPos+((len+15)&0x7FFFFFF0);
        } while(_file.Position<_file.Length);
        foreach(var kv in _refitParent) {
          Log.Warning("! [{1:X4}] {0} ({2})", kv.name, kv.pos<<3, kv.parent);
        }
        _refitParent=null;
      }
      _fileLength=_file.Length;
      Backup();
    }
    public void Start() {
      ThreadPool.QueueUserWorkItem(FileOperations);
      Topic.root.Subscribe("/#", MqChanged);
      Topic.paused=false;
    }
    public void Stop() {
      Topic.root.Unsubscribe("/#", MqChanged);

      _terminate=true;
      _fileOp.Set();
      int i=120;
      while(_terminate && i-->0) {
        Thread.Sleep(100);
      }
      _file.Close();

    }
    private void MqChanged(Topic sender, TopicChanged param) {
      if(sender==null || sender.path.StartsWith("/local") || sender.path=="/var/log/A0" || param.Visited(_sign, true)) {
        return;
      }
      Save(sender, param.Art==TopicChanged.ChangeArt.Remove);
    }
    private void Backup() {
      DateTime now = DateTime.Now;
      _nextBak=now.AddDays(1);
      string fn="../data/"+LongToString(now.Ticks*3/2000000000L)+".bak";  // 1/66(6)Sec.
      try {
        var bak=new FileStream(fn, FileMode.Create, FileAccess.ReadWrite);
        _file.Position=0;
        _file.CopyTo(bak);
        bak.Close();
        foreach(string f in Directory.GetFiles("../data/", "*.bak", SearchOption.TopDirectoryOnly)) {
          if(File.GetLastWriteTime(f).AddDays(15)<_nextBak)
            File.Delete(f);
        }
      }
      catch(System.IO.IOException ex) {
        Log.Warning("PersistentStorage.Backup - "+ex.Message);
      }
    }
    private void FileOperations(object o) {
      Tuple<long, byte[]> val;
      bool signal;
      while(!_terminate) {
        try {
          _fileOp.Reset();
          signal=_fileOp.WaitOne(1500);
        }
        catch(Exception ex) {
          Log.Debug("PersistentStorage.FileOperations terminated - "+ex.Message);
          break;
        }
        try {
          if(!signal) {
            if(_nextBak<DateTime.Now) {
              Backup();
            }
            lock(_free) {
              for(int i=_free.Count-1; i>=0; i--) {
                if((((long)_free[i].pos<<4)+((_free[i].size+15)&0x7FFFFFF0))>=_fileLength) {
                  _fileLength=(long)_free[i].pos<<4;
                  _free.RemoveAt(i);
                  break;
                }
              }
              if(_fileLength<_file.Length) {
                _file.SetLength(_fileLength);
              }
            }
          } else {
            Thread.Sleep(60);
            while(true) {
              lock(_toSave) {
                if(_toSave.First==null) {
                  val=null;
                } else {
                  val=_toSave.First.Value;
                  _toSave.RemoveFirst();
                }
              }
              if(val==null) {
                break;
              } else {
                _file.Position=val.Item1;
                _file.Write(val.Item2, 0, val.Item2.Length);
              }
            }
            _file.Flush(true);
          }
        }
        catch(Exception ex) {
          Log.Warning("PersistentStorage.FileOperations exception - "+ex.ToString());
        }
      }
      _terminate=false;
    }
    private void ToSave(long pos, byte[] buf) {
      if(buf==null || buf.Length<4) {
        throw new ArgumentException("buf");
      }
      var val=new Tuple<long, byte[]>(pos, buf);
      lock(_toSave) {
        var cur=_toSave.Last;
        if(cur!=null && (buf[3]&(byte)(FL_REMOVED>>24))==0) {
          while(cur!=null && (cur.Value.Item2[3]&(byte)(FL_REMOVED>>24))!=0) {
            if(cur.Value.Item1==pos) {
              cur=cur.Previous;
              _toSave.Remove(cur.Next);
            } else {
              cur=cur.Previous;
            }
          }
        }
        if(cur==null) {
          _toSave.AddFirst(val);
        } else {
          _toSave.AddAfter(cur, val);
          while(cur!=null) {
            if(cur.Value.Item1==pos) {
              _toSave.Remove(cur);
              break;
            }
            cur=cur.Previous;
          }
        }
      }
      _fileOp.Set();
    }
    private void Save(Topic t, bool remove) {
      Record rec;
      uint parentPos, oldFl_Size;
      int oldDataSize;
      bool recModified=false, dataModified=false;
      if(t.parent==null || remove) {
        parentPos=0;
      } else if(_tr.TryGetValue(t.parent, out rec)) {
        parentPos=rec.pos;
      } else {
        return;  // parent is unknown
      }
      if(!_tr.TryGetValue(t, out rec)) {
        if(remove) {
          return;
        }
        oldFl_Size=0;
        oldDataSize=0;
        rec=new Record(t, parentPos);
        recModified=true;
        dataModified=true;
        _tr[t]=rec;
      } else if(remove) {
        if(rec.saved_fl==FL_SAVED_E && rec.data_pos>0 && rec.data_size>0) {
          AddFree(rec.data_pos, rec.data_size);
        }
        AddFree(rec.pos, rec.size);
        _tr.Remove(t);
        return;
      } else {
        oldFl_Size=rec.fl_size;
        oldDataSize=rec.data_size+6;
        if(rec.parent!=parentPos) {
          rec.parent=parentPos;
          recModified=true;
        }
        if(t==Topic.root) {
          if(rec.name!="/") {
            rec.name="/";
            recModified=true;
          }
        } else if(rec.name!=t.name) {
          rec.name=t.name;
          recModified=true;
        }
        rec.fl_size=FL_RECORD | (uint)(14+Encoding.UTF8.GetByteCount(rec.name));
        string type_data=t.valueType==null?string.Empty:string.Concat(t.valueType.FullName, "\0", (t.saved)?t.ToJson():string.Empty);
        if(type_data.Length>0) {
          if(rec.data!=type_data) {
            rec.data=type_data;
            dataModified=true;
          }
          rec.data_size=(rec.data==null?0:Encoding.UTF8.GetByteCount(rec.data));
          if(rec.data_size>0 && rec.data_size<32) {
            rec.fl_size=(rec.fl_size+(uint)rec.data_size) | FL_SAVED_I;
          } else {
            rec.fl_size|=FL_SAVED_E;
          }
        } else {
          rec.data=null;
          rec.data_size=0;
          rec.saved_fl=0;
          if(oldDataSize>0) {
            dataModified=true;
          }
        }
        if(t.saved) {
          rec.fl_size|=FL_SAVED;
        }
      }
      byte[] recBuf=new byte[rec.size];
      if(rec.data_size>0) {
        if(rec.saved_fl==FL_SAVED_I) {
          CopyBytes(rec.data_size, recBuf, 8);
          Encoding.UTF8.GetBytes(rec.data).CopyTo(recBuf, recBuf.Length-rec.data_size-2);
          if(rec.data_pos>0 && oldDataSize>0) {
            AddFree(rec.data_pos, oldDataSize);
            rec.data_pos=0;
          }
          if(dataModified) {
            recModified=true;
          }
        } else {
          if(dataModified) {
            byte[] dataBuf=new byte[6+rec.data_size];
            CopyBytes(dataBuf.Length, dataBuf, 0);
            Encoding.UTF8.GetBytes(rec.data).CopyTo(dataBuf, 4);
            if(Write(ref rec.data_pos, dataBuf, oldDataSize)) {
              recModified=true;
            }
            Log.Debug("D [0x{2:X4}]{0} ({1}) {3}", t.path, rec.size, rec.data_pos<<4, rec.data);
          }
          CopyBytes(rec.data_pos, recBuf, 8);
        }
      } else if(rec.data_pos>0 && oldDataSize>0) {
        AddFree(rec.data_pos, oldDataSize);
        rec.data_pos=0;
        recModified=true;
      }
      if(recModified || rec.fl_size!=oldFl_Size) {
        CopyBytes(rec.fl_size, recBuf, 0);
        CopyBytes(rec.parent, recBuf, 4);
        Encoding.UTF8.GetBytes(rec.name).CopyTo(recBuf, 12);
        if(Write(ref rec.pos, recBuf, (int)oldFl_Size & FL_REC_LEN)) {
          Log.Debug("W {0} {1} [0x{2:X4}]", t.path, rec.size, rec.pos<<4);
          var ch=t.children.ToArray();
          for(int i=ch.Length-1; i>=0; i--) {
            if(!ch[i].path.StartsWith("/local") && ch[i].path!="/var/log/A0") {
              Save(ch[i], false);
            }
          }
        } else {
          Log.Debug("W {0} {1} [0x{2:X4}]", t.path, rec.size, rec.pos<<4);
        }
      }
    }
    private bool Write(ref uint pos, byte[] buf, int oldSize) {
      oldSize=((oldSize+15)&0x7FFFFFF0);
      uint oldPos=pos;
      CopyBytes(Crc16.ComputeChecksum(buf, buf.Length-2), buf, buf.Length-2);
      if(((buf.Length+15)&0x00FFFFF0)!=oldSize) {
        if(pos>0) {
          AddFree(pos, oldSize);
        }
        pos=FindFree(buf.Length);
      }
      ToSave((long)pos<<4, buf);
      return pos!=oldPos;
    }
    private Topic AddTopic(Topic parent, Record r) {
      Topic t=null;
      Type type=null;
      string data=null;

      if(r.saved_fl!=0) {
        if(r.saved_fl==FL_SAVED_E) {
          byte[] lBuf=new byte[4];
          _file.Position=(long)r.data_pos<<4;
          _file.Read(lBuf, 0, 4);
          int data_size=BitConverter.ToInt32(lBuf, 0);
          if((data_size & (FL_REMOVED | FL_RECORD))==0) {
            if(data_size<4) {
              Log.Warning("DataStorage: mismatch data size, record @ {0:X8} for {1}/{2}", (long)r.data_pos<<4, parent!=null?parent.path:string.Empty, r.name);
            } else {
              r.data_size=data_size-6;
              byte[] buf=new byte[data_size];
              lBuf.CopyTo(buf, 0);
              _file.Read(buf, 4, buf.Length-4);
              ushort crc1=BitConverter.ToUInt16(buf, buf.Length-2);
              ushort crc2=Crc16.ComputeChecksum(buf, buf.Length-2);
              if(crc1!=crc2) {
                throw new ApplicationException("CRC Error Data@0x"+((long)r.pos<<3).ToString("X8"));
              }
              r.data=Encoding.UTF8.GetString(buf, 4, (int)r.data_size);
            }
          }
        }
        if(r.data!=null && r.data.Length>1) {
          var datat=r.data.Split('\0');
          if(datat!=null && datat.Length>0 && !string.IsNullOrEmpty(datat[0])) {
            type=X13.WOUM.ExConverter.FullName2Type(datat[0]);
            if(datat.Length>1) {
              data=datat[1];
            }
          }
        }
      }
      if(parent==null) {
        if(r.name=="/") {
          t=Topic.root;
        }
      } else {
        t=Topic.GetP(r.name, type, _sign, parent);
      }
      if(t!=null) {
        t.saved=r.saved;
        if(data!=null) {
          t.FromJson(data, _sign);
        }
        Log.Debug("R {0}={1} [0x{2:X4}]", t.path, t.GetValue(), r.pos);
        _tr[t]=r;
        int idx=indexPPos(_refitParent, r.pos);
        while(idx>=0 && idx<_refitParent.Count && _refitParent[idx].parent==r.pos) {
          Record nextR=_refitParent[idx];
          _refitParent.RemoveAt(idx);
          AddTopic(t.Get(nextR.name, null), nextR);
          idx=indexPPos(_refitParent, r.pos);
        }
      }
      return t;

    }
    private int indexPPos(List<Record> np, uint ppos) {
      int min=0, mid=-1, max=np.Count-1;

      while(min<=max) {
        mid = (min + max) / 2;
        if(np[mid].parent < ppos) {
          min = mid + 1;
        } else if(np[mid].parent > ppos) {
          max = mid - 1;
          mid = max;
        } else {
          break;
        }
      }
      if(mid>=0) {
        max=np.Count-1;
        while(mid<max && np[mid+1].parent<=ppos) {
          mid++;
        }
      }
      return mid;
    }
    private void AddFree(uint pos, int size) {
      if(((uint)size & FL_REMOVED)==0) {
        ToSave((long)pos<<4, BitConverter.GetBytes(((uint)size+4)&0x7FFFFFF0 | FL_REMOVED));
      }
      lock(_free) {
        FRec fr=new FRec(pos, (size+15)&0x7FFFFFF0);
        int idx=_free.BinarySearch(fr);
        idx=idx<0?~idx:idx+1;
        _free.Insert(idx, fr);
      }
    }
    private uint FindFree(int size) {
      size=(size+15)&0x7FFFFFF0;
      uint rez;
      lock(_free) {
        int idx=-1;
        idx=_free.BinarySearch(new FRec(0, size));
        if(idx<0) {
          idx=~idx;
        }
        if(idx<_free.Count && _free[idx].size==size) {
          rez=_free[idx].pos;
          _free.RemoveAt(idx);
        } else {
          rez=(uint)((_fileLength+15)>>4);
          _fileLength=((long)rez<<4)+size;
        }
      }
      return rez;
    }
    private static void CopyBytes(int value, byte[] buf, int offset) {
      buf[offset++]=(byte)value;
      buf[offset++]=(byte)(value>>8);
      buf[offset++]=(byte)(value>>16);
      buf[offset++]=(byte)(value>>24);
    }
    private static void CopyBytes(uint value, byte[] buf, int offset) {
      buf[offset++]=(byte)value;
      buf[offset++]=(byte)(value>>8);
      buf[offset++]=(byte)(value>>16);
      buf[offset++]=(byte)(value>>24);
    }
    private static void CopyBytes(ushort value, byte[] buf, int offset) {
      buf[offset++]=(byte)value;
      buf[offset++]=(byte)(value>>8);
    }
    public static string LongToString(long value, string baseChars=null) {
      if(string.IsNullOrEmpty(baseChars)) {
        baseChars="0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
      }
      int i = 64;
      char[] buffer = new char[i];
      int targetBase= baseChars.Length;

      do {
        buffer[--i] = baseChars[(int)(value % targetBase)];
        value = value / targetBase;
      }
      while(value>0);

      char[] result = new char[64 - i];
      Array.Copy(buffer, i, result, 0, 64 - i);

      return new string(result);
    }

    private struct FRec : IComparable<FRec> {
      private long val;

      public FRec(uint pos, int size) {
        val=((long)size<<32) | pos;
      }
      public int CompareTo(FRec o) {
        return val.CompareTo(o.val);
      }
      public uint pos { get { return (uint)val; } }
      public int size { get { return (int)(val>>32); } }

    }
    private class Record {
      public uint pos;
      public uint fl_size;
      public uint parent;
      public uint data_pos;
      public int data_size;
      public string name;
      public string data;

      public Record(uint pos, byte[] buf) {
        this.pos=pos;
        this.fl_size=BitConverter.ToUInt32(buf, 0);
        uint len=fl_size & FL_REC_LEN;
        parent=BitConverter.ToUInt32(buf, 4);
        uint dataPS=BitConverter.ToUInt32(buf, 8);
        if(dataPS>0) {
          if((fl_size&FL_SAVED_A)==FL_SAVED_I) {
            data_pos=0;
            data_size=(int)dataPS;
            data=Encoding.UTF8.GetString(buf, (int)(buf.Length-data_size-2), (int)(data_size));
          } else {
            data_pos=dataPS;
            data_size=0;
            data=null;
          }
        } else {
          data=null;
          data_pos=0;
          data_size=0;
        }
        name=Encoding.UTF8.GetString(buf, 12, (int)(buf.Length-data_size-14));
      }
      public Record(Topic t, uint parent) {
        pos=0;
        data_pos=0;
        this.parent=parent;
        if(t==Topic.root) {
          name="/";
        } else {
          name=t.name;
        }
        fl_size=FL_RECORD | (uint)(14+Encoding.UTF8.GetByteCount(name));
        string type_data=t.valueType==null?string.Empty:string.Concat(t.valueType.FullName, "\0", (t.saved)?t.ToJson():string.Empty);
        if(type_data.Length>0) {
          data=type_data;
          data_size=Encoding.UTF8.GetByteCount(data);
          if(data_size>0 && data_size<32) {
            fl_size=(fl_size+(uint)data_size) | FL_RECORD | FL_SAVED_I;
          } else {
            fl_size|=FL_SAVED_E;
          }
        } else {
          data=null;
          data_size=0;
        }
        if(t.saved) {
          fl_size|=FL_SAVED;
        }
      }
      public uint saved_fl { get { return fl_size&FL_SAVED_A; } set { fl_size=(fl_size & ~FL_SAVED_A) | (value & FL_SAVED_A); } }
      public bool saved { get { return (fl_size&FL_SAVED)!=0; } set { fl_size=(value?fl_size|FL_SAVED : fl_size&~FL_SAVED); } }
      public bool local { get { return (fl_size&FL_LOCAL)!=0; } set { fl_size=value?fl_size|FL_LOCAL : fl_size&~FL_LOCAL; } }
      public bool removed { get { return (fl_size&FL_REMOVED)!=0; } set { fl_size=value?fl_size|FL_REMOVED:fl_size&~FL_REMOVED; } }
      public int size { get { return (int)fl_size & FL_REC_LEN; } }
    }

  }
}
