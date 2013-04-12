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
using X13.PLC;
using System.Threading;

namespace X13.Periphery {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public partial class XBeeDevice : ITopicOwned {
    #region static
    private static DVar<bool> _verbose;
    private static List<IXBeeIF> _ifs;

    static XBeeDevice() {
      _verbose=Topic.root.Get<bool>("/etc/log/XBee/verboseLog");
      _verbose.saved=true;
      _ifs=new List<IXBeeIF>();
    }
    internal static void Open() {
      XISerial.Open();
    }
    #endregion static

    private IXBeeIF _gate;
    private Timer _toTimer;
    private List<byte[]> _sendQueue=new List<byte[]>();
    private ushort _pullUpMask=0x1FFF;
    private ushort _evntMask;
    private ushort _usedMask;
    private int _tryCnt;
    private AutoResetEvent _sendPush=new AutoResetEvent(false);
    private RegisteredWaitHandle _sendPushWH;
    private UInt32 _devVer;
    private int _serialSpeed;

    [Newtonsoft.Json.JsonProperty]
    private string backName { get; set; }
    [Newtonsoft.Json.JsonProperty]
    private ulong _sn { get; set; }
    [Newtonsoft.Json.JsonProperty]
    private ulong _addr { get; set; }
    private string via {
      get { return Owner!=null?Owner.Get<string>(".cfg/_via").value:string.Empty; }
      set {
        if(Owner!=null) {
          var t=Owner.Get<string>(".cfg/_via");
          t.saved=true;
          t.value=value;
          Connect();
        }
      }
    }

    private void SendProc(object o, bool to) {
      if(_gate==null) {
        return;
      }
      byte[] cmd=null;
      if(!to) {
        _tryCnt=4;
      }
      lock(_sendQueue) {
        if(_sendQueue.Count>0) {
          cmd=_sendQueue[0];
        }
      }
      if(cmd!=null) {
        if(cmd[0]!=0x01 && cmd[1]!=0x00) {
          _gate.SentATCommand(this, (XBeeATCommand)((cmd[0]<<8) | cmd[1]), cmd.Skip(2).ToArray());
        } else {
          _gate.SendToSerial(this, cmd.Skip(2).ToArray());
        }
        if(_tryCnt--<1) {
          Disconnect();
        }
      } else {
        _tryCnt=4;
      }
    }
    private void SendAT(XBeeATCommand cmd, byte[] buf=null) {
      byte[] sb;
      if(buf==null || buf.Length==0) {
        sb=new byte[2];
      } else {
        sb=new byte[2+buf.Length];
        buf.CopyTo(sb, 2);
      }
      sb[0]=(byte)((ushort)cmd>>8);
      sb[1]=(byte)cmd;
      SendAT(sb);
    }
    private void SendAT(byte[] cmd) {
      bool send=false;
      lock(_sendQueue) {
        send=!_sendQueue.Any();
        _sendQueue.RemoveAll(z => z[0]==cmd[0] && z[1]==cmd[1]);
        _sendQueue.Add(cmd);
      }
      if(send && _gate!=null) {
        _sendPush.Set();
      }
    }
    internal void Connect() {
      if(_gate==null || Owner==null) {
        return;
      }
      int i;
      _sendPushWH=ThreadPool.RegisterWaitForSingleObject(_sendPush, new WaitOrTimerCallback(SendProc), null, 3000, false);
      _tryCnt=4;
      _pullUpMask=0x1FFF;
      _evntMask=0;
      _usedMask=0;
      Log.Info("{0} Connected", Owner.name);
      if(_gate.gwTopic!=null && _gate.gwTopic.value!=null && _gate.gwTopic!=Owner) {
        byte[] sb=BitConverter.GetBytes(_gate.gwTopic.value._sn);
        SendAT(XBeeATCommand.DestinationAddressHigh, sb.Skip(4).Reverse().ToArray());
        SendAT(XBeeATCommand.DestinationAddressLow, sb.Take(4).Reverse().ToArray());
      }
      SendAT(XBeeATCommand.HardwareVersion);
      SendAT(XBeeATCommand.FirmwareVersion);
      foreach(Topic t in Owner.children.ToArray()) {
        InitCmd(t);
      }
      for(i=0; i<8; i++) {
        if((_usedMask & (1<<i))==0) {
          SendAT(new byte[] { 0x44, (byte)(0x30+i) });      //D[0-7]
        }
      }
      for(i=10; i<13; i++) {
        if((_usedMask & (1<<i))==0) {
          SendAT(new byte[] { 0x50, (byte)(0x30+i-10) });   // P[0-2]
        }
      }
      SendAT(XBeeATCommand.IR_SampleRate, new byte[] { 0x40, 0x00 });    // IR=0x4000 
      Owner.Get<bool>("present").value=true;
    }
    internal void Disconnect() {
      if(Owner!=null) {
        Owner.Get<bool>("present").value=false;
        Log.Warning("{0} Lost", Owner.name);
      }
      if(_sendPushWH!=null) {
        _sendPushWH.Unregister(_sendPush);
        _sendPushWH=null;
      }
      _sendQueue.Clear();
      if(_gate!=null) {
        _gate.SentATCommand(this, XBeeATCommand.SoftwareReset);
      }
      _gate=null;
    }

