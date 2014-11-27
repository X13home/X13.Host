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

namespace X13.Periphery {
  public enum MsMessageType : byte {
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
    DHCP_REQ=0x43,
    DHCP_ACK=0x44,
    EncapsulatedMessage=0xFE,
  }
  internal enum MsReturnCode : byte {
    Accepted=0,
    Congestion=1,
    InvalidTopicId=2,
    NotSupportes=3
  }
  internal enum TopicIdType {
    Normal=0,
    PreDefined=1,
    ShortName=2
  }

  public class MsMessage {
    public const int MSG_MAX_LENGTH=50;

    public static MsMessage Parse(byte[] buf, int start, int end) {
      if(start+1>end) {
        return null;
      }
      var msgTyp=(MsMessageType)(buf[start+0]>1?buf[start+1]:buf[start+3]);
      try {
        switch(msgTyp) {
        case MsMessageType.ADVERTISE:
          return new MsAdvertise(buf, start, end);
        case MsMessageType.SEARCHGW:
          return new MsSearchGW(buf, start, end);
        case MsMessageType.GWINFO:
          return new MsGwInfo(buf, start, end);
        case MsMessageType.CONNECT:
          return new MsConnect(buf, start, end);
        case MsMessageType.WILLTOPIC:
          return new MsWillTopic(buf, start, end);
        case MsMessageType.WILLMSG:
          return new MsWillMsg(buf, start, end);
        case MsMessageType.SUBSCRIBE:
          return new MsSubscribe(buf, start, end);
        case MsMessageType.REGISTER:
          return new MsRegister(buf, start, end);
        case MsMessageType.REGACK:
          return new MsRegAck(buf, start, end);
        case MsMessageType.PUBLISH:
          return new MsPublish(buf, start, end);
        case MsMessageType.PUBACK:
          return new MsPubAck(buf, start, end);
        case MsMessageType.PINGREQ:
          return new MsPingReq(buf, start, end);
        case MsMessageType.DISCONNECT:
          return new MsDisconnect(buf, start, end);
        case MsMessageType.DHCP_REQ:
          return new MsDhcpReq(buf, start, end);
        case MsMessageType.EncapsulatedMessage:
          return new MsForward(buf, start, end);
        }
      }
      catch(Exception) {
      }
      return null;
    }
    protected static UTF8Encoding enc = new UTF8Encoding();

    protected ushort _length;
    private byte[] _sendBuf;
    public readonly MsMessageType MsgTyp;
    public bool IsRequest { get; protected set; }        // response is required 
    public MsMessageType ReqTyp { get; protected set; }  // message is a response to
    public ushort MessageId { get; set; }

    protected MsMessage(byte[] buf, int start, int end) {
      if(buf[start+0]>1) {
        _length=buf[start+0];
      } else {
        _length=(ushort)((buf[start+1]<<8) | (buf[start+2]));
      }
      if(end-start<_length) {
        throw new ArgumentException("length is too small");
      }
      MsgTyp=(MsMessageType)(buf[start+0]>1?buf[start+1]:buf[start+3]);
      _sendBuf=null;
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
      _sendBuf=null;
    }
    public virtual byte[] GetBytes() {
      if(_sendBuf==null) {
        if(_length>MSG_MAX_LENGTH) {
          throw new ArgumentOutOfRangeException(string.Format("Msg is too long {0}", this.ToString()));
        }

        if(_length>255) {
          _length+=2;
        }
        _sendBuf=new byte[_length];
        int ptr=0;
        if(_length>255) {
          _sendBuf[ptr++]=1;
          _sendBuf[ptr++]=(byte)(_length>>8);
          _sendBuf[ptr++]=(byte)(_length);
        } else {
          _sendBuf[ptr++]=(byte)(_length);
        }
        _sendBuf[ptr]=(byte)MsgTyp;

      }
      return _sendBuf;
    }

    public override string ToString() {
      return MsgTyp.ToString();
    }

  }

