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
using System.IO.Ports;
using System.IO;

namespace X13.Periphery {
  public partial class XBeeDevice : ITopicOwned {

    private class XISerial : IXBeeIF {
      #region static
      private static AutoResetEvent _startScan;
      private static bool _scanAllPorts=false;
      private static int _scanBusy;

      static XISerial() {
        _startScan=new AutoResetEvent(false);
        _scanBusy=0;
        ThreadPool.RegisterWaitForSingleObject(_startScan, ScanPorts, null, TimeSpan.FromMinutes(15), false);
      }
      public static void Open() {
        Log.Info("Search for XBee devices");
        _scanAllPorts=true;
        ScanPorts(null, false);
      }
      private static void ScanPorts(object o, bool b) {
        if(Interlocked.Exchange(ref _scanBusy, 1)!=0) {
          return;
        }
        XBFrame iFrame=new XBFrame() { buf=new byte[64] };
        SerialPort port=null;
        XISerial gw;
        byte[] xbIdentPacket1=new byte[] { (byte)XBeeCmdID.ModemStatus, 1 };
        byte[] xbIdentPacket2=new byte[] { (byte)XBeeCmdID.ATCommandResponse, 0x01, (byte)((ushort)XBeeATCommand.SoftwareReset>>8), (byte)((ushort)XBeeATCommand.SoftwareReset&0xFF), (byte)XBeeATCommandStatus.OK };

        List<string> pns=new List<string>();
        Topic dev=Topic.root.Get("/dev");

        lock(dev) {
          var ifs=dev.children.Where(z => z.valueType==typeof(XBeeDevice)).Cast<DVar<XBeeDevice>>().Where(z => z.value!=null).Select(z => z.value).ToArray();
          foreach(var devSer in ifs) {
            if(string.IsNullOrWhiteSpace(devSer.via)) {
              _scanAllPorts=true;
              continue;
            }
            string via=devSer.via;
            if(via!="offline" && via.StartsWith("com", StringComparison.InvariantCultureIgnoreCase) && !pns.Exists(z => string.Equals(z, via, StringComparison.InvariantCultureIgnoreCase))) {
              pns.Add(via);
            }
          }
        }
        if(_scanAllPorts) {
          _scanAllPorts=false;
          foreach(var pn in SerialPort.GetPortNames()) {
            if(!pns.Exists(z => string.Equals(z, pn))) {
              pns.Add(pn);
            }
          }
        }
        for(int i=0; i<pns.Count; i++) {
          try {
            port=new SerialPort(pns[i], 38400, Parity.None, 8, StopBits.One);
            port.ReadBufferSize=300;
            port.WriteBufferSize=500;
            port.Open();
            byte[] NDframe=new byte[] { (byte)XBeeCmdID.ATCommand, 1, (byte)((ushort)XBeeATCommand.SoftwareReset>>8), (byte)((ushort)XBeeATCommand.SoftwareReset&0xFF) };
            port.DiscardInBuffer();
            SendRaw(port, NDframe);
            Log.Debug("{0} s {1}", pns[i], BitConverter.ToString(NDframe, 0, NDframe.Length));
            gw=null;
            iFrame.Reset();
            for(int t=0; t<20; t++) {   // timeout=20*30=600 ms
              Thread.Sleep(30);
              if(GetFrame(port, ref iFrame)) {
                if(_verbose)
                  Log.Debug("{0} r {1}", pns[i], BitConverter.ToString(iFrame.buf, 0, iFrame.length));
                if((xbIdentPacket1.Length==iFrame.length && xbIdentPacket1.SequenceEqual(iFrame.buf.Take(xbIdentPacket1.Length))) 
                  || (xbIdentPacket2.Length==iFrame.length && xbIdentPacket2.SequenceEqual(iFrame.buf.Take(xbIdentPacket2.Length)))) {
                  gw=new XISerial(port);
                  _ifs.Add(gw);
                  break;
                }
              }
            }
            if(gw==null && port!=null) {
              port.Close();
            }
          }
          catch(Exception) {
            if(port!=null && port.IsOpen) {
              port.Close();
            }
          }
        }
        port=null;
        _scanBusy=0;
      }
      private static bool GetFrame(SerialPort port, ref XBFrame f) {
        byte b;
        if(port==null || !port.IsOpen) {
          return false;
        }
        while(port.BytesToRead>0) {
          b=(byte)port.ReadByte();
          if(b==0x7E) {            //Frame Delimiter
            f.cnt=-2;
            f.esc=false;
            continue;
          }
          if(b==0x7D) {            //Escape
            f.esc=true;
            continue;
          }
          if(f.esc) {              //Escaped
            b^=0x20;
            f.esc=false;
          }
          if(f.cnt==-2) {     //Length MSB
            f.length=b<<8;
            f.cnt++;
            continue;
          }
          if(f.cnt==-1) {     //Length LSB
            f.length|=b;
            f.crc=0;
            f.cnt++;
            continue;
          }
          if(f.cnt==f.length) {          //Checksum
            if(f.crc+b==0xFF) {
              return true;
            }
            f.Reset();
            continue;
          }
          if(f.cnt>=0) {
            f.buf[f.cnt++]=b;
            f.crc+=b;
          }
        }
        return false;
      }
      private static void SendRaw(SerialPort port, byte[] frame) {
        if(port==null || frame.Length==0 || !port.IsOpen)
          return;
        List<byte> txBuf=new List<byte>();
        byte txCRC=0;
        byte txB;
        txBuf.Add(0x7E);                        //Frame Delimiter
        txBuf.Add((byte)(frame.Length/256));    //Length MSB
        txBuf.Add((byte)(frame.Length));        //Length LSB
        for(int i=0; i<frame.Length; i++) {
          txB=frame[i];
          txCRC+=txB;
          if(txB==0x7E || txB==0x7D || txB==0x11 || txB==0x13) {
            txBuf.Add(0x7D);
            txB^=0x20;
          }
          txBuf.Add(txB);
        }
        txBuf.Add((byte)(0xFF-txCRC));
        port.Write(txBuf.ToArray(), 0, txBuf.Count);
      }
      private static void SendRaw(XISerial xi, XBFrame frm) {
        if(xi==null || xi._port==null || !xi._port.IsOpen || frm.length==0) {
          return;
        }
        List<byte> txBuf=new List<byte>();
        byte txCRC=0;
        byte txB;
        txBuf.Add(0x7E);                        //Frame Delimiter
        txBuf.Add((byte)(frm.length/256));    //Length MSB
        txBuf.Add((byte)(frm.length));        //Length LSB
        for(int i=0; i<frm.length; i++) {
          txB=frm.buf[i];
          txCRC+=txB;
          if(txB==0x7E || txB==0x7D || txB==0x11 || txB==0x13) {
            txBuf.Add(0x7D);
            txB^=0x20;
          }
          txBuf.Add(txB);
        }
        txBuf.Add((byte)(0xFF-txCRC));
        xi._port.Write(txBuf.ToArray(), 0, txBuf.Count);
      }
      private static UInt16 ToUShortNet(byte[] frame, ref int i) {
        UInt16 addr;
        addr=(ushort)(frame[i++]<<8 | frame[i++]);
        return addr;
      }
      private static UInt64 ToULongNet(byte[] frame, ref int i) {
        UInt64 sn=((UInt64)frame[i++]<<56) | ((UInt64)frame[i++]<<48) | ((UInt64)frame[i++]<<40) | ((UInt64)frame[i++]<<32) | ((UInt64)frame[i++]<<24) | ((UInt64)frame[i++]<<16) | ((UInt64)frame[i++]<<08) | (UInt64)frame[i++];
        return sn;
      }
      #endregion static