    private void InitCmd(Topic t) {
      int idx;
      switch(t.name) {
      case "Op0":
      case "Op1":
      case "Op2":
      case "Op3":
      case "Op4":
      case "Op5":
      case "Op6":
      case "Op7":
        idx=(((byte)t.name[2])-0x30);
        if(t.valueType==typeof(bool) && (_usedMask & (1<<idx))==0) {
          SendAT(new byte[] { 0x44, (byte)t.name[2], (byte)((t as DVar<bool>).value?0x5:0x4) }); //Dx
          _usedMask=(ushort)(_usedMask | 1<< idx);
        } else {
          t.Remove();
        }
        break;
      case "Op10":
      case "Op11":
      case "Op12":
        idx=(((byte)t.name[3])-0x30);
        if(t.valueType==typeof(bool) && (_usedMask & (1<<(10+idx)))==0) {
          SendAT(new byte[] { 0x50, (byte)t.name[3], (byte)((t as DVar<bool>).value?0x5:0x4) }); //P[0..2]
          _usedMask=(ushort)(_usedMask | 1<< (10+idx));
        } else {
          t.Remove();
        }
        break;
      //case "Pp0":
      //case "Pp1":
      //case "Pn0":
      //case "Pn1":
      //  idx=(((byte)t.name[2])-0x30);
      //  if(t.valueType==typeof(long) && (_usedMask & (1<<(idx+10)))==0) {
      //    _usedMask=(ushort)(_usedMask | 1<< (idx+10));
      //    SendAT(new byte[] { 0x50, (byte)t.name[2], (byte)(t.name[1]=='p'?0x4:0x05) }); //P[0-1]
      //    SendAT(new byte[] { 0x4D, (byte)t.name[2], (byte)(((t as DVar<long>).value>>8) & 0x03), (byte)((t as DVar<long>).value) }); //M[0-1]
      //  } else {
      //    t.Remove();
      //  }
      //  break;
      case "St1":   // TxD 1200
      case "Sr1":   // RxD 1200 
      case "St2":   // TxD 2400
      case "Sr2":   // RxD 2400 
      case "St3":   // TxD 4800
      case "Sr3":   // RxD 4800 
      case "St4":   // TxD 9600
      case "Sr4":   // RxD 9600
      case "St5":   // TxD 19200
      case "Sr5":   // RxD 19200
      case "St6":   // TxD 38400
      case "Sr6":   // RxD 38400
      case "St7":   // TxD 57600
      case "Sr7":   // RxD 57600
      case "St8":   // TxD 115200
      case "Sr8":   // RxD 115200
        idx=(((byte)t.name[2])-0x30);
        if(t.valueType==typeof(string) && (_serialSpeed==0 || _serialSpeed==idx)) {
          if(_serialSpeed==0) {
            SendAT(XBeeATCommand.InterfaceDataRate, new byte[] { (byte)(idx-1) });  // 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200
            _serialSpeed=idx;
          }
        } else {
          t.Remove();
        }

        break;
      case "Ip0":
      case "Ip1":
      case "Ip2":
      case "Ip3":
      case "Ip4":
      case "Ip5":
      case "Ip6":
      case "Ip7":
      case "Ip8":
        idx=(((byte)t.name[2])-0x30);
        if(t.valueType==typeof(bool) && (_usedMask & (1<<idx))==0) {
          SendAT(new byte[] { 0x44, (byte)t.name[2], 3 });                                //Dx
          _evntMask=(ushort)(_evntMask | 1<< (((byte)t.name[2])-0x30));
          SendAT(XBeeATCommand.IODigitalChangeDetection, new byte[] { (byte)(_evntMask>>8), (byte)_evntMask });
          ushort tmp=_pullUpMask;
          _pullUpMask=(ushort)(_pullUpMask  |(1<<idx));
          if(_pullUpMask!=tmp) {
            SendAT(XBeeATCommand.PullUpResistor, new byte[] { (byte)(_pullUpMask>>8), (byte)(_pullUpMask) });
          }
          _usedMask=(ushort)(_usedMask | 1<< idx);
          SendAT(XBeeATCommand.ForceSample);
        } else {
          t.Remove();
        }
        break;
      case "Ip10":
      case "Ip11":
      case "Ip12":
        idx=(((byte)t.name[3])-0x30+10);
        if(t.valueType==typeof(bool) && (_usedMask & (1<<idx))==0) {
          SendAT(new byte[] { 0x50, (byte)t.name[3], 3 });                                //P[0..2]
          _evntMask=(ushort)(_evntMask | 1<<idx);
          SendAT(XBeeATCommand.IODigitalChangeDetection, new byte[] { (byte)(_evntMask>>8), (byte)_evntMask });
          ushort tmp=_pullUpMask;
          _pullUpMask=(ushort)(_pullUpMask  |(1<<idx));
          if(_pullUpMask!=tmp) {
            SendAT(XBeeATCommand.PullUpResistor, new byte[] { (byte)(_pullUpMask>>8), (byte)(_pullUpMask) });
          }
          _usedMask=(ushort)(_usedMask | 1<< idx);
          SendAT(XBeeATCommand.ForceSample);
        } else {
          t.Remove();
        }

        break;
      case "Ai0":
      case "Ai1":
      case "Ai2":
      case "Ai3":
      case "Ai4":
        idx=(((byte)t.name[2])-0x30);
        if(t.valueType==typeof(double) && (_usedMask & (1<<idx))==0) {
          _usedMask=(ushort)(_usedMask | 1<< idx);
          SendAT(new byte[] { 0x44, (byte)t.name[2], 2 });                            //Dx
          ushort tmp=_pullUpMask;
          _pullUpMask=(ushort)(_pullUpMask & ~(1<<idx));
          if(_pullUpMask!=tmp) {
            SendAT(XBeeATCommand.PullUpResistor, new byte[] { (byte)(_pullUpMask>>8), (byte)(_pullUpMask) });
          }
          SendAT(XBeeATCommand.ForceSample);
        } else {
          t.Remove();
        }
        break;
      case "Vcc":
        if(t.valueType==typeof(double)) {
          SendAT(XBeeATCommand.VccThresold, new byte[] { 0x0C, 0x00 });
          SendAT(XBeeATCommand.ForceSample);
        } else {
          t.Remove();
        }
        break;
      }
    }
    internal void CmdResponse(XBeeATCommand cmd, XBeeATCommandStatus atStatus, byte[] buf) {
      int idx;
      bool actuell=false;
      lock(_sendQueue) {
        if(_sendQueue.Any() && _sendQueue[0][0] == (byte)((int)cmd>>8) && _sendQueue[0][1]== (byte)cmd) {
          if(atStatus!=XBeeATCommandStatus.ERROR) {
            _sendQueue.RemoveAt(0);
          }
          actuell=true;
        }
      }
      if(atStatus!=XBeeATCommandStatus.OK) {
        Log.Error("{0} ATCommand: {1}, response={2}", Owner.name, cmd.ToString(), atStatus.ToString());
        return;
      }
      if(actuell && Owner!=null && buf!=null && buf.Length>0) {
        switch(cmd) {
        case XBeeATCommand.D0:
        case XBeeATCommand.D1:
        case XBeeATCommand.D2:
        case XBeeATCommand.D3:
        case XBeeATCommand.D4:
        case XBeeATCommand.D5:
        case XBeeATCommand.D6:
        case XBeeATCommand.D7:
          idx=(int)cmd-(int)XBeeATCommand.D0;
          if(buf[0]==4 || buf[0]==5) {
            Owner.Get<bool>(string.Format("Op{0}", idx)).value=buf[0]==5;
          } else if(buf[0]==3) {
            Owner.Get<bool>(string.Format("Ip{0}", idx));
          } else if(buf[0]==2) {
            Owner.Get<double>(string.Format("Ai{0}", idx));
          }
          break;
        case XBeeATCommand.P0:
        case XBeeATCommand.P1:
        case XBeeATCommand.P2:
          idx=(int)cmd-(int)XBeeATCommand.P0;
          if(buf[0]==4 || buf[0]==5) {
            Owner.Get<bool>(string.Format("Op1{0}", idx)).value=buf[0]==5;
          } else if(buf[0]==3) {
            Owner.Get<bool>(string.Format("Ip1{0}", idx));
          }
          break;
        case XBeeATCommand.ForceSample:
          ReceiveDataSample(buf);
          break;
        case XBeeATCommand.HardwareVersion:   //1944, 1945
          _devVer=(uint)(buf[0]<<24 | buf[1]<<16);
          break;
        case XBeeATCommand.FirmwareVersion:   //1147, 21A7, 22A7, 28A7
          _devVer|=(uint)(buf[0]<<8 | buf[1]);
          Owner.Get<string>("_declarer").value=string.Format("XB{0:X2}{1:X2}.{2:X2}{3:X2}", (_devVer>>24)&0xFF, (_devVer>>8)&0xFF, (_devVer>>16)&0xFF, _devVer&0xFF);
          break;
        }
      }
      if(actuell) {
        _sendPush.Set();
      }
    }
    internal void ReceiveDataSample(byte[] buf) {
      int i=1, j;
      UInt16 data;
      UInt16 dMask=(UInt16)((buf[i++]<<8) | buf[i++]);
      Topic ct;
      byte aMask=(byte)buf[i++];
      if(dMask!=0) {
        data=(UInt16)((buf[i++]<<8) | buf[i++]);   // digital Inputs
        for(j=0; j<13; j++) {
          if((dMask & 1<<j)!=0) {
            if(Owner.Exist(string.Format("Ip{0}", j), out ct)) {
              (ct as DVar<bool>).value=(data & 1<<j)!=0;
              if(_verbose) {
                Log.Debug("{0}={1}", ct.path, (data & 1<<j)!=0);
              }
            } else {
              if(Owner!=null && _verbose) {
                Log.Debug("{0}.Ip{1}={2}", Owner.path, j, (data & 1<<j)!=0);
              }
            }
          }
        }
      }
      for(j=0; j<4; j++) {
        if((aMask & 1<<j)!=0) {
          data=(UInt16)((buf[i++]<<8) | buf[i++]);   // analog Input
          double ad=data*1.2/1024.0;
          if(Owner.Exist(string.Format("Ai{0}", j), out ct)) {
            (ct as DVar<double>).value=ad;
            if(_verbose) {
              Log.Debug("{0}={1:f3}", ct.path, ad);
            }
          } else {
            if(Owner!=null && _verbose) {
              Log.Debug("{0}.Ai{1}={2:3}", Owner.path, j, ad);
            }
            SendAT(new byte[] { 0x44, (byte)(0x30+j), 0 });
          }
        }
      }
      if((aMask & 0x80)!=0) {
        data=(UInt16)((buf[i++]<<8) | buf[i++]);   // power U
        if(Owner.Exist("Vcc", out ct)) {
          (ct as DVar<double>).value=data*1.2/1024.0;
          if(_verbose) {
            Log.Debug("{0}={1:f3} V", ct.path, data*1.2/1024.0);
          }
        } else {
          if(Owner!=null && _verbose) {
            Log.Debug("{0}.Vcc={1:f3} V", Owner.path, data*1.2/1024.0);
          }
          SendAT(XBeeATCommand.VccThresold, new byte[] { 0, 0 });
        }
      }
    }
    internal void ReceivePacket(byte[] buf) {
      Topic Sr;
      if(buf!=null && Owner!=null && Owner.Exist(string.Format("Sr{0}", _serialSpeed), out Sr) && Sr.valueType==typeof(string)) {
        (Sr as DVar<string>).value=Encoding.Default.GetString(buf);
        if(_verbose) {
          Log.Debug("{0}={1}", Sr.path, BitConverter.ToString(buf));
        }
      }
    }
    private void ChildChanged(Topic sender, TopicChanged param) {
      if(_gate==null) {
        return;
      }
      if(param.Art==TopicChanged.ChangeArt.Add) {
        InitCmd(sender);
        return;
      }
      int idx;
      switch(sender.name) {
      case "Op0":
      case "Op1":
      case "Op2":
      case "Op3":
      case "Op4":
      case "Op5":
      case "Op6":
      case "Op7":
        if(sender.valueType==typeof(bool)) {
          if(param.Art==TopicChanged.ChangeArt.Remove) {
            SendAT(new byte[] { 0x44, (byte)sender.name[2], 0 });
            _usedMask=(ushort)(_usedMask & ~(1<<(((byte)sender.name[2])-0x30)));
          } else {
            SendAT(new byte[] { 0x44, (byte)sender.name[2], (byte)((sender as DVar<bool>).value?5:4) });
          }
        }
        break;
      case "Op10":
      case "Op11":
      case "Op12":
        idx=(((byte)sender.name[3])-0x30);
        if(sender.valueType==typeof(bool)) {
          if(param.Art==TopicChanged.ChangeArt.Remove) {
            SendAT(new byte[] { 0x50, (byte)sender.name[3], 0 });
            _usedMask=(ushort)(_usedMask & ~(1<<(idx+10)));
          } else {
            SendAT(new byte[] { 0x50, (byte)sender.name[3], (byte)((sender as DVar<bool>).value?5:4) });
          }
        }

        break;
      case "St1":
      case "St2":
      case "St3":
      case "St4":
      case "St5":
      case "St6":
      case "St7":
      case "St8":
        idx=(((byte)sender.name[2])-0x30);
        if(sender.valueType==typeof(string)) {
          if(param.Art!=TopicChanged.ChangeArt.Remove) {
            string str=(sender as DVar<string>).value;
            if(str!=null && str.Length>0) {
              SendAT(XBeeATCommand.TransmitRequest, Encoding.Default.GetBytes(str));
            }
          } else if(!Owner.Exist(string.Format("Sr{0}", idx))) {
            _serialSpeed=0;
          }
        }
        break;
      case "Sr1":
      case "Sr2":
      case "Sr3":
      case "Sr4":
      case "Sr5":
      case "Sr6":
      case "Sr7":
      case "Sr8":
        idx=(((byte)sender.name[2])-0x30);
        if(param.Art==TopicChanged.ChangeArt.Remove && !Owner.Exist(string.Format("St{0}", idx))) {
          _serialSpeed=0;
        }
        break;
      case "Ip0":
      case "Ip1":
      case "Ip2":
      case "Ip3":
      case "Ip4":
      case "Ip5":
      case "Ip6":
      case "Ip7":
      case "Ip8":
      case "Ai0":
      case "Ai1":
      case "Ai2":
      case "Ai3":
      case "Ai4":
        if(param.Art==TopicChanged.ChangeArt.Remove) {
          idx=(((byte)sender.name[2])-0x30);
          SendAT(new byte[] { 0x44, (byte)sender.name[2], 0 });
          ushort oldEM=_evntMask;
          _evntMask=(ushort)(_evntMask &~(1<< idx));
          if(oldEM!=_evntMask) {
            SendAT(new byte[] { 0x49, 0x43, (byte)(_evntMask>>8), (byte)_evntMask });
          }
          _usedMask=(ushort)(_usedMask& ~(1<< idx));
        }
        break;
      case "Ip10":
      case "Ip11":
      case "Ip12":
        if(param.Art==TopicChanged.ChangeArt.Remove) {
          idx=(((byte)sender.name[3])-0x30+10);
          SendAT(new byte[] { 0x50, (byte)sender.name[3], 0 });
          ushort oldEM=_evntMask;
          _evntMask=(ushort)(_evntMask &~(1<< idx));
          if(oldEM!=_evntMask) {
            SendAT(new byte[] { 0x49, 0x43, (byte)(_evntMask>>8), (byte)_evntMask });
          }
          _usedMask=(ushort)(_usedMask& ~(1<< idx));
        }
        break;
      //case "Pp0":
      //case "Pp1":
      //case "Pn0":
      //case "Pn1":
      //  idx=(((byte)sender.name[2])-0x30);
      //  if(param.Art==TopicChanged.ChangeArt.Remove) {
      //    _usedMask=(ushort)(_usedMask & ~(1<<(idx+10)));
      //    SendAT(new byte[] { 0x50, (byte)sender.name[2], 0 }); //P[0-1]
      //  } else {
      //    SendAT(new byte[] { 0x4D, (byte)sender.name[2], (byte)(((sender as DVar<long>).value>>8) & 0x03), (byte)((sender as DVar<long>).value) }); //M[0-1]
      //  }
      //  break;
      }
    }

