﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.Periphery {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class DevicePLC : ITopicOwned {
    private static DVar<bool> _verbose;

    static DevicePLC() {
      _verbose=Topic.root.Get<bool>("/etc/MQTT-SN/PLC/verbose");
    }
    private Topic _owner;
    private MsDevice _dev;
    private int _offset, _st;  // 0 - idle, 1 - check CRC, 2- check CRC resp, 3-PLC stop, 4 - PLC stop resp, 5- write block, 6- write block resp, 7- PLC start resp
    private Chunk _curChunk;
    private SortedSet<Chunk> _prg;
    private bool _plcStoped;

    public DevicePLC() {
      _prg=new SortedSet<Chunk>();
      if(signature==0) {
        signature=(new Random()).Next(1, int.MaxValue);
      }
    }
    public DevicePLC(Topic owner) {
      SetOwner(owner);
    }

    [Newtonsoft.Json.JsonProperty]
    public int signature { get; set; }

    public void Reset() {
      _st=1;
    }
    public void Recv(byte[] msgData) {
      if(msgData==null || msgData.Length==0) {
        return;
      }
      if(_st==2 && msgData[0]==(byte)Cmd.GetCRCResp) {
        if(_curChunk!=null && msgData.Length==4 && msgData[1]==0) {
          _curChunk.crcDev=(msgData[3]<<8) | msgData[2];
          if(_curChunk.crcDev==_curChunk.crcCur) {
            _curChunk=null;
            _st=1;
          } else {
            _st=_plcStoped?5:3;
          }
        } else {
          Log.Error("{0}.Recv({1}) ch={2}", _owner, BitConverter.ToString(msgData), _curChunk==null?"null":_curChunk.ToString());
          _st=1;
        }
      } else if(_st==4 && msgData[0]==(byte)Cmd.PlcStopResp){
        _plcStoped=true;
        _st=_curChunk==null?1:5;
      } else if(_st==6 && msgData.Length==2 && msgData[0]==(byte)Cmd.WriteBlockResp) {
        if(msgData[1]==0) {  // success
          _offset+=32;
          if(_offset>_curChunk.Data.Length) {
            _curChunk.crcDev=_curChunk.crcCur;
            _curChunk=null;
            _st=1;
            _offset=0;
          } else {
            _st=5;
          }
        } else {
          Log.Error("{0}.Recv({1}) {2}", _owner, BitConverter.ToString(msgData), ((Cmd)msgData[0]));
          _st=1;
        }
      } else if(_st==7 && msgData[0]==(byte)Cmd.PlcStartResp) {
        _st=0;
        _plcStoped=false;
      } else if(_verbose.value) {
        Log.Warning("{0}.Recv({1}) {2}", _owner, BitConverter.ToString(msgData), ((Cmd)msgData[0]));
      }
    }
    private void Pool() {
      if(_dev==null || _owner==null) {
        return;
      }
      if(_dev.state!=MsDevice.State.Connected && _dev.state!=MsDevice.State.AWake && _dev.state!=MsDevice.State.ASleep) {
        return;
      }
      byte[] buf;

      if(_st==0) {
        return;
      } else if(_curChunk!=null) {
        if(_st==1) {
          if(_curChunk==null) {
            _curChunk=_prg.FirstOrDefault(z => z.crcCur!=z.crcDev);
            if(_curChunk==null) {
              if(_plcStoped) {
                buf=new byte[] { (byte)Cmd.PlcStartReq };
                _st=7;
                _dev.PublishWithPayload(_owner, buf);
              } else {
                _st=0;
              }
              return;
            }
          }
          buf=new byte[] { (byte)Cmd.GetCRCReq, (byte)_curChunk.offset, (byte)(_curChunk.offset>>8), (byte)_curChunk.Data.Length, (byte)(_curChunk.Data.Length>>8) };
          _dev.PublishWithPayload(_owner, buf);
          _st=2;
        } else if(_st==3){
          buf=new byte[] { (byte)Cmd.PlcStopReq };
          _st=4;
          _dev.PublishWithPayload(_owner, buf);
        } else if(_st==5) {
          int len=_curChunk.Data.Length-_offset;
          if(len>32) {
            len=32;
          }
          buf=new byte[len+5];
          int addr=_curChunk.offset+_offset;
          buf[0]=(byte)(Cmd.WriteBlockReq);
          buf[1]=(byte)addr;
          buf[2]=(byte)(addr>>8);
          Buffer.BlockCopy(_curChunk.Data, _offset, buf, 3, len);
          ushort crc=0;
          for(int i=0; i<len; i++) {
            crc=Crc16.UpdateChecksum(crc, buf[i+3]);
          }
          buf[len+3]=(byte)crc;
          buf[len+4]=(byte)(crc>>8);
          _st=6;
          _dev.PublishWithPayload(_owner, buf);
        }
      } else {
        _st=1;
      }
    }
    public void SetOwner(Topic owner) {
      if(owner==_owner) {
        return;
      }
      if(_owner!=null) {
        if(Topic.brokerMode) {
          _owner.Unsubscribe("+", VarChanged);
        }
      }
      if(_dev!=null) {
        _dev.Pool-=Pool;
      }
      _owner=owner;
      if(_owner!=null) {
        //name=owner.name;
        if(Topic.brokerMode) {
          if(_owner.parent!=null && _owner.parent.valueType==typeof(MsDevice)) {
            _dev=(_owner.parent as DVar<MsDevice>).value;
          }
          //_owner.Get<string>("_declarer", _owner).value="TWI";
          _owner.Subscribe("+", VarChanged);
          if(_dev!=null) {
            _dev.Pool+=Pool;
            Reset();
          }
        }
      }
    }
    public override string ToString() {
      return "["+_prg.Count.ToString()+"]";
    }

    private void VarChanged(Topic snd, TopicChanged p) {
      int start;
      if(!snd.name.StartsWith("pa") || snd.valueType!=typeof(PLC.ByteArray) || !int.TryParse(snd.name.Substring(2), out start)) {
        return;
      }
      Chunk ch=_prg.FirstOrDefault(z => z.offset==start);
      if(p.Art==TopicChanged.ChangeArt.Value) {
        var pa=snd.GetValue() as PLC.ByteArray;
        byte[] data=pa==null?null:pa.GetBytes();
        if(ch==null) {
          ch=new Chunk(start);
          _prg.Add(ch);
        }
        if(data==null || ch.Data==null || !data.SequenceEqual(ch.Data)) {
          ch.Data=data;
          ch.crcCur=Crc16.ComputeChecksum(ch.Data);
          if(System.Threading.Interlocked.CompareExchange(ref _st, 1, 0)==0) {
            _curChunk=ch;
          }
        }
      } else if(p.Art==TopicChanged.ChangeArt.Remove) {
        if(ch!=null) {
          _prg.Remove(ch);
        }
      }
    }
    private enum Cmd : byte {
      Idle,
      PlcStartReq,
      PlcStartResp,
      PlcStopReq,
      PlcStopResp,
      GetCRCReq,
      GetCRCResp,
      WriteBlockReq,
      WriteBlockResp,
    }
    private class Chunk : IComparable<Chunk> {
      public int offset;
      public int crcDev;
      public int crcCur;
      public byte[] Data;

      public Chunk(int offset) {
        this.offset=offset;
        crcDev=-1;
      }
      public override string ToString() {
        return offset.ToString("X4")+"["+(Data==null?"null":Data.Length.ToString("X4"))+(crcCur==crcDev?" ok":" !");
      }
      public int CompareTo(Chunk other) {
        if(other==null) {
          return 1;
        }
        return this.offset.CompareTo(other.offset);
      }
    }
  }
}