      #region instance
      private SerialPort _port;
      private Queue<XBFrame> _sendQueue;
      public DVar<XBeeDevice> gwTopic { get; private set; }
      private int _initSt;

      public XISerial(SerialPort port) {
        _port = port;
        _sendQueue=new Queue<XBFrame>();
        ThreadPool.QueueUserWorkItem(CommThread);
      }
      public void Send(XBFrame frm) {
        lock(_sendQueue) {
          _sendQueue.Enqueue(frm);
        }
      }
      private void CommThread(object o) {
        XBFrame iFrame=new XBFrame() { buf=new byte[300] };
        XBFrame oFrm=new XBFrame();
        iFrame.Reset();
        try {
          while(_port!=null && _port.IsOpen) {
            if(GetFrame(_port, ref iFrame)) {
              ParseInPacket(iFrame.buf, iFrame.length);
              continue;
            } else {
              oFrm.Reset();
              lock(_sendQueue) {
                if(_sendQueue.Count>0) {
                  oFrm=_sendQueue.Dequeue();
                }
              }
              SendRaw(this, oFrm);
            }
            Thread.Sleep(15);
            if(gwTopic!=null && gwTopic.value!=null && gwTopic.value._gate==null) {
              break;
            }
          }
        }
        catch(IOException) {
        }
        catch(Exception ex) {
          Log.Error("MsGSerial({0}).CommThread() - {1}", _port!=null?_port.PortName:string.Empty, ex.ToString());
        }
        this.Dispose();
      }

