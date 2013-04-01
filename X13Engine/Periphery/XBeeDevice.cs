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
    private static Dictionary<string, uint> _initCmd=new Dictionary<string, uint>() {
      {"DO0", 0x40000 | (uint)XBeeATCommand.D0},
      {"DO1", 0x40000 | (uint)XBeeATCommand.D1},
      {"DO2", 0x40000 | (uint)XBeeATCommand.D2},
      {"DO3", 0x40000 | (uint)XBeeATCommand.D3},
      {"DO4", 0x40000 | (uint)XBeeATCommand.D4},
      {"DO5", 0x40000 | (uint)XBeeATCommand.D5},
      {"DO6", 0x40000 | (uint)XBeeATCommand.D6},
      {"DO7", 0x40000 | (uint)XBeeATCommand.D7},
    };

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
    private List<uint> _initLst=new List<uint>();

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
        _gate.SentATCommand(this, (XBeeATCommand)(_initLst[0] & 0xFFFF), new byte[] { (byte)(_initLst[0]>>16) });
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
      int idx=_initLst.FindIndex(z => (z&0xFFFF) == (int)cmd);
      if(idx<0) {

      } else {
        if(buf.Length==0 || (buf.Length==1 && buf[0]==(byte)(_initLst[idx]>>16))) {
          _initLst.RemoveAt(idx);
        }
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
          foreach(Topic t in Owner.children.ToArray()) {
            uint val;
            if(_initCmd.TryGetValue(t.name, out val)) {
              if(_initLst.Any(z => (z&0xFFFF)==(val&0xFFFF))) {
                t.Remove();       // conflict
              } else {

                _initLst.Add(val);
              }
            } else {
              // unknown variable
            }
          }
          backName=Owner.name;
        }
      }
    }

    void ChildChanged(Topic sender, TopicChanged param) {
      if(_gate==null) {
        return;
      }
      if(param.Art==TopicChanged.ChangeArt.Add) {
        uint val;
        if(_initCmd.TryGetValue(sender.name, out val)) {
          _initLst.Add(val);
        }
        return;
      }
      switch(sender.name) {
      case "DO0":
      case "DO1":
      case "DO2":
      case "DO3":
      case "DO4":
      case "DO5":
      case "DO6":
      case "DO7":
        if(sender.valueType==typeof(bool)) {
          if(param.Art==TopicChanged.ChangeArt.Remove) {
            _gate.SentATCommand(this, (XBeeATCommand)(_initCmd[sender.name] &0xFFFF), new byte[] { 0 });
          } else {
            _gate.SentATCommand(this, (XBeeATCommand)(_initCmd[sender.name] &0xFFFF), new byte[] { (byte)((sender as DVar<bool>).value?5:4) });
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
