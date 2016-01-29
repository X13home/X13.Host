using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.Periphery {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class DevicePLC : ITopicOwned, X13.PLC.IPiDocument {
    private static DVar<bool> _verbose;

    static DevicePLC() {
      _verbose = Topic.root.Get<bool>("/etc/MQTT-SN/PLC/verbose");
    }

    private Topic _owner;
    private MsDevice _dev;
    private int _offset, _st;  // 0 - idle, 1 - check CRC, 2- check CRC resp, 3-PLC stop, 4 - PLC stop resp, 5- write block, 6- write block resp, 7- PLC start resp
    private Chunk _curChunk;
    private SortedSet<Chunk> _prg;
    private bool _plcStoped;

    public DevicePLC() {
      _prg = new SortedSet<Chunk>();
      if(signature == 0) {
        signature = (new Random()).Next(1, int.MaxValue);
      }
    }
    public DevicePLC(Topic owner)
      : this() {
      SetOwner(owner);
    }

    [Newtonsoft.Json.JsonProperty]
    public int signature { get; set; }
    public string View { get { return "JavaScript"; } }

    public void Reset() {
      _st = 1;
      _plcStoped = false;
      foreach(var c in _prg) {
        c.crcDev = -1;
      }
      if(_verbose.value) {
        Log.Info("{0}[{1}].Reset", _owner == null ? null : _owner.path, signature);
      }
    }
    public void Recv(byte[] msgData) {
      if(msgData == null || msgData.Length == 0) {
        return;
      }
      bool processed = false;
      int oSt = _st;

      if(_st == 2 && msgData[0] == (byte)Cmd.GetCRCResp) {
        if(_curChunk != null && msgData.Length == 4 && msgData[1] == 0) {
          _curChunk.crcDev = (msgData[3] << 8) | msgData[2];
          if(_curChunk.crcDev == _curChunk.crcCur) {
            _curChunk = null;
            _st = 1;
          } else {
            _st = _plcStoped ? 5 : 3;
          }
          processed = true;
        }
      } else if(msgData[0] == (byte)Cmd.PlcStopResp) {
        if(msgData[1] != 0) {
          if(msgData.Length == 18) {
            processed = true;
            _plcStoped = true;
            Log.Warning("{0}.PlcStop({1}) SP={2:X4}, *SP={3:X4}, SFP={4:X4}, PC={5:X4}", _owner, ((ErrorCode)msgData[1]).ToString(), BitConverter.ToUInt32(msgData, 2), BitConverter.ToInt32(msgData, 6), BitConverter.ToUInt32(msgData, 10), BitConverter.ToUInt32(msgData, 14));
          } else {
            processed = false;
          }
          _st = 0;
        } else {
          _plcStoped = true;
          _st = _curChunk == null ? 1 : 5;
          processed = true;
        }
      } else if(_st == 6 && msgData.Length == 2 && msgData[0] == (byte)Cmd.WriteBlockResp) {
        if(msgData[1] == 0) {  // success
          _offset += 32;
          if(_offset > _curChunk.Data.Length) {
            _curChunk.crcDev = _curChunk.crcCur;
            _curChunk = null;
            _st = 1;
            _offset = 0;
          } else {
            _st = 5;
          }
          processed = true;
        }
      } else if(_st == 7 && msgData[0] == (byte)Cmd.PlcStartResp) {
        _st = 0;
        _plcStoped = false;
        processed = true;
      }
      if(!processed) {
        if(_verbose.value) {
          Log.Warning("{0}.Recv({1}) {2}-{3}", _owner, BitConverter.ToString(msgData), ((Cmd)msgData[0]), msgData.Length > 1 ? ((ErrorCode)msgData[1]).ToString() : "empty");
        }
        _st = 0;
      } else if(_verbose.value && _st != oSt) {
        Log.Info("{0}[{1}]._st={2}=>{3}", _owner == null ? null : _owner.path, signature, oSt, _st);
      }
    }
    private void Pool() {
      if(_dev == null || _owner == null) {
        return;
      }
      if(_dev.state != MsDevice.State.Connected && _dev.state != MsDevice.State.AWake && _dev.state != MsDevice.State.ASleep) {
        return;
      }
      byte[] buf;

      if(_st == 0) {
        return;
      } else {
        int oSt = _st;
        if(_curChunk == null) {
          _st = 1;
        }
        if(_st == 1) {
          if(_curChunk == null) {
            _curChunk = _prg.FirstOrDefault(z => z.crcCur != z.crcDev);
            if(_curChunk == null) {
              if(_plcStoped) {
                buf = new byte[] { (byte)Cmd.PlcStartReq };
                _st = 7;
                _dev.PublishWithPayload(_owner, buf);
              } else {
                _st = 0;
              }
              return;
            }
          }
          buf = new byte[] { (byte)Cmd.GetCRCReq, (byte)_curChunk.offset, (byte)(_curChunk.offset >> 8), (byte)_curChunk.Data.Length, (byte)(_curChunk.Data.Length >> 8) };
          _dev.PublishWithPayload(_owner, buf);
          _st = 2;
        } else if(_st == 3) {
          buf = new byte[] { (byte)Cmd.PlcStopReq };
          _st = 4;
          _dev.PublishWithPayload(_owner, buf);
        } else if(_st == 5) {
          int len = _curChunk.Data.Length - _offset;
          if(len > 32) {
            len = 32;
          }
          buf = new byte[len + 5];
          int addr = _curChunk.offset + _offset;
          buf[0] = (byte)(Cmd.WriteBlockReq);
          buf[1] = (byte)addr;
          buf[2] = (byte)(addr >> 8);
          Buffer.BlockCopy(_curChunk.Data, _offset, buf, 3, len);
          ushort crc = Crc16.UpdateCrc(0xFFFF, buf.Skip(3).Take(len).ToArray());
          buf[len + 3] = (byte)crc;
          buf[len + 4] = (byte)(crc >> 8);
          _st = 6;
          _dev.PublishWithPayload(_owner, buf);
        }
        if(_verbose.value && _st != oSt) {
          Log.Info("{0}[{1}]._st={2}=>{3}", _owner == null ? null : _owner.path, signature, oSt, _st);
        }
      }
    }
    public void SetOwner(Topic owner) {
      if(owner == _owner) {
        return;
      }
      if(_owner != null) {
        if(Topic.brokerMode) {
          _owner.Unsubscribe("+", VarChanged);
        }
      }
      if(_dev != null) {
        _dev.Pool -= Pool;
      }
      _owner = owner;
      if(_owner != null) {
        if(Topic.brokerMode) {
          if(_owner.parent != null && _owner.parent.valueType == typeof(MsDevice)) {
            _dev = (_owner.parent as DVar<MsDevice>).value;
          }
          _owner.Get<string>("_declarer", _owner).value = "DevicePLC";
          _owner.Subscribe("+", VarChanged);
          if(_dev != null) {
            _dev.Pool += Pool;
            Reset();
          }
        }
      }
    }
    public override string ToString() {
      return "[" + _prg.Count.ToString() + "]";
    }

    private void VarChanged(Topic snd, TopicChanged p) {
      int start;
      if(!snd.name.StartsWith("pa") || snd.valueType != typeof(PLC.ByteArray) || !int.TryParse(snd.name.Substring(2), out start)) {
        return;
      }
      Chunk ch = _prg.FirstOrDefault(z => z.offset == start);
      var pa = snd.GetValue() as PLC.ByteArray;
      byte[] data = pa == null ? null : pa.GetBytes();

      if(p.Art == TopicChanged.ChangeArt.Remove || data == null) {
        if(ch != null) {
          _prg.Remove(ch);
        }
      } else {
        if(ch == null) {
          ch = new Chunk(start);
          _prg.Add(ch);
        }
        if(data == null || ch.Data == null || !data.SequenceEqual(ch.Data)) {
          ch.Data = data;
          ch.crcCur = Crc16.UpdateCrc(0xFFFF, ch.Data);
          if(System.Threading.Interlocked.CompareExchange(ref _st, 1, 0) == 0) {
            _curChunk = ch;
          }
        }
      }
    }
    private enum Cmd : byte {
      Idle = 0,
      PlcStartReq = 1,      // 1
      PlcStartResp = 2,     // 2, 0
      PlcStopReq = 3,       // 3
      PlcStopResp = 4,      // 4, 0
      GetCRCReq = 5,        // 5, addrL, addrH, lenL, lenH
      GetCRCResp = 6,       // 6, 0, crcL, crcH
      WriteBlockReq = 7,    // 7, addrL, addrH, [data(length = packet lenght-5)], crcL, crcH
      WriteBlockResp = 8,   // 8, 0
      EraseBlockReq = 9,    // 9, addrL, addrH, lenL, lenH
      EraseBlockResp = 10,  //10, 0
    }
    private enum ErrorCode : byte {
      Success = 0x00,

      UnknownOperation = 0x80,
      ProgrammOutOfRange = 0x81,
      RamOutofRange = 0x82,
      TestError = 0x83,
      Watchdog=0x84,

      WrongState = 0xFA,
      CrcError = 0xFB,
      OutOfRange = 0xFC,
      FormatError = 0xFD,
      UnknowmCmd = 0xFE,
    }
    private class Chunk : IComparable<Chunk> {
      public int offset;
      public int crcDev;
      public int crcCur;
      public byte[] Data;

      public Chunk(int offset) {
        this.offset = offset;
        crcDev = -1;
      }
      public override string ToString() {
        return offset.ToString("X4") + "[" + (Data == null ? "null" : Data.Length.ToString("X4")) + (crcCur == crcDev ? " ok" : " !");
      }
      public int CompareTo(Chunk other) {
        if(other == null) {
          return 1;
        }
        return this.offset.CompareTo(other.offset);
      }
    }
  }
}