      private void ParseInPacket(byte[] frame, int length) {
        Topic devR=Topic.root.Get("/dev");
        int i=1;
        UInt16 addr;
        UInt64 sn;
        ushort cmdCode;
        byte frameId;
        DVar<XBeeDevice> dev;
        DVar<XBeeDevice> pDev=null;
        byte[] buf;
        UInt16 pAddr;
        byte dType;
        string id;
        XBeeATCommand cmd;
        XBeeATCommandStatus atStatus;
        StringBuilder tmp;
        try {
          switch((XBeeCmdID)frame[0]) {
          #region ATCommandResponse 0x88
          case XBeeCmdID.ATCommandResponse:
            frameId=frame[i++];
            cmd=(XBeeATCommand)ToUShortNet(frame, ref i);
            atStatus=(XBeeATCommandStatus)frame[i++];
            if(atStatus!=XBeeATCommandStatus.OK) {
              if(gwTopic!=null && gwTopic.value!=null) {
                buf=new byte[length-i];
                Array.Copy(frame, i, buf, 0, buf.Length);
                gwTopic.value.CmdResponse(cmd, atStatus, buf);
              }
              break;
            }
            if(length>=i) {
              if(_verbose) {
                Log.Debug("response {0}: {1}", cmd, BitConverter.ToString(frame, i, length-i));
              }
              int j;
              switch(cmd) {
              case XBeeATCommand.NodeIdentifier: {
                  id=ASCIIEncoding.ASCII.GetString(frame, i, length-i);
                  gwTopic=devR.Get<XBeeDevice>(id);
                  if(gwTopic.value==null) {
                    gwTopic.value=new XBeeDevice();
                  }
                  gwTopic.value._gate=this;
                  Thread.Sleep(0);
                  InitStep();
                }
                break;
              case XBeeATCommand.SerialNumberHigh:
                if(gwTopic!=null && gwTopic.value!=null) {
                  gwTopic.value._sn=((UInt64)frame[i++]<<56) | ((UInt64)frame[i++]<<48) | ((UInt64)frame[i++]<<40) | ((UInt64)frame[i++]<<32);
                } else {
                  _initSt=0;
                }
                InitStep();
                break;
              case XBeeATCommand.SerialNumberLow:
                if(gwTopic!=null && gwTopic.value!=null) {
                  gwTopic.value._sn|=((UInt64)frame[i++]<<24) | ((UInt64)frame[i++]<<16) | ((UInt64)frame[i++]<<8) | (UInt64)frame[i++];
                  gwTopic.value.via=_port.PortName;
                } else {
                  _initSt=0;
                }
                InitStep();
                break;
              case XBeeATCommand.NetworkAddress:
                if(gwTopic!=null && gwTopic.value!=null) {
                  gwTopic.value._addr=ToUShortNet(frame, ref i);
                } else {
                  _initSt=0;
                }
                InitStep();
                break;
              case XBeeATCommand.NodeDiscovery: {
                  addr=ToUShortNet(frame, ref i);
                  sn = ToULongNet(frame, ref i);
                  j=i;
                  for(; frame[i]!=0; i++)
                    ;
                  id=ASCIIEncoding.ASCII.GetString(frame, j, i-j);
                  i++;
                  pAddr=ToUShortNet(frame, ref i);
                  i+=2;
                  dType=frame[i++];
                  i++;
                  if(string.IsNullOrWhiteSpace(id)) {
                    id=string.Format("XB_{0:X4}", addr);
                  }
                  dev=devR.Get<XBeeDevice>(id);
                  if(dev.value==null) {
                    dev.value=new XBeeDevice();
                  }
                  if(dev==gwTopic) {
                    break;
                  }
                  dev.value._gate=this;
                  dev.value._sn=sn;
                  dev.value._addr=addr;
                  if(pAddr!=0xFFFE) {
                    pDev=devR.children.Where(z => z.valueType==typeof(XBeeDevice)).Cast<DVar<XBeeDevice>>().FirstOrDefault(z => z!=null && z.value!=null && z.value._addr==pAddr);
                  }
                  Thread.Sleep(0);
                  dev.value.via=pDev!=null?pDev.name:(gwTopic==null?string.Empty:gwTopic.name);
                }
                break;
              default:
                if(gwTopic!=null && gwTopic.value!=null) {
                  buf=new byte[length-i];
                  Array.Copy(frame, i, buf, 0, buf.Length);
                  gwTopic.value.CmdResponse(cmd, atStatus, buf);
                }
                break;
              }
            }
            break;
          #endregion ATCommandResponse
          #region ModemStatus 0x8A
          case XBeeCmdID.ModemStatus:
            if(frame[1]==6) {  //Coordinator started
              _initSt=0;
              InitStep();
            }
            break;
          #endregion ModemStatus
          #region TransmitStatus 0x8B
          case XBeeCmdID.TransmitStatus:
              frameId=frame[i++];
            addr=ToUShortNet(frame, ref i);
              byte cnt=frame[i++];
              DeliveryStatus deliveryStatus=(DeliveryStatus)frame[i++];
              DiscoveryStatus discoveryStatus=(DiscoveryStatus)frame[i++];
            dev=devR.children.Where(z => z.valueType==typeof(XBeeDevice)).Cast<DVar<XBeeDevice>>().FirstOrDefault(z => z!=null && z.value!=null && z.value._addr==addr);
            if(_verbose) {
              Log.Debug("{0} TransmitStatus {1}, {2}", dev==null?addr.ToString("X4"):dev.path, deliveryStatus, discoveryStatus);
            }
            dev.value.CmdResponse(XBeeATCommand.TransmitRequest, deliveryStatus==DeliveryStatus.Success?XBeeATCommandStatus.OK:XBeeATCommandStatus.ERROR, null);
            break;
          #endregion TransmitStatus
          #region ReceivePacket 0x90
          case XBeeCmdID.ReceivePacket:
            sn = ToULongNet(frame, ref i);
            addr=ToUShortNet(frame, ref i);
            i++;        //Recive Options
            dev=devR.children.Where(z => z.valueType==typeof(XBeeDevice)).Cast<DVar<XBeeDevice>>().FirstOrDefault(z => z!=null && z.value!=null && (z.value._sn==sn || z.value._addr==addr));
            if(dev!=null && length-i>0) {
              if(dev.value._gate==null) {
                SentATCommand(dev, XBeeATCommand.SoftwareReset);
                break;
              }
              dev.value.ReceivePacket(frame.Take(length).Skip(i).ToArray());
            }
            break;
          #endregion ReceivePacket
          #region DataSampleRxIndicator 0x92
          case XBeeCmdID.DataSampleRxIndicator:
            sn = ToULongNet(frame, ref i);
            addr=ToUShortNet(frame, ref i);

            i++;        //Receive Options
            if(frame[i]!=1)
              break;    //Number of sample sets included in the payload. (Always set to 1)
            dev=devR.children.Where(z => z.valueType==typeof(XBeeDevice)).Cast<DVar<XBeeDevice>>().FirstOrDefault(z => z!=null && z.value!=null && (z.value._sn==sn || z.value._addr==addr));
            if(dev!=null) {
              if(dev.value._gate==null) {
                SentATCommand(dev, XBeeATCommand.SoftwareReset);
                break;
              }
              buf=new byte[frame.Length-i];
              Array.Copy(frame, i, buf, 0, buf.Length);
              dev.value.ReceiveDataSample(buf);
            }
            break;
          #endregion DataSampleRxIndicator
          #region ModulIdentificationIndicator  0x95
          case XBeeCmdID.ModulIdentificationIndicator: {
              sn=ToULongNet(frame, ref i);
              addr = ToUShortNet(frame, ref i);
              i++;        //Recive Options
              UInt16 addr2=ToUShortNet(frame, ref i);
              UInt64 sn2=ToULongNet(frame, ref i);
              for(; frame[i]!=0; i++)
                ;
              id=ASCIIEncoding.ASCII.GetString(frame, 22, i-22);
              i++;
              pAddr=ToUShortNet(frame, ref i);
              dType=frame[i];
              i++;
              if(string.IsNullOrWhiteSpace(id)) {
                id=string.Format("XB_{0:X4}", addr);
              }
              dev=devR.Get<XBeeDevice>(id);
              if(dev==gwTopic) {
                break;
              }
              if(dev.value==null) {
                dev.value=new XBeeDevice();
              }
              dev.value._gate=this;
              dev.value._sn=sn;
              dev.value._addr=addr;
              if(pAddr!=0xFFFE) {
                pDev=devR.children.Where(z => z.valueType==typeof(XBeeDevice)).Cast<DVar<XBeeDevice>>().FirstOrDefault(z => z!=null && z.value!=null && z.value._addr==pAddr);
              }
              Thread.Sleep(0);
              dev.value.via=pDev!=null?pDev.name:(gwTopic==null?string.Empty:gwTopic.name);
            }
            break;
          #endregion ModulIdentificationIndicator
          #region RemoteCommandResponse 0x97
          case XBeeCmdID.RemoteCommandResponse:
            frameId=frame[i++];
            sn=ToULongNet(frame, ref i);
            addr=ToUShortNet(frame, ref i);
            cmd=(XBeeATCommand)ToUShortNet(frame, ref i);
            atStatus=(XBeeATCommandStatus)frame[i++];
            dev=devR.children.Where(z => z.valueType==typeof(XBeeDevice)).Cast<DVar<XBeeDevice>>().FirstOrDefault(z => z!=null && z.value!=null && z.value._addr==addr);
            buf=new byte[length-i];
            Array.Copy(frame, i, buf, 0, buf.Length);
            if(_verbose) {
              Log.Debug("{0} Response {1}={2}, {3}", dev==null?sn.ToString("X"):dev.path, cmd, atStatus, BitConverter.ToString(buf));
            }
            if(dev.value._gate==null) {
              SentATCommand(dev, XBeeATCommand.SoftwareReset);
              break;
            }
            dev.value.CmdResponse(cmd, atStatus, buf);
            break;
          #endregion RemoteCommandResponse
          }
        }
        catch(Exception ex) {
          Log.Error(ex.Message);
        }
      }
      public void SentATCommand(XBeeDevice dev, XBeeATCommand cmd, byte[] param=null) {
        byte[] buf=null;
        if(dev==null || (gwTopic!=null && dev==gwTopic.value)) {
          if(param==null || param.Length==0) {
            buf=new byte[4];
          } else {
            buf=new byte[4+param.Length];
            param.CopyTo(buf, 4);
          }
          buf[0]=(byte)XBeeCmdID.ATCommand;
          buf[1]=frameID();
          buf[2]=(byte)((ushort)cmd>>8);
          buf[3]=(byte)((ushort)cmd);
        } else {
          if(param==null || param.Length==0) {
            buf=new byte[15];
          } else {
            buf=new byte[15+param.Length];
            param.CopyTo(buf, 15);
          }
          buf[0]=(byte)XBeeCmdID.RemoteCommandRequest;
          buf[1]=frameID();
          buf[2]=(byte)(dev._sn>>56);
          buf[3]=(byte)(dev._sn>>48);
          buf[4]=(byte)(dev._sn>>40);
          buf[5]=(byte)(dev._sn>>32);
          buf[6]=(byte)(dev._sn>>24);
          buf[7]=(byte)(dev._sn>>16);
          buf[8]=(byte)(dev._sn>>8);
          buf[9]=(byte)dev._sn;
          buf[10]=(byte)(dev._addr>>8);
          buf[11]=(byte)dev._addr;
          buf[12]=0x02;   // apply changes
          buf[13]=(byte)((ushort)cmd>>8);
          buf[14]=(byte)((ushort)cmd);
        }
        if(buf!=null) {
          Send(new XBFrame() { buf=buf, length=buf.Length });
          if(_verbose) {
            Log.Debug("SendAtCommand({0}, {1}, {2})", dev==null?string.Empty:dev.backName, cmd, param!=null?BitConverter.ToString(param):"null");
          }
        }
      }
      public void SendToSerial(XBeeDevice dev, byte[] data) {
        byte[] buf=null;
        if(dev==null || gwTopic==null || dev==gwTopic.value || data==null || data.Length==0) {
          return;
        }
        buf=new byte[14+data.Length];
        buf[0]=(byte)XBeeCmdID.TransmitRequest;
        buf[1]=frameID();
        buf[2]=(byte)(dev._sn>>56);
        buf[3]=(byte)(dev._sn>>48);
        buf[4]=(byte)(dev._sn>>40);
        buf[5]=(byte)(dev._sn>>32);
        buf[6]=(byte)(dev._sn>>24);
        buf[7]=(byte)(dev._sn>>16);
        buf[8]=(byte)(dev._sn>>8);
        buf[9]=(byte)dev._sn;
        buf[10]=(byte)(dev._addr>>8);
        buf[11]=(byte)dev._addr;
        buf[12]=0;   // broadcast radius
        buf[13]=0;   // options
        data.CopyTo(buf, 14);
        Send(new XBFrame() { buf=buf, length=buf.Length });
        if(_verbose) {
          Log.Debug("TransmitRequest({0}, {1})", dev==null?string.Empty:dev.backName, BitConverter.ToString(data));
        }
      }