  internal class MsAdvertise : MsMessage {
    private byte[] _buf;
    public MsAdvertise(byte[] buf, int start, int end)
      : base(buf, start, end) {
    }
    public MsAdvertise(byte gwId, ushort duration)
      : base(MsMessageType.ADVERTISE) {
      base._length=5;
      _buf=base.GetBytes();
      _buf[2]=gwId;
      _buf[3]=(byte)(duration>>8);
      _buf[4]=(byte)duration;
    }
    public override byte[] GetBytes() {
      return _buf;
    }
  }

  internal class MsSearchGW : MsMessage {
    public MsSearchGW(byte[] buf, int start, int end)
      : base(buf, start, end) {
      radius=buf[start+2];
    }
    public readonly byte radius;
  }

  internal class MsGwInfo : MsMessage {
    private byte _gwIdx;
    public MsGwInfo(byte[] buf, int start, int end)
      : base(buf, start, end) {
    }
    public MsGwInfo(byte gwIdx)
      : base(MsMessageType.GWINFO) {
      _gwIdx=gwIdx;
    }
    public override byte[] GetBytes() {
      base._length=3;
      byte[] buf=base.GetBytes();
      buf[2]=_gwIdx;
      return buf;
    }
  }

  internal class MsConnect : MsMessage {
    public MsConnect(byte[] buf, int start, int end)
      : base(buf, start, end) {
      int ptr=buf[start+0]==1?start+4:start+2;
      Will=(buf[ptr] & 8)!=0;
      CleanSession=(buf[ptr] & 4)!=0;
      ptr++;
      if(buf[ptr++]!=1) {
        throw new ArgumentException("Unknown ProtocolId");
      }
      Duration=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      ClientId=enc.GetString(buf, ptr, _length+start-ptr);
    }

    public bool CleanSession;
    public bool Will;
    public ushort Duration;
    public string ClientId;

    public override byte[] GetBytes() {
      throw new NotSupportedException("MsConnect.GetBytes() not supported");
    }
    public override string ToString() {
      return string.Format("MsConnect [{0}{1} {2}] {3}", Will?"W":" ", CleanSession?"C":" ", Duration, ClientId);
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
      buf[2]=(byte)_retCode;
      return buf;
    }
  }

  internal class MsWillTopic : MsMessage {
    public MsWillTopic(byte[] buf, int start, int end)
      : base(buf, start, end) {
      int ptr=buf[start+0]==1?start+4:start+2;
      this.Retain=(buf[ptr] & 0x10)!=0;
      ptr++;
      if(1+_length+start-ptr>0) {
        this.Path=enc.GetString(buf, ptr, _length+start-ptr);
      } else {
        this.Path=string.Empty;
      }
      base.ReqTyp=MsMessageType.WILLTOPICREQ;
    }
    public readonly string Path;
    public readonly bool Retain;

    public override byte[] GetBytes() {
      throw new NotSupportedException("MsWillTopic.GetBytes() not supported");
    }
  }

  internal class MsWillMsg : MsMessage {
    public MsWillMsg(byte[] buf, int start, int end)
      : base(buf, start, end) {
      int ptr=buf[start+0]==1?start+4:start+2;
      if(_length+start-ptr>0) {
        Payload=buf.Skip(ptr).ToArray();
      }
      base.ReqTyp=MsMessageType.WILLMSGREQ;
    }
    public readonly byte[] Payload;

    public override byte[] GetBytes() {
      throw new NotSupportedException("MsWillMsg.GetBytes() not supported");
    }
  }

