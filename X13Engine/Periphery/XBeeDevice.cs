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
      _verbose=Topic.root.Get<bool>("/system/Broker/XBee/verboseLog");
      _verbose.value=true;
      _ifs=new List<IXBeeIF>();
    }
    internal static void Open() {
      XISerial.Open();
    }
    #endregion static

    private IXBeeIF _gate;
    private Timer _toTimer;
    private List<byte[]> _initLst=new List<byte[]>();
    private ushort _pullUpMask;
    private ushort _evntMask;

    [Newtonsoft.Json.JsonProperty]
    private string backName { get; set; }
    [Newtonsoft.Json.JsonProperty]
    private ulong _sn { get; set; }
    [Newtonsoft.Json.JsonProperty]
    private ulong _addr { get; set; }
    private string via {
      get { return Owner!=null?Owner.Get<string>("_via").value:string.Empty; }
      set {
        if(Owner!=null) {
          var t=Owner.Get<string>("_via");
          t.saved=true;
          t.value=value;
          _toTimer=new Timer(InitCB, null, 100, 200);
        }
      }
    }

    private void InitCB(object o) {
      if(_gate==null) {
        return;
      }
      if(_initLst.Count>0) {
        _gate.SentATCommand(this, (XBeeATCommand)((_initLst[0][0]<<8) | _initLst[0][1]), _initLst[0].Skip(2).ToArray());
      }
    }
    internal void Disconnect() {
      throw new NotImplementedException();
    }

    internal void CmdResponse(XBeeATCommand cmd, XBeeATCommandStatus atStatus, byte[] buf) {
      if(atStatus!=XBeeATCommandStatus.OK) {
        Log.Error("{0} ATCommand: {1}, response={2}", Owner.path, cmd.ToString(), atStatus.ToString());
        return;
      }
      _initLst.RemoveAll(z => z[0] == (byte)((int)cmd>>8) && z[1]== (byte)cmd);
      //if(buf.Length>0) {
        // TODO: create variables
      //}
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
          foreach(Topic t in Owner.children.ToArray()) {
            InitCmd(t);
          }
          Owner.Subscribe("+", ChildChanged);
          backName=Owner.name;
        }
      }
    }
    private void InitCmd(Topic t) {
      switch(t.name) {
      case "Op0":
      case "Op1":
      case "Op2":
      case "Op3":
      case "Op4":
      case "Op5":
      case "Op6":
      case "Op7":
        if(t.valueType==typeof(bool)) {
          _initLst.Add(new byte[]{0x44,  (byte)t.name[2], (byte)((t as DVar<bool>).value?0x5:0x4)});
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
        if(t.valueType==typeof(bool)) {
          _initLst.Add(new byte[]{0x44,  (byte)t.name[2], 3});
          _evntMask=(ushort)(_evntMask | 1<< (((byte)t.name[2])-0x30));
          _initLst.Add(new byte[] {0x49, 0x43, (byte)(_evntMask>>8), (byte)_evntMask});
        } else {
          t.Remove();
        }
        break;
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
            _gate.SentATCommand(this, (XBeeATCommand)(0x4400 | (byte)sender.name[2]), new byte[] { 0 });
          } else {
            _gate.SentATCommand(this, (XBeeATCommand)(0x4400 | (byte)sender.name[2]), new byte[] { (byte)((sender as DVar<bool>).value?5:4) });
          }
        }
        break;
      case "PP0":
        if(sender.valueType==typeof(long)) {
          if(param.Art==TopicChanged.ChangeArt.Remove) {
            _gate.SentATCommand(this, XBeeATCommand.P0, new byte[] { 0 });
          } else {
            long v=(sender as DVar<long>).value;
            _gate.SentATCommand(this, XBeeATCommand.P0, new byte[] { 4 });
            _gate.SentATCommand(this, XBeeATCommand.M0, new byte[] { (byte)((v>>8)&0x0F), (byte)(v) });
          }
        }
        break;
      }
    }
    #endregion ITopicOwned Members

    private interface IXBeeIF {
      void SentATCommand(XBeeDevice dev, XBeeATCommand cmd, byte[] param=null);
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
      RouteNotFound=0x25
    }
    private enum DiscoveryStatus : byte {
      NoDiscoveryOverhead=0x00,
      AddressDiscovery=0x01,
      RouteDiscovery=0x02,
      AddressAndRouteDiscovery=0x03
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
      TimeBeforeSleep=0x5354
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