      private void InitStep() {
        switch(_initSt++) {
        case 0:
          SentATCommand(null, XBeeATCommand.NodeIdentifier);
          break;
        case 1:
          SentATCommand(null, XBeeATCommand.SerialNumberHigh);
          break;
        case 2:
          SentATCommand(null, XBeeATCommand.SerialNumberLow);
          break;
        case 3:
          SentATCommand(null, XBeeATCommand.NodeDiscovery);
          break;
        }
      }

      private int _frameIdGenerator=2;
      private byte frameID() {
        Interlocked.CompareExchange(ref _frameIdGenerator, 1, 0xFF);
        return (byte)Interlocked.Increment(ref _frameIdGenerator);
      }
      private void Dispose() {
        if(_port!=null && _port.IsOpen) {
          _port.Close();
        }
        _port=null;
        _ifs.Remove(this);
        Topic dev=Topic.root.Get("/dev");
        lock(dev) {
          var ifs=dev.children.Where(z => z.valueType==typeof(XBeeDevice)).Cast<DVar<XBeeDevice>>().Where(z => z.value!=null && z.value._gate==this).Select(z => z.value).ToArray();
          foreach(var t in ifs) {
            t._gate=null;
            t.Disconnect();
          }
        }
      }

      #endregion instance

      public struct XBFrame {
        public void Reset() {
          length=0;
          cnt=-3;
          esc=false;
        }
        public int length;
        public int cnt;
        public bool esc;
        public byte crc;
        public byte[] buf;
      }


    }

  }
}
