using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13.Periphery {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class SmartTwi : ITopicOwned {
    private Topic _owner;
    private DVar<string> _decl;
    private MsDevice _dev;
    private List<STVar> _vars;
    private int _state;
    private int _cntTry;
    private Timer _to;

    [Newtonsoft.Json.JsonProperty]
    private int addr { get; set; }

    public SmartTwi(Topic owner) {
      _vars=new List<STVar>();
      _to=new Timer(TO);
      _state=0xB0;
      _cntTry=0;
      SetOwner(owner);
      if(Topic.root.Get<bool>("/local/cfg/MQTTS.Gate/enable").value || Topic.root.Get<bool>("/local/cfg/MQTTS.udp/enable").value) {
        _to.Change(5000, -1);
      }
    }
    internal void Recv(byte[] data) {
      if(_owner==null || data==null) {
        return;
      }
      if(_dev==null) {
        if(_owner.parent!=null && _owner.parent.valueType==typeof(MsDevice)) {
          _dev=(_owner.parent as DVar<MsDevice>).value;
        }
      }

      Log.Debug("{0}.Recv {1}", _owner.name, BitConverter.ToString(data));
      if(data.Length>0 && data[0]==0xB0) {
        _state=0xB0;
      }
      if(_state==0xF0 && data.Length>1) {
        byte addr=data[0];
        var st=_vars.FirstOrDefault(z => z.addr==addr);
        if(st.flType!=0) {
          SetValue(st, data);
        }
      } else if(_state==0xB0) {
        if(data.Length>1 && data[0]==0xB0) {
          string decl=Encoding.UTF8.GetString(data, 1, data.Length-1);
          if(_decl!=null) {
            _decl.value=decl;
          }
          _state=0xC0;
          _dev.PublishWithPayload(_owner, new byte[] { 0xF0, (byte)'r', (byte)_state });
        } else {
          _to.Change(100, -1);
          return;
        }
      } else if(_state>=0xC0 && _state<0xF0) {
        if(data.Length>0 && data[0]==(byte)_state) {
          if(data.Length==1) {
            _dev.PublishWithPayload(_owner, new byte[] { 0xF0, (byte)'I' });
            _state=0xF0;
            _to.Change(-1, -1);
          } else {
            var st=_vars.FirstOrDefault(z => z.addr==data[0]);
            if(st.flType==0) {
              st.addr=data[2];
              st.flType=data[1];
              Topic ptr;
              string name=Encoding.UTF8.GetString(data, 3, data.Length-3);
              switch(st.dateType) {
              case 'z':
                ptr=_owner.Get<bool>(name);
                break;
              case 'b':
              case 'B':
              case 'w':
              case 'W':
              case 'd':
              case 'D':
              case 'q':
                ptr=_owner.Get<long>(name);
                break;
              case 's':
                ptr=_owner.Get<string>(name);
                break;
              case 'a':
                ptr=_owner.Get<PLC.ByteArray>(name);
                break;
              default:
                ptr=null;
                break;
              }
              if(ptr!=null) {
                st.ptr=ptr;
                _vars.Add(st);
              }
            }
            _state++;
            _dev.PublishWithPayload(_owner, new byte[] { 0xF0, (byte)'r', (byte)_state });
          }
        } else {
          _to.Change(100, -1);
          return;
        }
      }
      _cntTry=0;
    }
    internal void Reset() {
      if(_to!=null) {
        _state=0xB0;
        _cntTry=0;
        _to.Change(150, -1);
      }

    }
    public void SetOwner(Topic owner) {
      if(_owner!=null) {
        _owner.Unsubscribe("+", STVarChanged);
      }
      _owner=owner;
      if(_owner!=null) {
        addr=int.Parse(owner.name.Substring(2));
        if(_owner.parent!=null && _owner.parent.valueType==typeof(MsDevice)) {
          _dev=(_owner.parent as DVar<MsDevice>).value;
        }
        _decl=_owner.Get<string>("_declarer", _owner);
        _owner.Subscribe("+", STVarChanged);
        if(_state!=0xB0) {
          Reset();
        }
      } else {
        _decl=null;
        _state=0xB0;
        _to.Change(-1, -1);
      }
    }
    public override string ToString() {
      return _decl==null?base.ToString():_decl.value;
    }
    private void SetValue(STVar st, byte[] data) {
      object o;
      switch(st.dateType) {
      case 'z':
        o=(data[1]!=0);
        break;
      case 'b':
        o=(long)(sbyte)data[1];
        break;
      case 'B':
        o=(long)data[1];
        break;
      case 'w':
        o=(long)(short)((data[2]<<8) | data[1]);
        break;
      case 'W':
        o=(long)(ushort)((data[2]<<8) | data[1]);
        break;
      case 'd':
        o=(long)(int)((data[4]<<24) | (data[3]<<16) | (data[2]<<8) | data[1]);
        break;
      case 'D':
        o=(long)(uint)((data[4]<<24) | (data[3]<<16) | (data[2]<<8) | data[1]);
        break;
      case 'q':
        o=(long)((data[8]<<56) | (data[7]<<48) | (data[6]<<40) | (data[5]<<32) | (data[4]<<24) | (data[3]<<16) | (data[2]<<8) | data[1]);
        break;
      case 's':
        o=Encoding.Default.GetString(data, 1, data.Length-1);
        break;
      case 'a':
        o=new PLC.ByteArray(data.Skip(1).ToArray());
        break;
      default:
        return;
      }
      st.ptr.SetValue(o, new TopicChanged(TopicChanged.ChangeArt.Value, _owner));
    }
    private void STVarChanged(Topic t, TopicChanged a) {
      if(_dev==null || a.Art!=TopicChanged.ChangeArt.Value) {
        return;
      }
      var st=_vars.FirstOrDefault(z => z.ptr==t);
      if(st.ptr!=t || !st.write) {
        return;
      }
      byte[] payload;
      switch(st.dateType) {
      case 'z': {
          payload=new byte[2];
          payload[1]=(byte)((t as DVar<bool>).value?0xFF:0);
        }
        break;
      case 'b': {
          payload=new byte[2];
          payload[1]=(byte)(sbyte)(t as DVar<long>).value;
        }
        break;
      case 'B': {
          payload=new byte[2];
          payload[1]=(byte)(t as DVar<long>).value;
        }
        break;
      case 'w': {
          payload=new byte[3];
          short v=(short)(t as DVar<long>).value;
          payload[1]=(byte)v;
          payload[2]=(byte)(v>>8);
        }
        break;
      case 'W': {
          payload=new byte[3];
          ushort v=(ushort)(t as DVar<long>).value;
          payload[1]=(byte)v;
          payload[2]=(byte)(v>>8);
        }
        break;
      case 'd': {
          payload=new byte[5];
          int v=(int)(t as DVar<long>).value;
          payload[1]=(byte)v;
          payload[2]=(byte)(v>>8);
          payload[3]=(byte)(v>>16);
          payload[4]=(byte)(v>>24);
        }
        break;
      case 'D': {
          payload=new byte[5];
          uint v=(uint)(t as DVar<long>).value;
          payload[1]=(byte)v;
          payload[2]=(byte)(v>>8);
          payload[3]=(byte)(v>>16);
          payload[4]=(byte)(v>>24);
        }
        break;
      case 'q': {
          payload=new byte[9];
          long v=(t as DVar<long>).value;
          payload[1]=(byte)v;
          payload[2]=(byte)(v>>8);
          payload[3]=(byte)(v>>16);
          payload[4]=(byte)(v>>24);
          payload[5]=(byte)(v>>32);
          payload[6]=(byte)(v>>40);
          payload[7]=(byte)(v>>48);
          payload[8]=(byte)(v>>56);
        }
        break;
      case 's': {
          string v=(t as DVar<string>).value;
          if(string.IsNullOrEmpty(v)) {
            payload=new byte[1];
          } else {
            byte[] buf=Encoding.Default.GetBytes(v);
            payload=new byte[buf.Length+1];
            Buffer.BlockCopy(buf, 0, payload, 1, buf.Length);
          }
        }
        break;
      case 'a': {
          PLC.ByteArray v=(t as DVar<PLC.ByteArray>).value;
          if(v!=null) {
            byte[] buf=v.GetBytes();
            payload=new byte[buf.Length+1];
            Buffer.BlockCopy(buf, 0, payload, 1, buf.Length);
          } else {
            payload=new byte[1];
          }
        }
        break;
      default:
        return;
      }
      payload[0]=st.addr;
      _dev.PublishWithPayload(_owner, payload);
    }
    private void TO(object o) {
      if(_dev==null || _owner==null) {
        return;
      }
      _cntTry++;
      if(_state!=0xB0 && _cntTry>3) {
        _state=0xB0;
      } else if(_cntTry>8) {
        _cntTry=8;
      }
      if(_state==0xB0) { // preInit
        _dev.PublishWithPayload(_owner, new byte[] { 0xF0, (byte)'R' });
      } else if(_state>=0xC0 && _state<0xF0) {
        _dev.PublishWithPayload(_owner, new byte[] { 0xF0, (byte)'r', (byte)_state });
      }
      _to.Change(500+_cntTry*_cntTry*500, -1);
    }
    private struct STVar {
      public Topic ptr;
      public byte addr;
      public byte flType;

      public bool write { get { return (flType&0x40)!=0; } }
      public char dateType { get { return (char)((flType & 0x3F)+0x40); } }
    }

  }
}