    public Topic Owner { get; private set; }

    #region ITopicOwned Members
    public void SetOwner(Topic owner) {
      if(Owner!=owner) {
        if(Owner!=null) {
          Owner.Unsubscribe("+", ChildChanged);
          //TODO: disconnect
        }
        Owner=owner;
        if(Topic.brokerMode && Owner!=null) {
          Owner.saved=true;

          Topic oldT;
          if(!string.IsNullOrEmpty(backName) && backName!=Owner.name && Owner.parent.Exist(backName, out oldT) && oldT.valueType==typeof(XBeeDevice)) {   // Device renamed
            XBeeDevice old=(oldT as DVar<XBeeDevice>).value;
            if(old!=null) {
              _addr=old._addr;
              _sn=old._sn;
              //TODO: rename
            }
          }

          Owner.Subscribe("+", ChildChanged);
          backName=Owner.name;
        }
      }
    }
    #endregion ITopicOwned Members

    private interface IXBeeIF {
      void SentATCommand(XBeeDevice dev, XBeeATCommand cmd, byte[] param=null);
      void SendToSerial(XBeeDevice dev, byte[] buf);
      DVar<XBeeDevice> gwTopic { get; }
    }
    private enum XBeeCmdID : byte {
      ATCommand=0x08,
      ATCommandResponse=0x88,
      ATCommandQ=0x09,
      ModemStatus=0x8A,
      TransmitStatus=0x8B,
      AdvancedModemStatus=0x8C,
      TransmitRequest=0x10,
      ReceivePacket=0x90,
      ExplicitAddresingCommand=0x11,
      ExplicitRxIndicator=0x91,
      DataSampleRxIndicator=0x92,
      SensorReadIndicator=0x94,
      ModulIdentificationIndicator=0x95,
      RemoteCommandRequest=0x17,
      RemoteCommandResponse=0x97
    }
    private enum DeliveryStatus : byte {
      Success=0x00,
      CCA_Failure=0x02,
      InvalidDstEndpoint=0x15,
      NetworkACKFailure=0x21,
      NotJoinedToNetwork=0x22,
      SelfAddressed=0x23,
      AddressNotFound=0x24,
      RouteNotFound=0x25,
      PayloadTooLarge=0x74,
    }
    private enum DiscoveryStatus : byte {
      Ok=0x00,
      AddressDiscovery=0x01,
      RouteDiscovery=0x02,
      AddressAndRouteDiscovery=0x03,
    }
    public enum XBeeATCommand : ushort {
      Write=0x5752,
      WriteBindingTable=0x5742,
      RestoreDefault=0x5244,
      SoftwareReset=0x4652,
      NetworkReset=0x4E52,
      DestinationAddressHigh=0x4448,
      DestinationAddressLow=0x444C,
      NetworkAddress=0x4D59,
      ParentNetworkAddress=0x4D50,
      SerialNumberHigh=0x5348,
      SerialNumberLow=0x534C,
      NodeIdentifier=0x4E49,
      DeviceTypeIdentifier=0x4444,
      ZigBeeApplicationLayerAddressing=0x5A41,
      SourceEndpoint=0x5345,
      DestinationEndpoint=0x4445,
      ClusterIdentifier=0x4349,
      BindingTableIndex=0x4249,
      OperatingChannel=0x4348,
      PAN_ID=0x4944,
      BroadcastHops=0x4248,
      NodeDiscoveryTimeout=0x4E54,
      NodeDiscovery=0x4E44,
      DestinationNode=0x444E,
      JoinNotification=0x4A4E,
      ScanChannels=0x5343,
      ScanDuration=0x5344,
      NodeJoinTime=0x4E4A,
      AggregateRoutingNotification=0x4152,
      AssociationIndication=0x4149,
      PowerLevel=0x504C,
      PowerMode=0x504D,
      APIEnable=0x4150,
      APIOptions=0x414F,
      InterfaceDataRate=0x4244,
      PacketizationTimeout=0x524F,
      RSSI_PWM_Timer=0x5250,
      ForceSample=0x4953,
      D0=0x4430,
      D1=0x4431,
      D2=0x4432,
      D3=0x4433,
      D4=0x4434,
      D5=0x4435,
      D6=0x4436,
      D7=0x4437,
      P0=0x5030,
      P1=0x5031,
      P2=0x5032,
      M0=0x4D30,
      M1=0x4D31,
      M2=0x4D32,
      IODigitalChangeDetection=0x4943,
      PullUpResistor=0x5052,
      FirmwareVersion=0x5652,
      HardwareVersion=0x4856,
      SupplyVoltage=0x2556,
      CommandModeTimeout=0x4354,
      ExitCommandMode=0x434E,
      GuardTimes=0x4754,
      CommandSequenceCharacter=0x4343,
      SleepMode=0x534D,
      NumberOfSleepPeriods=0x534E,
      SleepPeriod=0x5350,
      TimeBeforeSleep=0x5354,
      ForceDisassociation=0x4441,
      IR_SampleRate=0x4952,
      VccThresold=0x562B,
      TransmitRequest=0x0100,       // Sent to serial port
      //  A   B   C   D   E   F   G   H   I   J   K   L   M   N   O   P   Q   R   S   T   U   V   W   X   Y   Z
      //  41  42  43  44  45  46  47  48  49  4A  4B  4C  4D  4E  4F  50  51  52  53  54  55  56  57  58  59  5A 
    }
    public enum XBeeATCommandStatus : byte {
      OK=0,
      ERROR=1,
      InvalidCommand=2,
      InvalidParameter=3
    }

  }
}