  internal class MsSubscribe : MsMessage {
    public MsSubscribe(byte[] buf, int start, int end)
      : base(buf, start, end) {
      int ptr=buf[start+0]==1?start+4:start+2;
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
        path=enc.GetString(buf, ptr, _length+start-ptr);
        if(string.IsNullOrEmpty(path)) {
          throw new ArgumentException("empty path");
        }
      } else {
        throw new ArgumentException("unknown MsSubscribe.topicIdType");
      }
    }
    public override byte[] GetBytes() {
      throw new NotSupportedException("MsSubscribe.GetBytes() not supported");
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
      buf[2]=(byte)(((int)_qualityOfService)<<5);
      buf[3]=(byte)(_topicId>>8);
      buf[4]=(byte)_topicId;
      buf[5]=(byte)(_msgId>>8);
      buf[6]=(byte)_msgId;
      buf[7]=(byte)_retCode;
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
    public MsRegister(byte[] buf, int start, int end)
      : base(buf, start, end) {
      int ptr=buf[start+0]==1?start+4:start+2;
      TopicId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      MessageId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      TopicPath=enc.GetString(buf, ptr, _length+start-ptr);
    }
    public override byte[] GetBytes() {
      base._length=(ushort)(6+enc.GetByteCount(TopicPath));
      byte[] buf=base.GetBytes();
      int ptr=buf[0]==1?4:2;
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
      return string.Format("MsRegister [{0:X4}.{1:X4}] {2}", TopicId, MessageId, TopicPath);
    }
  }

  internal class MsRegAck : MsMessage {
    public MsRegAck(ushort topicId, ushort messageId, MsReturnCode code)
      : base(MsMessageType.REGACK) {
      this.TopicId=topicId;
      this.MessageId=messageId;
      this.RetCode=code;
    }
    public MsRegAck(byte[] buf, int start, int end)
      : base(buf, start, end) {
      int ptr=buf[start+0]==1?start+4:start+2;
      this.TopicId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this.MessageId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this.RetCode=(MsReturnCode)buf[ptr];
      this.ReqTyp=MsMessageType.REGISTER;
    }

    public override byte[] GetBytes() {
      base._length=7;
      byte[] buf=base.GetBytes();
      int ptr=buf[0]==1?4:2;
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
      return string.Format("MsRegAck [{0:X4}.{1:X4}] {2}", TopicId, MessageId, RetCode);
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
      if(MsDevice.PredefinedTopics.ContainsValue(topicId) && (_val==null || MsDevice.PredefinedTopics.ContainsKey(_val.name) || MsDevice.PredefinedTopics.ContainsKey(".cfg/"+_val.name))) {
        this.topicIdType=TopicIdType.PreDefined;
      }
    }
    public MsPublish(byte[] buf, int start, int end)
      : base(buf, start, end) {
      int ptr=buf[start+0]==1?start+4:start+2;
      this.Dup=(buf[ptr]&0x80)!=0;
      this.qualityOfService=(QoS)((buf[ptr]>>5)&0x03);
      this.Retained=(buf[ptr]&0x10)!=0;
      topicIdType=(TopicIdType)(buf[ptr] & 0x03);
      ptr++;
      this.TopicId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this.MessageId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this._payload=new byte[end-ptr];
      Buffer.BlockCopy(buf, ptr, this._payload, 0, end-ptr);
    }
    public override byte[] GetBytes() {
      byte[] tmp=this.Data;
      base._length=(ushort)(7+tmp.Length);
      byte[] buf=base.GetBytes();
      int ptr=buf[0]==1?4:2;
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
    public byte[] Data { get { if(_payload==null) _payload=MsDevice.Serialize(_val); return _payload; } set { _payload=value; } }

    public override string ToString() {
      return string.Format("MsPublish [{1:X4}.{3:X4}] {0}={2}", _val!=null?_val.name:"msg", TopicId, Data==null?"null":(BitConverter.ToString(Data)+"["+ Encoding.ASCII.GetString(Data.Select(z => (z<0x20 || z>0x7E)?(byte)'.':z).ToArray())+"]"), MessageId);
    }
  }

  internal class MsPubAck : MsMessage {
    public MsPubAck(ushort topicId, ushort messageId, MsReturnCode retCode)
      : base(MsMessageType.PUBACK) {
      this.TopicId=topicId;
      this.MessageId=messageId;
      this.retCode=retCode;
    }
    public MsPubAck(byte[] buf, int start, int end)
      : base(buf, start, end) {
      int ptr=start+2;
      this.TopicId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this.MessageId=(ushort)((buf[ptr++]<<8) | buf[ptr++]);
      this.retCode=(MsReturnCode)buf[ptr];
      this.ReqTyp=MsMessageType.PUBLISH;
    }
    public override byte[] GetBytes() {
      base._length=7;
      byte[] buf=base.GetBytes();
      int ptr=2;
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
      return string.Format("PUBACK [{0:X4}.{1:X4}] {2}", TopicId, MessageId, retCode.ToString());
    }
  }

