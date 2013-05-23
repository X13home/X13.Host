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
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Threading;

namespace X13.MQTT {
  internal class MqStreamer {
    private NetworkStream _stream;

    private int _rcvState=0;
    private byte _rcvHeader=0;
    private uint _rcvLengt=0;
    private int _rcvLengthPos=0;
    private MemoryStream _rcvMemoryStream=new MemoryStream();
    private byte[] _rcvBuf=new byte[1];
    private Action<MqMessage> _rcvCallback;
    private Timer _rcvTimer;
    private bool _sndPaused;

    private Queue<MqMessage> _sendQ=new Queue<MqMessage>(32);
    private List<wMessage> _waitAck=new List<wMessage>();

    private int _sendProcessed=0;
    private AsyncCallback _sendCB;
    private Action _idleCB;
    private Timer _sendTimer;

    private int _messageIdGen;
    private bool _connected;
    public readonly TcpClient Socket;

    public MqStreamer(TcpClient _tcp, Action<MqMessage> recv, Action idle) {
      this._connected=true;
      this._messageIdGen=0;
      this.Socket = _tcp;
      this._stream=_tcp.GetStream();
      this._stream.Flush();
      this._sendCB=new AsyncCallback(SendProcess);
      this._rcvCallback=recv;
      this._idleCB=idle;
      this._stream.BeginRead(_rcvBuf, 0, 1, RcvProcess, _stream);
      this._rcvTimer=new Timer(RcvTimeout);
      this._sendTimer=new Timer(SendWaitAck);
    }
    public bool isOpen {
      get {
        return Socket==null?false:Socket.Connected;
      }
    }

    /// <summary> Send Message to broker</summary>
    /// <param name="msg">MQTT message</param>
    /// <remarks>Upon receipt of subscription switches to pause.
    /// Resumes at a pause in the receive of data over 300 ms.
    /// When paused publish are not sent</remarks>
    public void Send(MqMessage msg) {
      if(msg.QualityOfService!=QoS.AtMostOnce && msg.MessageID==0) {
        int tmp=Interlocked.Increment(ref _messageIdGen);
        if((tmp & 0x7FFF)==0) {
          tmp=Interlocked.Increment(ref _messageIdGen);
          _messageIdGen=(ushort)tmp;
        }
        if(Topic.brokerMode) {
          tmp^=0x8000;
        }
        msg.MessageID=(ushort)tmp;
      }
      //if(msg.MsgType==MessageType.SUBSCRIBE) {
      //  _sndPaused=true;
      //  _rcvTimer.Change(300, Timeout.Infinite);
      //}
      if((!_sndPaused || msg.MsgType!=MessageType.PUBLISH) && Interlocked.Exchange(ref _sendProcessed, 1)==0) {
        SendIntern(msg);
      } else {
        lock(_sendQ) {
          _sendQ.Enqueue(msg);
        }
      }
    }

    private void SendProcess(IAsyncResult ar) {
      try {
        _stream.EndWrite(ar);
      }
      catch(IOException) {
        if(this._connected) {
          this.Close();
        }
        return;
      }
      catch(ObjectDisposedException) {
        return;
      }
      MqMessage msg;
      lock(_sendQ) {
        msg=_sendQ.Count>0?_sendQ.Dequeue():null;
        if(_sendQ.Count==32) {
          _sendQ.TrimExcess();
        }
      }
      if(msg!=null) {
        SendIntern(msg);
      } else {
        _sendProcessed=0;
        if(_waitAck.Count>0) {
          _sendTimer.Change(900, Timeout.Infinite);
        } else {
          _sendTimer.Change(30, Timeout.Infinite);
        }
      }
    }
    private void SendIntern(MqMessage msg) {
      if(msg==null || _waitAck==null) {
        return;
      }
      MemoryStream ms=new MemoryStream();
      msg.Serialise(ms);
      try {
        _stream.BeginWrite(ms.GetBuffer(), 0, (int)ms.Length, _sendCB, null);
      }
      catch(IOException) {
        if(this._connected) {
          this.Close();
        }
        return;
      }
      catch(ObjectDisposedException) {
        return;
      }
      if(msg.QualityOfService!=QoS.AtMostOnce && !msg.Duplicate) {
        _waitAck.Add(new wMessage(msg));
      }
      msg.Duplicate=true;
      if(msg.MsgType==MessageType.DISCONNECT) {
        Thread.Sleep(30);     // hack, wait fo buffers flush
        Close(true);
      }
    }
    private void SendWaitAck(object o) {
      if(_waitAck.Count>0) {
        _waitAck[0].cnt++;
        this.Send(_waitAck[0].msg);
        if(_waitAck.Count>0 && _waitAck[0].cnt>2) {
          _waitAck.RemoveAt(0);
        }
      } else if(_idleCB!=null) {
        _idleCB();
      }
    }

