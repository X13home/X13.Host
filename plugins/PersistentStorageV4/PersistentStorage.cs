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
    private const int FL_LEN_MASK  =0x00FFFFF0;

    private Dictionary<Topic, Record> _tr;
    private LinkedList<Record> _recordsToSave;
    private SortedSet<ulong> _freeBlocks;
    private FileStream _file;
    private List<Record> _refitParent;
    private System.Collections.Concurrent.ConcurrentQueue<Topic> _ch;
    private long _fileLength;
    private byte[] _writeBuf;
    private DateTime _nextBak;
    private DateTime _now;
    private Topic _sign;
    private Thread _thread;
    private AutoResetEvent _work;
    private bool _terminate;
    private static DVar<bool> _verbose;

    public PersistentStorage() {
      _tr=new Dictionary<Topic, Record>();
      _freeBlocks=new SortedSet<ulong>();
      _ch=new System.Collections.Concurrent.ConcurrentQueue<Topic>();
    }
    public void Init() {
      Topic.paused=true;
      _sign=Topic.root.Get("/local/cfg/PersistentStorage");
      _verbose=Topic.root.Get<bool>("/local/cfg/PersistentStorage/verbose");

      if(!Directory.Exists("../data")) {
        Directory.CreateDirectory("../data");
      }
      _file=new FileStream("../data/persist.xdb", FileMode.OpenOrCreate, FileAccess.ReadWrite);
      if(_file.Length<0x40) {
        _file.Write(new byte[0x40], 0, 0x40);
        _file.Flush(true);
        _nextBak=DateTime.Now.AddHours(1);
      } else {
        Load();
      }
      _fileLength=_file.Length;
      _work=new AutoResetEvent(false);
      _thread=new Thread(new ThreadStart(PrThread));
      _thread.Priority=ThreadPriority.Lowest;
      _now=DateTime.Now;
      if(_nextBak<_now) {
        Backup();
      }
      _thread.Start();
      Topic.root.Subscribe("/#", MqChanged);
    }
    public void Start() {
      Topic.paused=false;
    }
    public void Stop() {
      if(_file!=null) {
        Topic.paused=true;
        Topic.root.Unsubscribe("/#", MqChanged);
        _terminate=true;
        _work.Set();
        _thread.Join(3500);
        _file.Close();
        _file=null;
      }
    }

    private void Load() {
      long curPos;
      byte[] lBuf=new byte[4];
      _refitParent=new List<Record>();
      Topic t;
      _file.Position=0x40;

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
          var r=new Record((uint)(curPos>>4), buf, _file);

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
        _file.Position=curPos+((len+15)&FL_LEN_MASK);
      } while(_file.Position<_file.Length);
      foreach(var kv in _refitParent) {
        Log.Warning("! [{1:X4}] {0} ({2:X4})", kv.name, kv.pos<<4, kv.parent<<4);
      }
      _refitParent=null;
    }
    private void MqChanged(Topic sender, TopicChanged param) {
      if(sender==null || sender.path.StartsWith("/local") || sender.path=="/var/log/A0" || param.Visited(_sign, true)) {
        return;
      }
      _ch.Enqueue(sender);
      if(param.Art!=TopicChanged.ChangeArt.Add) {
        _work.Set();
      }
    }
    private void PrThread() {
      _recordsToSave=new LinkedList<Record>();
      Topic tCh;
      Record r, pr;
      bool signal, recModified, dataModified, remove;
      DateTime thr;
      uint parentPos, oldFl_Size;
      int oldDataSize;
      byte[] rBuf=new byte[64], dBuf=Record.dBuf;
      int freeCnt=0;

      do {
        signal=_work.WaitOne(850);
        _now=DateTime.Now;
        if(signal) {
          while(_ch.TryDequeue(out tCh)) {
            r=GetRecord(tCh);
            if(r==null) {
              continue;
            }
            r.modifyDT=_now;
            if(r.pos!=0) {
              for(var i=_recordsToSave.First; i!=null; i=i.Next) {
                if(i.Value==r) {
                  _recordsToSave.Remove(i);
                  break;
                }
              }
            }
            _recordsToSave.AddLast(r);
          }
        }
        signal=false;
        thr=_now.AddMilliseconds(-2630);
        while(_recordsToSave.First!=null && (r=_recordsToSave.First.Value)!=null && (r.pos==0 || r.modifyDT<thr || _terminate)) {
          _recordsToSave.RemoveFirst();
          remove=r.t.disposed;
          recModified=false;
          dataModified=false;

          if(r.t==Topic.root || remove) {
            parentPos=0;
          } else {
            pr=GetRecord(r.t.parent);
            if(pr==null) {
              Log.Warning("PersistentStaorage: parent for {0} not found", r.t.path);
              continue;
            }
            if(pr.pos==0) {
              LinkedListNode<Record> i;
              for(i=_recordsToSave.First; i!=null; i=i.Next) {
                if(i.Value==pr) {
                  _recordsToSave.AddAfter(i, r);
                  break;
                }
              }
              if(i==null) {
                _recordsToSave.AddFirst(r);
                _recordsToSave.AddFirst(pr);
              }
              continue;
            }
            parentPos=pr.pos;
          }

          if(remove) {
            if(r.saved_fl==FL_SAVED_E) {
              AddFree(r.data_pos, r.data_size+6);
            }
            AddFree(r.pos, r.size);
            _tr.Remove(r.t);
            signal=true;
            continue;
          } else {
            oldFl_Size=r.fl_size;
            oldDataSize=r.data_size+6;
            if(r.parent!=parentPos) {
              r.parent=parentPos;
              recModified=true;
            }
            if(r.t==Topic.root) {
              if(r.name!="/") {
                r.name="/";
                recModified=true;
              }
            } else if(r.name!=r.t.name) {
              r.name=r.t.name;
              recModified=true;
            }
            r.fl_size=FL_RECORD | (uint)(14+Encoding.UTF8.GetByteCount(r.name));
            string cPayload=(r.t.valueType!=null && r.t.saved)?r.t.ToJson():string.Empty;
            if(r.t.valueType!=null) {
              if(r.payload!=cPayload || r.t.valueType!=r.type) {
                r.payload=cPayload;
                r.type=r.t.valueType;
                dataModified=true;
              }
              byte[] tBuf=Encoding.UTF8.GetBytes(WOUM.ExConverter.Type2Name(r.type));
              byte[] pBuf=Encoding.UTF8.GetBytes(r.payload);
              r.data_size=tBuf.Length+1+pBuf.Length;
              if(dBuf==null || dBuf.Length<((r.data_size+6+15)&FL_LEN_MASK)) {
                dBuf=new byte[(r.data_size+6+15)&FL_LEN_MASK];
              }
              Buffer.BlockCopy(tBuf, 0, dBuf, 4, tBuf.Length);
              dBuf[tBuf.Length+4]=0;
              Buffer.BlockCopy(pBuf, 0, dBuf, tBuf.Length+5, pBuf.Length);
              if(r.data_size>0 && r.size+r.data_size<64) {
                r.fl_size=(r.fl_size+(uint)r.data_size) | FL_SAVED_I;
              } else {
                r.fl_size|=FL_SAVED_E;
              }
            } else {
              r.payload=string.Empty;
              r.data_size=0;
              r.saved_fl=0;
              if(oldDataSize>0) {
                dataModified=true;
              }
            }
            if(r.t.saved) {
              r.fl_size|=FL_SAVED;
            }
          }
          if(rBuf==null || rBuf.Length<((r.size+15)&FL_LEN_MASK)) {
            rBuf=new byte[(r.size+15)&FL_LEN_MASK];
          }
          if(r.data_size>0) {
            if(r.saved_fl==FL_SAVED_I) {
              CopyBytes(r.data_size, rBuf, 8);
              Buffer.BlockCopy(dBuf, 4, rBuf, r.size-r.data_size-2, r.data_size);
              if(r.data_pos>0 && oldDataSize>0) {  // FL_SAVED_E -> FL_SAVED_I
                AddFree(r.data_pos, oldDataSize);
                r.data_pos=0;
                signal=true;
              }
              if(dataModified) {
                recModified=true;
              }
            } else {
              if(dataModified) {
                CopyBytes(6+r.data_size, dBuf, 0);
                if(Write(ref r.data_pos, dBuf, oldDataSize, r.data_size+6)) {
                  recModified=true;
                }
                signal=true;
                if(_verbose.value) {
                  Log.Debug("D [{0:X4}]({1}) {2}{3}", r.data_pos<<4, r.data_size+6, r.t.path, r.type==null?" $":string.Concat("<", WOUM.ExConverter.Type2Name(r.type), ">=", (r.t.saved)?r.t.ToJson():" $"));
                }
              }
              CopyBytes(r.data_pos, rBuf, 8);
            }
          } else {
            CopyBytes(r.data_size, rBuf, 8);
            if(r.data_pos>0 && oldDataSize>0) {  // new data_size==0
              AddFree(r.data_pos, oldDataSize);
              r.data_pos=0;
              recModified=true;
              signal=true;
            }
          }
          if(recModified || r.fl_size!=oldFl_Size) {
            CopyBytes(r.fl_size, rBuf, 0);
            CopyBytes(r.parent, rBuf, 4);
            Encoding.UTF8.GetBytes(r.name).CopyTo(rBuf, 12);
            if(Write(ref r.pos, rBuf, (int)oldFl_Size & FL_REC_LEN, r.size)) {
              var ch=r.t.children.ToArray();
              for(int i=ch.Length-1; i>=0; i--) {
                if(!ch[i].path.StartsWith("/local") && ch[i].path!="/var/log/A0") {
                  pr=GetRecord(ch[i]);
                  if(pr!=null) {
                    _recordsToSave.AddLast(pr);
                  }
                }
              }
            }
            signal=true;
            if(_verbose.value) {
              Log.Debug("S [{0:X4}]({1}) {2}{3}", r.pos<<4, r.size, r.t.path, r.saved_fl==FL_SAVED_I?(string.Concat("<", WOUM.ExConverter.Type2Name(r.type), ">=", (r.t.saved)?r.t.ToJson():" $")):(r.type==null?" $":string.Empty));
            }
          }
        }
        if(!signal) {
          if(_nextBak<_now) {
            Backup();
          } else if(_freeBlocks.Count>freeCnt+30) {
            freeCnt=_freeBlocks.Count;
            List<ulong> rem=null;
            int fr_sz;
            long fr_pos;
            foreach(var b in _freeBlocks.OrderByDescending(z => (uint)z)) {
              fr_pos=(((long)(uint)b)<<4);
              fr_sz=(((int)(b>>32)+15)&FL_LEN_MASK);
              if(fr_pos + fr_sz >=_fileLength) {
                _fileLength=fr_pos;
                if(rem==null) {
                  rem=new List<ulong>();
                }
                rem.Add(b);
              }
            }
            if(rem!=null) {
              for(int i=0; i<rem.Count; i++) {
                _freeBlocks.Remove(rem[i]);
              }
              if(_fileLength<_file.Length) {
                _file.SetLength(_fileLength);
                signal=true;
                if(_verbose.value) {
                  Log.Debug("# {0:X4}", _fileLength);
                }
              }
            }
          }
        }
        if(signal) {
          _file.Flush(true);
        }
      } while(!_terminate);
    }
    private Record GetRecord(Topic t) {
      Record rec;
      if(!_tr.TryGetValue(t, out rec)) {
        if(t.disposed) {
          return null;
        }
        rec=new Record(t);
        _tr[t]=rec;
      }
      return rec;
    }

    private void Backup() {
      _nextBak=_now.AddDays(1);
      string fn="../data/"+LongToString(_now.Ticks*3/2000000000L)+".bak";  // 1/66(6)Sec.
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
    private bool Write(ref uint pos, byte[] buf, int oldSize, int curSize) {
      int bufSize=((curSize+15)&FL_LEN_MASK);
      if(buf==null || curSize<6 || buf.Length<bufSize) {
        throw new ArgumentException("curSize");
      }
      oldSize=((oldSize+15)&FL_LEN_MASK);
      uint oldPos=pos;
      CopyBytes(Crc16.ComputeChecksum(buf, curSize-2), buf, curSize-2);
      if(bufSize!=oldSize) {
        AddFree(pos, oldSize);
        pos=FindFree(curSize);
      }
      for(int i=curSize; i<bufSize; i++) {
        buf[i]=0;
      }
      _file.Position=(long)pos<<4;
      _file.Write(buf, 0, bufSize);
      return pos!=oldPos;
    }
    private Topic AddTopic(Topic parent, Record r) {
      Topic t=null;

      if(parent==null) {
        if(r.name=="/") {
          t=Topic.root;
        }
      } else {
        t=Topic.GetP(r.name, r.type, _sign, parent);
      }
      if(t!=null) {
        r.t=t;
        t.saved=r.saved;
        if(_verbose.value) {
          Log.Debug("L [{0:X4}]{1}<{2}>={3}", r.pos<<4, t.path, r.type, r.payload);
        }
        _tr[t]=r;
        int idx;
        while((idx=indexPPos(_refitParent, r.pos))>=0 && idx<_refitParent.Count && _refitParent[idx].parent==r.pos) {
          Record nextR=_refitParent[idx];
          _refitParent.RemoveAt(idx);
          AddTopic(t, nextR);
        }
        if(!string.IsNullOrEmpty(r.payload)) {
          t.FromJson(r.payload, _sign);
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
      if(pos==0 || size==0) {
        return;
      }
      int sz=(size+15)&FL_LEN_MASK;
      if(((uint)size & FL_REMOVED)==0) {
        if(_writeBuf==null || _writeBuf.Length<sz) {
          _writeBuf=new byte[sz];
        }
        CopyBytes((uint)sz | FL_REMOVED, _writeBuf, 0);
        for(int i=4; i<sz; i++) {
          _writeBuf[i]=0;
        }
        _file.Position=(long)pos<<4;
        _file.Write(_writeBuf, 0, sz);

      }
      _freeBlocks.Add((ulong)sz<<32 | pos);
      if(_verbose.value) {
        Log.Debug("F [{0:X4}]({1}/{2})", pos<<4, size, sz);
      }
    }
    private uint FindFree(int size) {
      size=(size+15)&FL_LEN_MASK;
      uint rez;
      ulong p=_freeBlocks.GetViewBetween((ulong)size<<32, (ulong)(size+1)<<32).FirstOrDefault();
      if((int)(p>>32)==size) {
        _freeBlocks.Remove(p);
        rez=(uint)p;
      } else {
        rez=(uint)((_fileLength+15)>>4);
        _fileLength=((long)rez<<4)+size;
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

    private class Record {
      public static byte[] dBuf;

      public Topic t;
      public DateTime modifyDT;
      public uint fl_size;
      public uint pos;
      public uint parent;
      public uint data_pos;
      public int data_size;
      public string name;
      public string payload;
      public Type type;

      public Record(uint pos, byte[] buf, FileStream file) {
        this.pos=pos;
        this.fl_size=BitConverter.ToUInt32(buf, 0);
        parent=BitConverter.ToUInt32(buf, 4);
        uint dataPS=BitConverter.ToUInt32(buf, 8);
        if(dataPS>0) {
          if((fl_size&FL_SAVED_A)==FL_SAVED_I) {
            data_pos=0;
            data_size=(int)dataPS;
            if(dBuf==null || dBuf.Length<((data_size+6+15)&FL_LEN_MASK)) {
              dBuf=new byte[(data_size+6+15)&FL_LEN_MASK];
            }
            Buffer.BlockCopy(buf, size-data_size-2, dBuf, 4, data_size);
          } else {  // saved_fl==FL_SAVED_E
            data_pos=dataPS;
            byte[] lBuf=new byte[4];
            file.Position=(long)data_pos<<4;
            file.Read(lBuf, 0, 4);
            data_size=BitConverter.ToInt32(lBuf, 0);
            if((data_size & (FL_REMOVED | FL_RECORD))==0) {
              if(data_size<4) {
                throw new ApplicationException(string.Format("DataStorage: mismatch data size, record @0x{0:X8} {1}", (long)data_pos<<4, name));
              } else {
                data_size=data_size-6;
                if(dBuf==null || dBuf.Length<((data_size+6+15)&FL_LEN_MASK)) {
                  dBuf=new byte[(data_size+6+15)&FL_LEN_MASK];
                }
                Buffer.BlockCopy(lBuf, 0, dBuf, 0, 4);
                file.Read(dBuf, 4, data_size);
                ushort crc1=BitConverter.ToUInt16(buf, size-2);
                ushort crc2=Crc16.ComputeChecksum(buf, size-2);
                if(crc1!=crc2) {
                  throw new ApplicationException("DataStorage: CRC Error Data @0x"+((long)data_pos<<4).ToString("X8"));
                }
              }
            }
          }

          if(data_size>1) {
            int idx=4;
            while(idx<data_size+4 && dBuf[idx]!=0) {
              idx++;
            }
            if(idx<data_size+4) {
              string ts=Encoding.UTF8.GetString(dBuf, 4, idx-4);
              type=X13.WOUM.ExConverter.FullName2Type(ts);
              idx++;  // delimiter '\0'
              if(saved && idx<data_size+4) {
                payload=Encoding.UTF8.GetString(dBuf, idx, data_size-idx+4);
              } else {
                payload=string.Empty;
              }
            }
          }
        } else {
          payload=string.Empty;
          type=null;
          data_pos=0;
          data_size=0;
        }
        name=Encoding.UTF8.GetString(buf, 12, (int)(size-14-(saved_fl==FL_SAVED_I?data_size:0)));
      }
      public Record(Topic t) {
        this.t=t;
      }

      public uint saved_fl { get { return fl_size&FL_SAVED_A; } set { fl_size=(fl_size & ~FL_SAVED_A) | (value & FL_SAVED_A); } }
      public bool saved { get { return (fl_size&FL_SAVED)!=0; } set { fl_size=(value?fl_size|FL_SAVED : fl_size&~FL_SAVED); } }
      public bool local { get { return (fl_size&FL_LOCAL)!=0; } set { fl_size=value?fl_size|FL_LOCAL : fl_size&~FL_LOCAL; } }
      public bool removed { get { return (fl_size&FL_REMOVED)!=0; } set { fl_size=value?fl_size|FL_REMOVED:fl_size&~FL_REMOVED; } }
      public int size { get { return (int)fl_size & FL_REC_LEN; } }
    }
  }
}