  internal class MsPingReq : MsMessage {
    public MsPingReq(byte[] buf, int start, int end)
      : base(buf, start, end) {
      int ptr=buf[start+0]==1?start+4:start+2;
      if(1+_length+start-ptr>0) {
        ClientId=enc.GetString(buf, ptr, _length+start-ptr);
      } else {
        ClientId=string.Empty;
      }
    }

    public string ClientId;

    public override byte[] GetBytes() {
      throw new NotSupportedException("MsPingReq.GetBytes() not supported");
    }
  }

  internal class MsDisconnect : MsMessage {
    public MsDisconnect()
      : base(MsMessageType.DISCONNECT) {
    }
    public MsDisconnect(byte[] buf, int start, int end)
      : base(buf, start, end) {
      if(_length==4) {
        Duration=(ushort)((buf[start+2]<<8) | buf[start+3]);
      }
    }
    public readonly ushort Duration;
  }

  internal class MsForward : MsMessage {
    public readonly byte[] addr;
    public readonly MsMessage msg;

    public MsForward(byte[] addr, MsMessage msg)
      : base(MsMessageType.EncapsulatedMessage) {
      this.addr=addr;
      this.msg=msg;
    }

    public MsForward(byte[] buf, int start, int end)
      : base(buf, start, end) {
      addr=new byte[_length-3];
      Buffer.BlockCopy(buf, start+3, addr, 0, _length-3);
      msg=MsMessage.Parse(buf, start+_length, end);
    }
    public override byte[] GetBytes() {
      base._length=(ushort)(3+addr.Length);
      byte[] fMsg=msg.GetBytes();
      byte[] rez=new byte[_length+fMsg.Length];
      int ptr=0;
      if(_length>255) {
        rez[ptr++]=1;
        rez[ptr++]=(byte)(_length>>8);
        rez[ptr++]=(byte)(_length);
      } else {
        rez[ptr++]=(byte)(_length);
      }
      rez[ptr++]=(byte)MsgTyp;
      rez[ptr++]=0;
      Buffer.BlockCopy(addr, 0, rez, ptr, addr.Length);
      ptr+=addr.Length;
      Buffer.BlockCopy(fMsg, 0, rez, ptr, fMsg.Length);
      return rez;
    }
    public override string ToString() {
      return string.Format("FWD[{0}] {1}", addr==null?"NA":BitConverter.ToString(addr), msg);
    }
  }
  internal class MsDhcpReq : MsMessage {
    public MsDhcpReq(byte[] buf, int start, int end)
      : base(buf, start, end) {
      radius=buf[start+2];
      xId=(ushort)((buf[start+3]<<8) | buf[start+4]);
      hLen=buf.Skip(start+5).Take(end-start-5).ToArray();
    }
    public readonly byte radius;
    public readonly ushort xId;
    public readonly byte[] hLen;
    public override string ToString() {
      return string.Format("MsDhcpReq r={0}, xId={1:X4}, hLen={2}", radius, xId, BitConverter.ToString(hLen));
    }
  }
  internal class MsDhcpAck : MsMessage {
    private byte _gwIdx;
    private ushort _xId;
    private byte[]  _resp;
    public MsDhcpAck(byte gwIdx, ushort xId, byte[] addr)
      : base(MsMessageType.DHCP_ACK) {
      _gwIdx=gwIdx;
      _xId=xId;
      _resp=addr;
      if(addr==null) {
        throw new ArgumentNullException("addr");
      }
    }
    public override byte[] GetBytes() {
      base._length=(ushort)(5+_resp.Length);
      byte[] buf=base.GetBytes();
      buf[2]=_gwIdx;
      buf[3]=(byte)(_xId>>8);
      buf[4]=(byte)(_xId);
      Buffer.BlockCopy(_resp, 0, buf, 5, _resp.Length);
      return buf;
    }
  }
}