    private void RcvProcess(IAsyncResult ar) {
      bool first=true;
      int len;
      try {
        len=_stream.EndRead(ar);
      }
      catch(IOException) {
        Close(true);
        return;
      }
      catch(ObjectDisposedException) {
        return;
      }
      if(len>0) {
        try {
          do {
            if(first) {
              first=false;
            } else {
              _rcvBuf[0]=(byte)_stream.ReadByte();
            }
            switch(_rcvState) {
            case 0:           // header
              _rcvHeader=_rcvBuf[0];
              _rcvLengt=0;
              _rcvLengthPos=0;
              _rcvState++;
              break;
            case 1: {
                _rcvLengt+=(uint)((_rcvBuf[0] & 0x7F) << (7*_rcvLengthPos));
                _rcvLengthPos++;
                if((_rcvBuf[0]&0x80)==0) {
                  _rcvState++;
                  _rcvLengthPos=0;
                  if(_rcvLengt==0) {
                    goto case 2;
                  }
                }
              }
              break;
            case 2:
              if(_rcvMemoryStream.Position<_rcvLengt) {
                _rcvMemoryStream.WriteByte(_rcvBuf[0]);
              }
              if(_rcvMemoryStream.Position>=_rcvLengt) {
                _rcvMemoryStream.Seek(0, SeekOrigin.Begin);
                MqMessage msg=MqMessage.Parse(_rcvHeader, _rcvLengt, _rcvMemoryStream);
                if(msg==null) {
                  Log.Warning("unrecognized message from {0}={1:X2}[{2}]", ((IPEndPoint)Socket.Client.RemoteEndPoint), _rcvHeader, _rcvLengt);
                } else {
                  _rcvMemoryStream.Seek(0, SeekOrigin.Begin);
                  _rcvState=0;

                  if(msg.MessageID!=0) {
                    if(msg.Reason!=MessageType.NONE) {
                      _waitAck.RemoveAll(wm => wm.msg.MessageID==msg.MessageID && wm.msg.MsgType==msg.Reason);
                    } else {
                      int nid=msg.MessageID+1;
                      if(nid==0x10000) {
                        nid++;
                      }
                      if(!Topic.brokerMode) {
                        nid^=0x8000;
                      }
                      if(nid>(int)_messageIdGen || (nid>0xFF00 && _messageIdGen<0x0100)) {
                        _messageIdGen=(ushort)nid;      // synchronize messageId
                      }
                    }
                  }
                  if(_rcvCallback!=null) {
                    _rcvCallback(msg);
                  }
                }

                if(_waitAck.Count>0) {
                  _sendTimer.Change(900, Timeout.Infinite); // connection is busy
                }
              }
              break;
            default:
              _rcvState=0;
              break;
            }
          } while(_stream.DataAvailable);
          if(_rcvState!=0) {
            _rcvTimer.Change(100, Timeout.Infinite);
          } else if(_sndPaused) {
            _rcvTimer.Change(150, Timeout.Infinite);
          } else {
            _rcvTimer.Change(Timeout.Infinite, Timeout.Infinite);
          }
        }
        catch(ObjectDisposedException) {
          return;
        }
        catch(Exception ex) {
          Log.Warning(ex.ToString());
        }
      } else {
        if(_connected) {
          this.Close(true);
        }
        return;
      }
      try {
        _stream.BeginRead(_rcvBuf, 0, 1, RcvProcess, _stream);
      }
      catch(IOException ex) {
        if(_connected) {
          this.Close(true);
          Log.Warning("MqStreamer.ReceiveProcess {0}", ex.Message);
        }
        return;
      }
      catch(ObjectDisposedException ex) {
        Log.Warning("MqStreamer.ReceiveProcess {0}", ex.Message);
        return;
      }
    }
    private void RcvTimeout(object o) {
      if(_rcvState!=0) {
        _rcvMemoryStream.Seek(0, SeekOrigin.Begin);
        _rcvState=0;
      } else if(_sndPaused) {
        _sndPaused=false;
        if(Interlocked.Exchange(ref _sendProcessed, 1)==0) {
          MqMessage msg;
          lock(_sendQ) {
            msg=_sendQ.Count>0?_sendQ.Dequeue():null;
            if(_sendQ.Count==32) {
              _sendQ.TrimExcess();
            }
          }
          if(msg!=null) {
            SendIntern(msg);
          } else {
            _sendProcessed=0;
          }
        }
      }
    }
    public override string ToString() {
      if(isOpen) {
        return "Connected to "+Dns.GetHostEntry(((IPEndPoint)Socket.Client.RemoteEndPoint).Address).HostName;
      } else {
        return "Disconnected";
      }
    }

    public void Close() {
      Close(false);
    }

    private void Close(bool inf) {
      if(inf && _rcvCallback!=null) {
        _rcvCallback(new MqDisconnect());
      }
      if(_connected) {
        _connected=false;
        _stream.Close();
        Socket.Close();
      }
    }

    private class wMessage {
      public wMessage(MqMessage msg) {
        this.msg=msg;
        this.cnt=0;
      }
      public MqMessage msg;
      public byte cnt;
    }

  }
}
