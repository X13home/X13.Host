#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace X13.Periphery {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class TWIDriver : ITopicOwned {
    private static DVar<bool> _verbose;
    private static bool _loaded;

    static TWIDriver() {
      _verbose=Topic.root.Get<bool>("/etc/TWI/verbose");
      _loaded=false;
    }
    public static void Load() {
      if(_loaded) {
        return;
      }
      _loaded=true;
      using(var sr=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.Periphery.TWI.xst"))) {
        Topic.Import(sr, null);
      }
    }
    //===================================================
    private Topic _owner;
    private MsDevice _dev;
    private Timer _pollTimer;
    private List<TWICommon> _drivers;
    private int _pollIdx;

    public TWIDriver() {
      _drivers=new List<TWICommon>();
      _pollTimer=new Timer(Poll);
      _pollIdx=0;
    }

    public TWIDriver(Topic owner)
      : this() {
      SetOwner(owner);
    }

    [Newtonsoft.Json.JsonProperty]
    public string name { get; set; }

    public void Reset() {
      if(Topic.brokerMode) {
        _pollTimer.Change(300, 100);
        for(int i=_drivers.Count-1; i>=0; i--) {
          _drivers[i].Reset();
        }
      }
    }

    public void Recv(byte[] msgData) {
      if(msgData==null) {
        return;
      }
      for(int i=_drivers.Count-1; i>=0; i--) {
        if(_drivers[i].Recv(msgData)) {
          return;
        }
      }
    }

    public void SetOwner(Topic owner) {
      if(_owner!=null) {
        if(Topic.brokerMode) {
          _owner.Unsubscribe("+", STVarChanged);
        }
      }
      _owner=owner;
      if(_owner!=null) {
        name=owner.name;
        if(Topic.brokerMode) {
          if(_owner.parent!=null && _owner.parent.valueType==typeof(MsDevice)) {
            _dev=(_owner.parent as DVar<MsDevice>).value;
          }
          _owner.Get<string>("_declarer", _owner).value="TWI";
          _owner.Subscribe("+", STVarChanged);
          if(_dev!=null) {
            Reset();
          }
        }
      } else {
        _pollTimer.Change(-1, -1);
      }
    }

    private void STVarChanged(Topic snd, TopicChanged p) {
      if(p.Visited(_owner, true)) {
        return;
      }
      for(int i=_drivers.Count-1; i>=0; i--) {
        if(_drivers[i].VarChanged(snd, p.Art==TopicChanged.ChangeArt.Remove)) {
          if(p.Art==TopicChanged.ChangeArt.Remove) {
            lock(_drivers) {
              _drivers.RemoveAt(i);
            }
          }
          return;
        }
      }
      if(p.Art==TopicChanged.ChangeArt.Remove) {
        return;
      }
      TWICommon drv=null;
      switch(snd.name) {
      case "LM75_T0":
      case "LM75_T1":
      case "LM75_T2":
      case "LM75_T3":
        drv=new LM75(snd);
        break;
      case "CC2D_T":
      case "CC2D_H":
        drv=new CC2D(snd);
        break;
      case "HIH61_T":
      case "HIH61_H":
        drv=new HIH61xx(snd);
        break;
      case "BMP180_T":
      case "BMP180_P":
        drv=new BMP180(snd);
        break;
      case "BLINKM_RGB8":
      case "BLINKM_F8":
      case "BLINKM_RGB9":
      case "BLINKM_F9":
      case "BLINKM_RGB10":
      case "BLINKM_F10":
        drv=new Blinky(snd);
        break;
      }
      if(drv!=null) {
        lock(_drivers) {
          for(int i=_drivers.Count-1; i>=0; i--) {
            if(_drivers[i].VarChanged(snd, false)) {
              drv=null;
              break;
            }
          }
          if(drv!=null) {
            _drivers.Add(drv);
          }
        }
      }
    }
    private void Poll(object state) {
      try {
        if(_dev==null || _owner==null) {
          _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
          return;
        }
        if(_dev.state!=MsDevice.State.Connected) {
          return;
        }
        byte[] buf;
        if(_pollIdx>=_drivers.Count) {
          _pollIdx=0;
        } else {
          if(!_drivers[_pollIdx].Poll(out buf)) {
            _pollIdx++;
          }
          if(buf!=null) {
            _dev.PublishWithPayload(_owner, buf);
          }
        }
      }
      catch(Exception ex) {
        Log.Warning("{0}.Poll -{1}", _owner!=null?_owner.path:"UNK", ex.Message);
      }
    }
    [Flags]
    private enum AckFlags : byte {
      WRITE=0x01,         // Write access
      READ=0x02,          // Read access
      Busy=0x08,          // Bus busy
      Ok=0x10,            // Access complete
      Timeout=0x20,       // Timeout
      SlaveNAck=0x40,     // Slave Addr NACK received
      ERROR=0x80          // Unknown error
    }
    private abstract class TWICommon {
      protected static Random _rand;
      static TWICommon() {
        _rand=new Random((int)DateTime.Now.Ticks);
      }
      public abstract bool VarChanged(Topic snd, bool delete);
      public abstract bool Recv(byte[] buf);
      public abstract bool Poll(out byte[] buf);
      public abstract void Reset();
    }
    private class LM75 : TWICommon {
      private DVar<double> _T;
      private byte addr;
      private DateTime _pt;
      private bool _busy;
      private DVar<bool> _present;

      public LM75(Topic owner) {
        _T=owner as DVar<double>;
        if(_T==null) {
          throw new ArgumentException();
        }
        _present=_T.Get<bool>("present");
        _present.saved=false;
        _present.value=false;
        addr=(byte)(0x48+int.Parse(_T.name.Substring(6)));
        Reset();
      }
      public override bool VarChanged(Topic snd, bool delete) {
        return (snd==_T);
      }
      public override bool Recv(byte[] buf) {
        if(buf.Length==6 && buf[0]==addr) {
          if(buf[1]==0x10) {
            _T.value=(short)((buf[4]<<8) | buf[5])/256.0;
            _present.value=true;
          } else {
            _present.value=false;
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _T.path, (AckFlags)buf[1]);
            }
          }
          _pt=DateTime.Now.AddSeconds(_rand.Next(45, 90));
          _busy=false;
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        if(_busy) {
          //if(_pt<DateTime.Now) {
          //  _busy=false;
          //  if(_verbose) {
          //    Log.Warning("{0}.poll({1}) - timeot", _T, _busy?1:0);
          //  }
          //  _present.value=false;
          //  _pt=DateTime.Now.AddSeconds(_rand.Next(100, 200));
          //}
          buf=null;
          return true;
        }
        if(_pt<DateTime.Now) {
          buf=new byte[] { addr, 0x03, 0x01, 0x02, 0x00 };
          _pt=DateTime.Now.AddSeconds(1);
          _busy=true;
          return true;
        }
        buf=null;
        return false;
      }
      public override void Reset() {
        _busy=false;
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(800, 2000));
      }
    }
    private class CC2D : TWICommon {
      private const byte ADDR=0x28;
      private DVar<double> _T;
      private DVar<double> _H;
      private DateTime _pt;
      private DVar<bool> _present;
      private int _st;

      public CC2D(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        if(pin.name=="CC2D_T") {
          _T=pin as DVar<double>;
          if(_T==null) {
            throw new ArgumentException();
          }
          _H=_T.parent.Get<double>("CC2D_H");
        } else if(pin.name=="CC2D_H") {
          _H=pin as DVar<double>;
          if(_H==null) {
            throw new ArgumentException();
          }
          _T=_H.parent.Get<double>("CC2D_T");
        } else {
          throw new ArgumentException();
        }
        _present=_T.Get<bool>("present");
        _present.saved=false;
        _present.value=false;
        Reset();
      }
      public override bool VarChanged(Topic snd, bool delete) {
        if(snd==_T) {
          if(delete && _H!=null) {
            _H.Remove();
          }
          return true;
        } else if(snd==_H) {
          if(delete && _T!=null) {
            _T.Remove();
          }
          return true;
        }
        return false;
      }
      public override bool Recv(byte[] buf) {
        if(buf.Length>=4 && buf[0]==ADDR) {
          if(buf[1]==0x10) {
            if(buf.Length==8) {
              _T.value=Math.Round(((buf[6]<<6) | (buf[7]>>2))*165.0/16384-40, 2);
              _H.value=Math.Round(((buf[4]<<8) | buf[5])*25.0/4096, 1);
              _present.value=true;
              _pt=DateTime.Now.AddSeconds(_rand.Next(45, 90));
              _st=0;
            }
          } else {
            _present.value=false;
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _T.path, (AckFlags)buf[1]);
            }
            _pt=DateTime.Now.AddSeconds(_rand.Next(15, 30));
            _st=0;
          }
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        buf=null;
        bool busy=false;
        if(_pt<DateTime.Now) {
          if(_st==0) {
            buf=new byte[] { ADDR, 0x01, 0x00, 0x00 }; // Write 0 bytes
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=1;
            busy=true;
          } else if(_st==1) {
            buf=new byte[] { ADDR, 0x02, 0x00, 0x04 }; // Read 4 bytes
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=3;
            busy=true;
          } else {
            busy=true;
            //_present.value=false;
            //if(_verbose) {
            //  Log.Warning("{0}.poll({1}) - timeot", _T, _st);
            //}
            //_pt=DateTime.Now.AddSeconds(_rand.Next(100, 200));
            //_st=0;
          }
        } else {
          busy=_st>0;
        }
        return busy;
      }
      public override void Reset() {
        _st=0;
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(800, 2000));
      }
    }
    private class HIH61xx : TWICommon {
      private const byte ADDR=0x27;
      private DVar<double> _T;
      private DVar<double> _H;
      private DateTime _pt;
      private DVar<bool> _present;
      private int _st;

      public HIH61xx(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        if(pin.name=="HIH61_T") {
          _T=pin as DVar<double>;
          if(_T==null) {
            throw new ArgumentException();
          }
          _H=_T.parent.Get<double>("HIH61_H");
        } else if(pin.name=="HIH61_H") {
          _H=pin as DVar<double>;
          if(_H==null) {
            throw new ArgumentException();
          }
          _T=_H.parent.Get<double>("HIH61_T");
        } else {
          throw new ArgumentException();
        }
        _present=_T.Get<bool>("present");
        _present.saved=false;
        _present.value=false;
        Reset();
      }
      public override bool VarChanged(Topic snd, bool delete) {
        if(snd==_T) {
          if(delete && _H!=null) {
            _H.Remove();
          }
          return true;
        } else if(snd==_H) {
          if(delete && _T!=null) {
            _T.Remove();
          }
          return true;
        }
        return false;
      }
      public override bool Recv(byte[] buf) {
        if(buf.Length>=4 && buf[0]==ADDR) {
          if(buf[1]==0x10) {
            if(buf.Length==8) {
              if((buf[4] & 0xC0)==0) {
                _T.value=Math.Round((((buf[6]<<6) | (buf[7]>>2)) & 0x3FFF) *55.0/5461-40, 2);
                _H.value=Math.Round(((buf[4]<<2) | (buf[5] >> 6))*20.0/51, 1);
                _present.value=true;
              }
              _pt=DateTime.Now.AddSeconds(_rand.Next(45, 90));
              _st=0;
            }
          } else {
            _present.value=false;
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _T.path, (AckFlags)buf[1]);
            }
            _pt=DateTime.Now.AddSeconds(_rand.Next(15, 30));
            _st=0;
          }
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        buf=null;
        bool busy=false;
        if(_pt<DateTime.Now) {
          if(_st==0) {
            buf=new byte[] { ADDR, 0x01, 0x00, 0x00 }; // Write 0 bytes
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=1;
            busy=true;
          } else if(_st==1) {
            buf=new byte[] { ADDR, 0x02, 0x00, 0x04 }; // Read 4 bytes
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=3;
            busy=true;
          } else {
            busy=true;
            //_present.value=false;
            //if(_verbose) {
            //  Log.Warning("{0}.poll({1}) - timeot", _T, _st);
            //}
            //_pt=DateTime.Now.AddSeconds(_rand.Next(100, 200));
            //_st=0;
          }
        } else {
          busy=_st>0;
        }
        return busy;
      }
      public override void Reset() {
        _st=0;
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(800, 2000));
      }
    }
    private class BMP180 : TWICommon {
      private const byte ADDR=0x77;
      private const int BMP180_OSS=3;
      private DVar<double> _T;
      private DVar<long> _P;
      private DateTime _pt;
      private DVar<bool> _present;
      private int _st;

      private  short _ac1;
      private  short _ac2;
      private  short _ac3;
      private ushort _ac4;
      private ushort _ac5;
      private ushort _ac6;
      private  short _b1;
      private  short _b2;
      private  short _mb;
      private  short _mc;
      private  short _md;
      private    int _ut;

      public BMP180(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        if(pin.name=="BMP180_T") {
          _T=pin as DVar<double>;
          if(_T==null) {
            throw new ArgumentException();
          }
          _P=_T.parent.Get<long>("BMP180_P");
        } else if(pin.name=="BMP180_P") {
          _P=pin as DVar<long>;
          if(_P==null) {
            throw new ArgumentException();
          }
          _T=_P.parent.Get<double>("BMP180_T");
        } else {
          throw new ArgumentException();
        }
        _present=_T.Get<bool>("present");
        _present.saved=false;
        _present.value=false;
        Reset();
      }

      public override bool VarChanged(Topic snd, bool delete) {
        if(snd==_T) {
          if(delete && _P!=null) {
            _P.Remove();
          }
          return true;
        } else if(snd==_P) {
          if(delete && _T!=null) {
            _T.Remove();
          }
          return true;
        }
        return false;
      }
      public override bool Recv(byte[] buf) {
        if(buf.Length>=4 && buf[0]==ADDR) {
          if(buf[1]==0x10) {
            if(_st==-1 && buf.Length==26) {
              _pt=DateTime.Now;
              _st=0;
              _ac1=(short)((buf[4]<<8) | buf[5]);
              _ac2=(short)((buf[6]<<8) | buf[7]);
              _ac3=(short)((buf[8]<<8) | buf[9]);
              _ac4=(ushort)((buf[10]<<8) | buf[11]);
              _ac5=(ushort)((buf[12]<<8) | buf[13]);
              _ac6=(ushort)((buf[14]<<8) | buf[15]);
              _b1=(short)((buf[16]<<8) | buf[17]);
              _b2=(short)((buf[18]<<8) | buf[19]);
              _mb=(short)((buf[20]<<8) | buf[21]);
              _mc=(short)((buf[22]<<8) | buf[23]);
              _md=(short)((buf[24]<<8) | buf[25]);
            } else if(_st==2 && buf.Length==6) {
              _ut=(buf[4]<<8) | buf[5];
              _st=3;
            } else if(_st==5 && buf.Length==7) {
              // Calculate temperature
              int x1 = (((int)_ut - _ac6) * _ac5) >> 15;
              int x2 = (_mc << 11) / (x1 + _md);
              int b5 = x1 + x2;
              _T.value=Math.Round((b5 + 8)/160.0, 2);

              int up=((buf[4]<<16) | (buf[5]<<8) | buf[6])>>(8-BMP180_OSS);
              int b6 = b5 - 4000;
              //  calculate B3
              x1 = (b6 * b6)>>12;
              x1 *= _b2;
              x1 >>= 11;
              x2 = _ac2 * b6;
              x2 >>= 11;
              int x3 = x1 + x2;
              int b3 = (((_ac1 * 4 + x3) << BMP180_OSS) + 2) >> 2;
              // calculate B4
              x1 = (_ac3 * b6) >> 13;
              x2 = (_b1 * ((b6*b6) >> 12)) >> 16;
              x3 = ((x1 + x2) + 2) >> 2;
              uint b4 = (_ac4 * (uint)(x3 + 32768)) >> 15;
              uint b7 = ((uint)(up - b3) * (50000>>BMP180_OSS));
              int p;
              if(b7 < 0x80000000) {
                p = (int)(b7*2/b4);
              } else {
                p = (int)(b7 / b4)*2;
              }
              x1 = (p >> 8);
              x1 *= x1;
              x1 = (x1 * 3038) >> 16;
              x2 = (p * -7357) >> 16;
              p += (x1 + x2 + 3791) >> 4;
              _P.value=p;

              _present.value=true;
              _pt=DateTime.Now.AddSeconds(_rand.Next(90, 120));
              _st=0;
            } else {
              _present.value=false;
              Reset();
            }
          } else {
            _present.value=false;
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _T.path, (AckFlags)buf[1]);
            }
            _pt=DateTime.Now.AddSeconds(_rand.Next(15, 30));
            _st=0;
          }
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        buf=null;
        bool busy=false;
        if(_pt<DateTime.Now) {
          if(_st==-2) {
            buf=new byte[] { ADDR, 0x03, 0x01, 0x16, 0xAA };
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=-1;
            busy=true;
          } else if(_st==0) {
            buf=new byte[] { ADDR, 0x01, 0x02, 0x00, 0xF4, 0x2E };
            _pt=DateTime.Now.AddMilliseconds(15);
            _st=1;
            busy=true;
          } else if(_st==1) {
            buf=new byte[] { ADDR, 0x03, 0x01, 0x02, 0xF6 };
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=2;
            busy=true;
          } else if(_st==3) {
            buf=new byte[] { ADDR, 0x01, 0x02, 0x00, 0xF4, (byte)(0x34 | (BMP180_OSS<<6)) };
            _pt=DateTime.Now.AddMilliseconds(30);
            _st=4;
            busy=true;
          } else if(_st==4) {
            buf=new byte[] { ADDR, 0x03, 0x01, 0x03, 0xF6 };
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=5;
            busy=true;
          } else {
            busy=true;
            //_present.value=false;
            //if(_verbose) {
            //  Log.Warning("{0}.poll({1}) - timeot", _T, _st);
            //}
            //_pt=DateTime.Now.AddSeconds(_rand.Next(150, 210));
            //_st=-2;
          }
        } else {
          busy=_st!=0;
        }
        return busy;
      }
      public override void Reset() {
        _st=-2;
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(800, 2000));
      }
    }
    private class Blinky : TWICommon {
      private byte _addr;
      private DVar<long> _RGB;
      private DVar<long> _fade;
      private int _st;

      public Blinky(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        if(pin.name.StartsWith("BLINKM_RGB")) {
          _RGB=pin as DVar<long>;
          if(_RGB==null) {
            throw new ArgumentException();
          }
          _addr=byte.Parse(pin.name.Substring(10));
          _fade=_RGB.parent.Get<long>(string.Format("BLINKM_F{0}", _addr));
        } else if(pin.name.StartsWith("BLINKM_F")) {
          _fade=pin as DVar<long>;
          if(_fade==null) {
            throw new ArgumentException();
          }
          _addr=byte.Parse(pin.name.Substring(8));
          _RGB=_fade.parent.Get<long>(string.Format("BLINKM_RGB{0}", _addr));
        } else {
          throw new ArgumentException();
        }
        //_present=_RGB.Get<bool>("present");
        //_present.saved=false;
        //_present.value=false;
        Reset();
      }
      public override bool VarChanged(Topic snd, bool delete) {
        if(snd==_RGB) {
          if(delete && _fade!=null) {
            _fade.Remove();
          } else {
            _st|=1;
          }
          return true;
        } else if(snd==_fade) {
          if(delete && _RGB!=null) {
            _RGB.Remove();
          } else {
            _st|=2;
          }
          return true;
        } else {
          return false;
        }
      }
      public override bool Recv(byte[] buf) {
        if(buf.Length>=4 && buf[0]==_addr) {
          if(buf[1]!=0x10) {
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _RGB.path, (AckFlags)buf[1]);
            }
          }
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        buf=null;
        bool busy=false;
        if((_st & 2)!=0) {
          _st&=1;
          buf=new byte[] { _addr, 0x01, 0x02, 0x00, 0x66, (byte)(_fade.value) };
          busy=true;
        } else if((_st & 1)!=0) {
          _st&=2;
          buf=new byte[] { _addr, 0x01, 0x04, 0x00, 0x63, (byte)(_RGB.value>>16), (byte)(_RGB.value >> 8), (byte)(_RGB.value) };
          busy=true;
        }
        return busy;
      }
      public override void Reset() {
        _st=3;
      }
    }
  }
}
