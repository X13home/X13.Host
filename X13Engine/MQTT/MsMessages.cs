#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Linq;
using System.Text;
using X13.PLC;
using System.Collections.Generic;

namespace X13.MQTT {
  internal enum MsMessageType : byte {
    ADVERTISE=0,
    SEARCHGW=1,
    GWINFO=2,
    CONNECT=4,
    CONNACK=5,
    WILLTOPICREQ=6,
    WILLTOPIC=7,
    WILLMSGREQ=8,
    WILLMSG=9,
    REGISTER=0x0A,
    REGACK=0x0B,
    PUBLISH=0x0C,
    PUBACK=0x0D,
    PUBCOMP=0x0E,
    PUBREC=0x0F,
    PUBREL=0x10,
    SUBSCRIBE=0x12,
    SUBACK=0x13,
    UNSUBSCRIBE=0x14,
    UNSUBACK=0x15,
    PINGREQ=0x16,
    PINGRESP=0x17,
    DISCONNECT=0x18,
    WILLTOPICUPD=0x1A,
    WILLTOPICRESP=0x1B,
    WILLMSGUPD=0x1C,
    WILLMSGRESP=0x1D,
    EncapsulatedMessage=0xFE,
  }
  internal enum MsReturnCode : byte {
    Accepted=0,
    Congestion=1,
    InvalidTopicId=2,
    NotSupportes=3
  }

  internal class MsMessage {
    protected static UTF8Encoding enc = new UTF8Encoding();

    protected ushort _length;

    public byte Addr;
    public readonly MsMessageType MsgTyp;
    public bool IsRequest { get; protected set; }        // response is required 
    public MsMessageType ReqTyp { get; protected set; }  // message is a response to
    public ushort MessageId { get; set; }

    protected MsMessage(byte[] buf) {
      Addr=buf[0];
      if(buf[1]>1) {
        _length=buf[1];
      } else {
        _length=(ushort)((buf[2]<<8) | (buf[3]));
      }
      if(buf.Length!=_length+1) {
        throw new ArgumentException("length is not correct");
      }
      MsgTyp=(MsMessageType)(buf[1]>0?buf[2]:buf[4]);
    }
    public MsMessage(MsMessageType type) {
      MsgTyp=type;
      _length=2;
      switch(MsgTyp) {
      case MsMessageType.WILLMSGREQ:
      case MsMessageType.WILLTOPICREQ:
        this.IsRequest=true;
        break;
      }
    }
    public virtual byte[] GetBytes() {
      if(_length>255) {
        _length+=2;
      }
      byte[] rez=new byte[_length+1];
      int ptr=0;
      rez[ptr++]=Addr;
      if(_length>255) {
        rez[ptr++]=1;
        rez[ptr++]=(byte)(_length>>8);
        rez[ptr++]=(byte)(_length);
      } else {
        rez[ptr++]=(byte)(_length);
      }
      rez[ptr]=(byte)MsgTyp;

      return rez;
    }

    public override string ToString() {
      return MsgTyp.ToString();
    }
  }

  internal class MsAdvertise : MsMessage {
    private byte[] _buf;
    public MsAdvertise(byte gwId, ushort duration)
      : base(MsMessageType.ADVERTISE) {
        base._length=5;
        _buf=base.GetBytes();
        _buf[3]=gwId;
        _buf[4]=(byte)(duration>>8);
        _buf[5]=(byte)duration;
    }
    public override byte[] GetBytes() {
      return _buf;
    }
  }

  internal class MsSearchGW : MsMessage {
    public MsSearchGW(byte[] buf)
      : base(buf) {
        radius=buf[2];
    }
    public readonly byte radius;
  }

  internal class MsGwInfo : MsMessage {
    private byte _gwAddr;
    public MsGwInfo(byte addr, byte gwAddr) : base(MsMessageType.GWINFO) {
      _gwAddr=gwAddr;
      base.Addr=addr;
    }
    public override byte[] GetBytes() {
      base._length=3;
      byte[] buf=base.GetBytes();
      buf[3]=_gwAddr;
      return buf;
    }
  }

  internal class MsConnect : MsMessage {
    public MsConnect(byte[] buf)
      : base(buf) {
      int ptr=buf[1]==1?5:3;
      Will=(buf[ptr] & 8)!=0;
      CleanSession=(buf[ptr] & 4)!=0;
      ptr++;
      if(buf[ptr++]!=1) {
        throw new ArgumentException("Unknown ProtocolId");
      }
      Duration=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      ClientId=enc.GetString(buf, ptr, 1+_length-ptr);
    }

    public bool CleanSession;
    public bool Will;
    public ushort Duration;
    public string ClientId;

    public override byte[] GetBytes() {
      throw new ApplicationException("MsConnect.GetBytes() not supported");
    }
  }

  internal class MsConnack : MsMessage {
    private MsReturnCode _retCode;
    public MsConnack(MsReturnCode code)
      : base(MsMessageType.CONNACK) {
      _retCode=code;
    }
    public override byte[] GetBytes() {
      base._length=3;
      byte[] buf=base.GetBytes();
      buf[3]=(byte)_retCode;
      return buf;
    }
  }

  internal class MsWillTopic : MsMessage {
    public MsWillTopic(byte[] buf)
      : base(buf) {
      int ptr=buf[1]==1?5:3;
      this.Retain=(buf[ptr] & 0x10)!=0;
      ptr++;
      if(1+_length-ptr>0) {
        this.Path=enc.GetString(buf, ptr, 1+_length-ptr);
      } else {
        this.Path=string.Empty;
      }
      base.ReqTyp=MsMessageType.WILLTOPICREQ;
    }
    public readonly string Path;
    public readonly bool Retain;

    public override byte[] GetBytes() {
      throw new ApplicationException("MsWillTopic.GetBytes() not supported");
    }
  }

  internal class MsWillMsg : MsMessage {
    public MsWillMsg(byte[] buf)
      : base(buf) {
      int ptr=buf[1]==1?5:3;
      if(1+_length-ptr>0) {
        Payload=buf.Skip(ptr).ToArray();
      }
      base.ReqTyp=MsMessageType.WILLMSGREQ;
    }
    public readonly byte[] Payload;

    public override byte[] GetBytes() {
      throw new ApplicationException("MsWillMsg.GetBytes() not supported");
    }
  }

  internal class MsSubscribe : MsMessage {
    public MsSubscribe(byte[] buf)
      : base(buf) {
      int ptr=buf[1]==1?5:3;
      dup=(buf[ptr] & 0x80)!=0;
      qualityOfService=(QoS)((buf[ptr] >>5) & 0x03);
      topicIdType=(TopicIdType)(buf[ptr] & 0x03);
      ptr++;
      base.MessageId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      if(topicIdType==TopicIdType.PreDefined || topicIdType==TopicIdType.ShortName) {
        topicId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
        path=null;
      } else if(topicIdType==TopicIdType.Normal) {
        topicId=0;
        path=enc.GetString(buf, ptr, buf.Length-ptr);
        if(string.IsNullOrEmpty(path)) {
          throw new ArgumentException("empty path");
        }
      } else {
        throw new ArgumentException("unknown MsSubscribe.topicIdType");
      }
    }
    public override byte[] GetBytes() {
      throw new ApplicationException("MsSubscribe.GetBytes() not supported");
    }

    public bool dup;
    public readonly QoS qualityOfService;
    public readonly ushort topicId;
    public readonly string path;
    public readonly TopicIdType topicIdType;
  }

  internal class MsSuback : MsMessage {
    public MsSuback(QoS qualityOfService, ushort topicId, ushort msgId, MsReturnCode code)
      : base(MsMessageType.SUBACK) {
      _qualityOfService=qualityOfService;
      _topicId=topicId;
      _msgId=msgId;
      _retCode=code;
    }
    public override byte[] GetBytes() {
      base._length=8;
      byte[] buf=base.GetBytes();
      buf[3]=(byte)(((int)_qualityOfService)<<5);
      buf[4]=(byte)(_topicId>>8);
      buf[5]=(byte)_topicId;
      buf[6]=(byte)(_msgId>>8);
      buf[7]=(byte)_msgId;
      buf[8]=(byte)_retCode;
      return buf;
    }

    private QoS _qualityOfService;
    private ushort _topicId;
    private ushort _msgId;
    private MsReturnCode _retCode;
  }

  internal class MsRegister : MsMessage {
    public MsRegister(ushort topicId, string topicPath)
      : base(MsMessageType.REGISTER) {
      this.TopicId=topicId;
      this.TopicPath=topicPath;
      this.IsRequest=true;
    }
    public MsRegister(byte[] buf)
      : base(buf) {
      int ptr=buf[1]==1?5:3;
      TopicId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      MessageId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      TopicPath=enc.GetString(buf, ptr, buf.Length-ptr);
    }
    public override byte[] GetBytes() {
      base._length=(ushort)(6+enc.GetByteCount(TopicPath));
      byte[] buf=base.GetBytes();
      int ptr=buf[1]==1?5:3;
      buf[ptr++]=(byte)(TopicId>>8);
      buf[ptr++]=(byte)(TopicId);
      buf[ptr++]=(byte)(MessageId>>8);
      buf[ptr++]=(byte)(MessageId);
      enc.GetBytes(TopicPath).CopyTo(buf, ptr);
      return buf;
    }

    public readonly ushort TopicId;
    public readonly string TopicPath;
    public override string ToString() {
      return string.Format("MsRegister {0}[{1:X4}]", TopicPath, TopicId);
    }
  }

  internal class MsRegAck : MsMessage {
    public MsRegAck(ushort topicId, ushort messageId, MsReturnCode code)
      : base(MsMessageType.REGACK) {
      this.TopicId=topicId;
      this.MessageId=messageId;
      this.RetCode=code;
    }
    public MsRegAck(byte[] buf)
      : base(buf) {
      int ptr=buf[1]==1?5:3;
      this.TopicId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this.MessageId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this.RetCode=(MsReturnCode)buf[ptr];
      this.ReqTyp=MsMessageType.REGISTER;
    }

    public override byte[] GetBytes() {
      base._length=7;
      byte[] buf=base.GetBytes();
      int ptr=buf[1]==1?5:3;
      buf[ptr++]=(byte)(TopicId>>8);
      buf[ptr++]=(byte)(TopicId);
      buf[ptr++]=(byte)(MessageId>>8);
      buf[ptr++]=(byte)(MessageId);
      buf[ptr]=(byte)RetCode;
      return buf;
    }

    public readonly ushort TopicId;
    public readonly MsReturnCode RetCode;

    public override string ToString() {
      return string.Format("MsRegAck [{0:X4}] {1}", TopicId, RetCode);
    }
  }

  internal class MsPublish : MsMessage {
    private Topic _val;
    private byte[] _payload;

    public MsPublish(Topic val, ushort topicId, QoS qualityOfService)
      : base(MsMessageType.PUBLISH) {
      this.IsRequest=qualityOfService!=QoS.AtMostOnce;
      this.qualityOfService=qualityOfService;
      this.TopicId=topicId;
      this._val=val;
      PredefinedTopics tmp;
      if(_val==null || Enum.TryParse<PredefinedTopics>(_val.name, out tmp)) {
        this.topicIdType=TopicIdType.PreDefined;
      }
    }
    public MsPublish(byte[] buf)
      : base(buf) {
      int ptr=buf[1]==1?5:3;
      this.Dup=(buf[ptr]&0x80)!=0;
      this.qualityOfService=(QoS)((buf[ptr]>>5)&0x03);
      this.Retained=(buf[ptr]&0x10)!=0;
      topicIdType=(TopicIdType)(buf[ptr] & 0x03);
      ptr++;
      this.TopicId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this.MessageId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this._payload=buf.Skip(ptr).ToArray();
    }
    public override byte[] GetBytes() {
      byte[] tmp=this.Data;
      base._length=(ushort)(7+tmp.Length);
      byte[] buf=base.GetBytes();
      int ptr=buf[1]==1?5:3;
      buf[ptr++]=(byte)((Dup?0x80:0) | (((int)qualityOfService)<<5) | (Retained?0x10:0) | ((int)topicIdType));
      buf[ptr++]=(byte)(TopicId>>8);
      buf[ptr++]=(byte)(TopicId);
      buf[ptr++]=(byte)(MessageId>>8);
      buf[ptr++]=(byte)(MessageId);
      Array.Copy(tmp, 0, buf, ptr, tmp.Length);
      Dup=true;
      return buf;
    }
    public bool Dup;
    public readonly bool Retained;
    public readonly QoS qualityOfService;
    public readonly TopicIdType topicIdType;
    public readonly ushort TopicId;
    public byte[] Data { get { return _payload??Serialize(_val); } set { _payload=value; } }

    private static byte[] Serialize(Topic t) {
      List<byte> ret=new List<byte>();
      switch(Type.GetTypeCode(t.valueType)) {
      case TypeCode.Boolean:
        ret.Add((byte)((bool)t.GetValue()?1:0));
        break;
      case TypeCode.Byte:
        ret.Add((byte)t.GetValue());
        break;
      case TypeCode.SByte:
        ret.Add((byte)(sbyte)t.GetValue());
        break;
      case TypeCode.Int16: {
          short v=(short)t.GetValue();
          ret.Add((byte)v);
          ret.Add((byte)(v>>8));
        }
        break;
      case TypeCode.UInt16: {
          ushort v=(ushort)t.GetValue();
          ret.Add((byte)v);
          ret.Add((byte)(v>>8));
        }
        break;
      case TypeCode.Int32: {
          int v=(int)t.GetValue();
          ret.Add((byte)v);
          ret.Add((byte)(v>>8));
          ret.Add((byte)(v>>16));
          ret.Add((byte)(v>>24));
        }
        break;
      case TypeCode.UInt32: {
          uint v=(uint)t.GetValue();
          ret.Add((byte)v);
          ret.Add((byte)(v>>8));
          ret.Add((byte)(v>>16));
          ret.Add((byte)(v>>24));
        }
        break;
      case TypeCode.String: {
          string v=(string)t.GetValue();
          if(!string.IsNullOrEmpty(v)) {
            ret.AddRange(Encoding.Default.GetBytes(v));
          }
        }
        break;
      case TypeCode.Object:
        if(t.valueType==typeof(byte[])) {
          ret.AddRange((byte[])t.GetValue());
        }
        break;
      }
      return ret.ToArray();
    }

    public override string ToString() {
      if(_val!=null) {
        return string.Format("MsPublish {0}[{1:X4}]", _val.name, TopicId);
      } else {
        return string.Format("MsPublish [{0:X4}]", TopicId);
      }
    }
  }

  internal class MsPubAck : MsMessage {
    public MsPubAck(ushort topicId, ushort messageId, MsReturnCode retCode)
      : base(MsMessageType.PUBACK) {
      this.TopicId=topicId;
      this.MessageId=messageId;
      this.retCode=retCode;
    }
    public MsPubAck(byte[] buf)
      : base(buf) {
      int ptr=3;
      this.TopicId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this.MessageId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this.retCode=(MsReturnCode)buf[ptr];
      this.ReqTyp=MsMessageType.PUBLISH;
    }
    public override byte[] GetBytes() {
      base._length=7;
      byte[] buf=base.GetBytes();
      int ptr=3;
      buf[ptr++]=(byte)(TopicId>>8);
      buf[ptr++]=(byte)(TopicId);
      buf[ptr++]=(byte)(MessageId>>8);
      buf[ptr++]=(byte)(MessageId);
      buf[ptr]=(byte)retCode;
      return buf;
    }
    public readonly ushort TopicId;
    public readonly MsReturnCode retCode;
    public override string ToString() {
      return string.Format("PUBACK {0}", retCode.ToString());
    }
  }

  internal class MsPingReq : MsMessage {
    public MsPingReq(byte[] buf)
      : base(buf) {
      int ptr=buf[1]==1?5:3;
      if(1+_length-ptr>0) {
        ClientId=enc.GetString(buf, ptr, 1+_length-ptr);
      } else {
        ClientId=string.Empty;
      }
    }

    public string ClientId;

    public override byte[] GetBytes() {
      throw new ApplicationException("MsPingReq.GetBytes() unsupported");
    }

  }

  internal class MsDisconnect : MsMessage {
    public MsDisconnect()
      : base(MsMessageType.DISCONNECT) {
    }
    public MsDisconnect(byte[] buf)
      : base(buf) {
      if(buf.Length==5) {
        Duration=(ushort)((buf[3]<<8) | buf[4]);
      }
    }
    public readonly ushort Duration;
  }
}
