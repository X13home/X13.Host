﻿#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections;
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
    private List<TWICommon> _drivers;
    private int _pollIdx;

    public TWIDriver() {
      _drivers=new List<TWICommon>();
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
        for(int i=_drivers.Count-1; i>=0; i--) {
          _drivers[i].Reset();
        }
      }
    }

    public void Recv(byte[] msgData) {
      if(msgData==null || msgData.Length<4) {
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
      if(_dev!=null) {
        _dev.Pool+=_dev_Pool;
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
            _dev.Pool+=_dev_Pool;
            Reset();
          }
        }
      }
    }
    private void _dev_Pool() {
      try {
        if(_dev==null || _owner==null) {
          return;
        }
        if(_dev.state!=MsDevice.State.Connected && _dev.state!=MsDevice.State.AWake && _dev.state!=MsDevice.State.ASleep) {
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
      case "SI7020_T":
      case "SI7020_H":
        drv=new SI7020(snd);
        break;
      case "BMP180_T":
      case "BMP180_P":
        drv=new BMP180(snd);
        break;
      case "BME280_T":
      case "BME280_P":
      case "BME280_H":
        drv=new BME280(snd);
        break;
      case "BLINKM_8":
      case "BLINKM_9":
      case "BLINKM_10":
        drv=new Blinky(snd);
        break;
      case "BH1750_0":
      case "BH1750_1":
        drv=new BH1750(snd);
        break;
      case "EXP_0":
      case "EXP_1":
      case "EXP_2":
      case "EXP_3":
      case "EXP_4":
      case "EXP_5":
      case "EXP_6":
      case "EXP_7":
        drv=new Expander(snd);
        break;
      //case "SI1143":
      //  drv=new SI1143(snd);
      //  break;
      default:
        if(snd.name.Length>2 && snd.name.Length<6 && (snd.name.StartsWith("Sa") || snd.name.StartsWith("Ra"))) {
          drv=new RawDevice(snd);
        }
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
    [Flags]
    private enum AckFlags : byte {
      WRITE=0x01,         // Write access
      READ=0x02,          // Read access
      WD_ARMED=0x04,      // Watchdog started
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
    private class RawDevice : TWICommon {
      private byte _addr;
      private bool _snd;
      private DVar<X13.PLC.ByteArray> _sa;
      private DVar<X13.PLC.ByteArray> _ra;

      public RawDevice(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        if(pin.name.Length<3 || !byte.TryParse(pin.name.Substring(2), out _addr) || _addr==0 || _addr>127) {
          pin.Remove();
          throw new ArgumentException("bad pin name: "+pin.name);
        }
        if(pin.name.StartsWith("Sa")) {
          _sa=pin as DVar<X13.PLC.ByteArray>;
          if(_sa==null) {
            throw new ArgumentException();
          }
          _ra=_sa.parent.Get<X13.PLC.ByteArray>(string.Format("Ra{0}", _addr));
        } else if(pin.name.StartsWith("Ra")) {
          _ra=pin as DVar<X13.PLC.ByteArray>;
          if(_ra==null) {
            throw new ArgumentException();
          }
          _sa=_ra.parent.Get<X13.PLC.ByteArray>(string.Format("Sa{0}", _addr));
        } else {
          throw new ArgumentException();
        }
        Reset();
      }
      public override bool VarChanged(Topic snd, bool delete) {
        if(snd==_sa) {
          if(delete) {
            if(_ra!=null) {
              _ra.Remove(_ra.parent);
            }
          } else {
            _snd=true;
          }
          return true;
        } else if(snd==_ra) {
          if(delete && _sa!=null) {
            _sa.Remove(_sa.parent);
          }
          return true;
        }
        return false;
      }

      public override bool Recv(byte[] buf) {
        if(buf[0]==_addr) {
          if(_ra!=null) {
            _ra.value=new PLC.ByteArray(buf.Skip(1).ToArray());
          }
          return true;
        }
        return false;
      }

      public override bool Poll(out byte[] buf) {
        if(_snd) {
          _snd=false;
          if(_sa.value!=null) {
            var tmp=_sa.value.GetBytes();
            if(tmp!=null && tmp.Length>2) {
              buf=new byte[tmp.Length+1];
              buf[0]=_addr;
              Buffer.BlockCopy(tmp, 0, buf, 1, tmp.Length);
              return true;
            }
          }
        }
        buf=null;
        return false;
      }

      public override void Reset() {
        _snd=false;
      }
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
        if(buf[0]==addr) {
          if(buf[1]==0x10 && buf.Length==6) {
            _T.value=(short)((buf[4]<<8) | buf[5])/256.0;
            _present.value=true;
          } else {
            _present.value=false;
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _T.path, (AckFlags)buf[1]);
            }
          }
          _pt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
          _busy=false;
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        if(_busy) {
          buf=null;
          if(_pt<DateTime.Now) {    // Timeout
            _present.value=false;
            _pt=DateTime.Now.AddSeconds(_rand.Next(135, 165));
            _busy=false;
          }
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
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(2800, 4000));
        _present.value=false;
      }
    }
    private class CC2D : TWICommon {
      private const byte ADDR=0x28;
      private DVar<double> _T;
      private DVar<double> _H;
      private DateTime _pt;
      private DVar<bool> _present;
      private int _st;
      private int _tCnt;

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
        if(buf[0]==ADDR) {
          if(buf[1]==0x10) {
            if(buf.Length==8) {
              if((buf[4] & 0xC0)==0) {
                _T.value=Math.Round(((buf[6]<<6) | (buf[7]>>2))*165.0/16384-40, 2);
                double tmp=Math.Round(((buf[4]<<8) | buf[5])*25.0/4096, 1);
                if(tmp<=100) {
                  _H.value=tmp;
                }
                _present.value=true;
                _pt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
                _st=0;
                _tCnt=0;
              } else {
                if(_tCnt++<3) {
                  _pt=DateTime.Now.AddMilliseconds(15);
                  _st=1;
                } else {
                  _pt=DateTime.Now.AddSeconds(_rand.Next(15, 30));
                  _st=0;
                }
              }
            }
          } else {
            _present.value=false;
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _T.path, (AckFlags)buf[1]);
            }
            _tCnt++;
            _pt=DateTime.Now.AddSeconds(1+_rand.Next(15, 30)*_tCnt);
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
            _pt=DateTime.Now.AddMilliseconds(15);
            _st=1;
            busy=true;
          } else if(_st==1) {
            buf=new byte[] { ADDR, 0x02, 0x00, 0x04 }; // Read 4 bytes
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=3;
            busy=true;
          } else {
            busy=true;
          }
        } else {
          busy=_st>0;
        }
        return busy;
      }
      public override void Reset() {
        _st=0;
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(2800, 4000));
        _present.value=false;
      }
    }
    private class HIH61xx : TWICommon {
      private const byte ADDR=0x27;
      private DVar<double> _T;
      private DVar<double> _H;
      private DateTime _pt;
      private DVar<bool> _present;
      private int _st;
      private int _tCnt;

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
        if(buf[0]==ADDR) {
          if(buf[1]==0x10) {
            if(buf.Length==8) {
              if((buf[4] & 0xC0)==0) {
                _T.value=Math.Round((((buf[6]<<6) | (buf[7]>>2)) & 0x3FFF) *55.0/5461-40, 2);
                _H.value=Math.Round(((buf[4]<<2) | (buf[5] >> 6))*20.0/51, 1);
                _present.value=true;
                _pt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
                _st=0;
                _tCnt=0;
              } else {
                if(_tCnt++<3) {
                  _pt=DateTime.Now.AddMilliseconds(15);
                  _st=1;
                } else {
                  _pt=DateTime.Now.AddSeconds(_rand.Next(15, 30));
                  _st=0;
                }
              }
            }
          } else {
            _present.value=false;
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _T.path, (AckFlags)buf[1]);
            }
            _tCnt++;
            _pt=DateTime.Now.AddSeconds(1+_rand.Next(15, 30)*_tCnt);
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
            _pt=DateTime.Now.AddMilliseconds(15);
            _st=1;
            busy=true;
          } else if(_st==1) {
            buf=new byte[] { ADDR, 0x02, 0x00, 0x04 }; // Read 4 bytes
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=3;
            busy=true;
          } else {
            busy=true;
          }
        } else {
          busy=_st>0;
        }
        return busy;
      }
      public override void Reset() {
        _st=0;
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(2800, 4000));
        _present.value=false;
      }
    }
    private class SI7020 : TWICommon {
      private const byte ADDR=0x40;
      private DVar<double> _T;
      private DVar<double> _H;
      private DateTime _pt;
      private DVar<bool> _present;
      private int _st;

      public SI7020(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        if(pin.name=="SI7020_T") {
          _T=pin as DVar<double>;
          if(_T==null) {
            throw new ArgumentException();
          }
          _H=_T.parent.Get<double>("SI7020_H");
        } else if(pin.name=="SI7020_H") {
          _H=pin as DVar<double>;
          if(_H==null) {
            throw new ArgumentException();
          }
          _T=_H.parent.Get<double>("SI7020_T");
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
        if(buf[0]==ADDR) {
          if(buf[1]==0x10 && buf.Length==6) {
            if(_st==2) {
              _H.value=Math.Round(((buf[4]<<8) | (buf[5]))*125.0/65536-6, 1);
              _present.value=true;
              _pt=DateTime.Now;
              _st=3;
            } else if(_st==5) {
              _T.value=Math.Round(((buf[4]<<8) | (buf[5]))*175.72/65536-46.85, 2);
              _present.value=true;
              _pt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
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
            _pt=DateTime.Now.AddSeconds(_rand.Next(135, 165));
            _st=0;
          }
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        buf=null;
        var now=DateTime.Now;
        if(_pt<now) {
          if(_st==0) {
            buf=new byte[] { ADDR, 0x01, 0x01, 0x02, 0xE5 }; // Trigger RH measurement hold master
            _pt=now.AddMilliseconds(500);
            _st=2;
          } else if(_st==3) {
            buf=new byte[] { ADDR, 0x01, 0x01, 0x02, 0xE3 }; // Trigger T measurement, hold master
            _pt=now.AddMilliseconds(15);
            _st=5;
          }
          return true;
        } 
        return _st>0;
      }
      /*
      public override bool Recv(byte[] buf) {
        if(buf[0]==ADDR) {
          if(buf[1]==0x10 && buf.Length==6) {
            if(_st==2) {
              _H.value=Math.Round(((buf[4]<<8) | (buf[5]))*125.0/65536-6, 1);
              _present.value=true;
              _pt=DateTime.Now;
              _st=3;
            } else if(_st==5) {
              _T.value=Math.Round(((buf[4]<<8) | (buf[5]))*175.72/65536-46.85, 2);
              _present.value=true;
              _pt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
              _st=0;
            } else {
              _present.value=false;
              Reset();
            }
          } else if((buf[1] & 0x40)==0x40) {    // Nack received
            if(_st==2 || _st==5) {
              _pt=DateTime.Now.AddMilliseconds(80);
              _st--;
            } else {
              _present.value=false;
              Reset();
            }
          } else {
            _present.value=false;
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _T.path, (AckFlags)buf[1]);
            }
            _pt=DateTime.Now.AddSeconds(_rand.Next(135, 165));
            _st=0;
          }
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        buf=null;
        var now=DateTime.Now;
        if(_pt<now) {
          if(_st==0) {
            buf=new byte[] { ADDR, 0x01, 0x01, 0x00, 0xF5 }; // Trigger RH measurement no hold master
            _pt=now.AddMilliseconds(15);
            _st=1;
          } else if(_st==1) {
            buf=new byte[] { ADDR, 0x02, 0x00, 0x02 }; // Read 2 bytes
            _pt=now.AddMilliseconds(500);
            _st=2;
          } else if(_st==3) {
            buf=new byte[] { ADDR, 0x01, 0x01, 0x00, 0xF3 }; // Trigger T measurement, no hold master
            _pt=now.AddMilliseconds(15);
            _st=4;
          } else if(_st==4) {
            buf=new byte[] { ADDR, 0x02, 0x00, 0x02 }; // Read 2 bytes
            _pt=now.AddMilliseconds(500);
            _st=5;
          }
          return true;
        }
        return _st>0;
      }*/
      public override void Reset() {
        _st=0;
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(2800, 4000));
        _present.value=false;
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
        if(buf[0]==ADDR) {
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
            } else if((_st==2 || _st==3) && buf.Length==6) {
              _ut=(buf[4]<<8) | buf[5];
              _pt=DateTime.Now.AddMilliseconds(-1);
              _st++;
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
              _pt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
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
            _pt=DateTime.Now.AddSeconds(_rand.Next(135, 165));
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
            _pt=DateTime.Now.AddMilliseconds(1);
            _st=2;
            busy=true;
          } else if(_st==2 || _st==3) {
            buf=new byte[] { ADDR, 0x01, 0x02, 0x00, 0xF4, (byte)(0x34 | (BMP180_OSS<<6)) };
            _pt=DateTime.Now.AddMilliseconds(500);  // Wait ack 
            _st++;
            busy=true;
          } else if(_st==4) {
            buf=new byte[] { ADDR, 0x03, 0x01, 0x03, 0xF6 };
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=5;
            busy=true;
          } else {
            busy=true;
          }
        } else {
          busy=_st!=0;
        }
        return busy;
      }
      public override void Reset() {
        _st=-2;
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(2800, 4000));
        _present.value=false;
      }
    }
    private class BME280 : TWICommon {
      private const byte ADDR=0x76;
      private DVar<double> _T;
      private DVar<double> _H;
      private DVar<long> _P;
      private DateTime _pt;
      private DVar<bool> _present;
      private int _st;

      private UInt16 _dig_T1;
      private Int16  _dig_T2;
      private Int16  _dig_T3;

      private UInt16 _dig_P1;
      private Int16  _dig_P2;
      private Int16  _dig_P3;
      private Int16  _dig_P4;
      private Int16  _dig_P5;
      private Int16  _dig_P6;
      private Int16  _dig_P7;
      private Int16  _dig_P8;
      private Int16  _dig_P9;

      private byte   _dig_H1;
      private Int16  _dig_H2;
      private byte   _dig_H3;
      private Int16  _dig_H4;
      private Int16  _dig_H5;
      private sbyte   _dig_H6;

      public BME280(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        if(pin.name=="BME280_T") {
          _T=pin as DVar<double>;
          if(_T==null) {
            throw new ArgumentException();
          }
          _P=_T.parent.Get<long>("BME280_P");
          _H=_T.parent.Get<double>("BME280_H");
        } else if(pin.name=="BME280_P") {
          _P=pin as DVar<long>;
          if(_P==null) {
            throw new ArgumentException();
          }
          _T=_P.parent.Get<double>("BME280_T");
          _H=_P.parent.Get<double>("BME280_H");
        } else if(pin.name=="BME280_H") {
          _H=pin as DVar<double>;
          if(_H==null) {
            throw new ArgumentException();
          }
          _P=_H.parent.Get<long>("BME280_P");
          _T=_H.parent.Get<double>("BME280_T");
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
          if(delete && _H!=null) {
            _H.Remove();
          }
          return true;
        } else if(snd==_P) {
          if(delete && _T!=null) {
            _T.Remove();
          }
          if(delete && _H!=null) {
            _H.Remove();
          }
          return true;
        } else if(snd==_H) {
          if(delete && _T!=null) {
            _T.Remove();
          }
          if(delete && _P!=null) {
            _P.Remove();
          }
          return true;
        }
        return false;
      }
      public override bool Recv(byte[] buf) {
        //int oldSt=_st;
        if(buf[0]==ADDR) {
          if(buf[1]==0x10) {
            if(_st==-3 && buf.Length==30) {
              _dig_T1=(ushort)(buf[4] | (buf[5]<<8));
              _dig_T2=(short)(buf[6] | (buf[7]<<8));
              _dig_T3=(short)(buf[8] | (buf[9]<<8));
              _dig_P1=(ushort)(buf[10] | (buf[11]<<8));
              _dig_P2=(short)(buf[12] | (buf[13]<<8));
              _dig_P3=(short)(buf[14] | (buf[15]<<8));
              _dig_P4=(short)(buf[16] | (buf[17]<<8));
              _dig_P5=(short)(buf[18] | (buf[19]<<8));
              _dig_P6=(short)(buf[20] | (buf[21]<<8));
              _dig_P7=(short)(buf[22] | (buf[23]<<8));
              _dig_P8=(short)(buf[24] | (buf[25]<<8));
              _dig_P9=(short)(buf[26] | (buf[27]<<8));
              _dig_H1=buf[29];
              _pt=DateTime.Now;
              _st=-2;
            } else if(_st==-1 && buf.Length==11) {
              _dig_H2=(short)(buf[4] | (buf[5]<<8));
              _dig_H3=buf[6];
              _dig_H4=(short)((buf[7] << 4) | (buf[8] & 0x0F));
              _dig_H5=(short)((buf[8] >> 4) | (buf[9]<<4));
              _dig_H6=(sbyte)buf[10];
              _pt=DateTime.Now;
              _st=0;
            } else if(_st==3 && buf.Length==12) {
              uint adc_H = (uint)(buf[11] | (buf[10] << 8));
              uint adc_T = (uint)((buf[9]>>4) | (buf[8]<<4) | (buf[7]<<12));
              uint adc_P=(uint)((buf[6]>>4) | (buf[5]<<4) | (buf[4]<<12));

              double  var1, var2, t_fine;

              var1=(adc_T/16384.0 - _dig_T1/1024.0) * _dig_T2;
              var2=((adc_T/131072.0 - _dig_T1/8192.0) * (adc_T/131072.0 - _dig_T1/8192.0)) * _dig_T3;
              t_fine = (int)(var1 + var2);
              _T.value=Math.Round((var1 + var2) / 5120.0, 2);

              var1 = t_fine/2.0 - 64000.0;
              var2 = var1 * var1 * _dig_P6 / 32768.0;
              var2 = var2 + var1 * _dig_P5 * 2.0;
              var2 = var2/4.0+_dig_P4 * 65536.0;
              var1 = (_dig_P3 * var1 * var1 / 524288.0 + _dig_P2 * var1) / 524288.0;
              var1 = (1.0 + var1 / 32768.0)*_dig_P1;
              if(var1 != 0.0) {
                double p = 1048576.0 - (double)adc_P;
                p = (p - (var2 / 4096.0)) * 6250.0 / var1;
                var1 = _dig_P9 * p * p / 2147483648.0;
                var2 = p * _dig_P8 / 32768.0;
                p = p + (var1 + var2 + _dig_P7) / 16.0;
                _P.value=(long)p;
              }

              var1 = t_fine - 76800.0;
              var1 = (adc_H - (_dig_H4 * 64.0 + _dig_H5 / 16384.0 * var1)) * (_dig_H2 / 65536.0 * (1.0 + _dig_H6 / 67108864.0 * var1 * (1.0 + _dig_H3 / 67108864.0 * var1)));
              var1 = var1 * (1.0 - _dig_H1 * var1 / 524288.0);
              if(var1 > 100.0)
                var1 = 100.0;
              else if(var1 < 0.0)
                var1 = 0.0;
              _H.value=Math.Round(var1, 1);


              //Log.Debug("{0} T={1}, H={2}, P={3}", _T.parent.path, _T.value, _H.value, _P.value);
              _present.value=true;
              _pt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
              _st=1;
            } else {
              _present.value=false;
              Reset();
            }
            //if(oldSt!=_st) {
            //  Log.Debug("{0} st={1}", _T.parent.path, _st);
            //}
          } else {
            _present.value=false;
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _T.path, (AckFlags)buf[1]);
            }
            _pt=DateTime.Now.AddSeconds(_rand.Next(135, 165));
            _st=-4;
          }
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        //int oldSt=_st;
        buf=null;
        bool busy=false;
        if(_pt<DateTime.Now) {
          if(_st==-4) {
            _st=-3;
            buf=new byte[] { ADDR, 0x03, 0x01, 0x1A, 0x88 };
            _pt=DateTime.Now.AddMilliseconds(500);
            busy=true;
          } else if(_st==-2) {
            _st=-1;
            buf=new byte[] { ADDR, 0x03, 0x01, 0x07, 0xE1 };
            _pt=DateTime.Now.AddMilliseconds(500);
            busy=true;
          } else if(_st==0) {
            buf=new byte[] { ADDR, 0x01, 0x04, 0x00, 0xF2, 0x01, 0x00, 0x24 };  // Pressure oversampling x1, Temperature oversampling x1, Humidity oversampling x1
            _pt=DateTime.Now.AddMilliseconds(30);
            _st=1;
            busy=true;
          } else if(_st==1) {
            buf=new byte[] { ADDR, 0x01, 0x02, 0x00, 0xF4, 0x25 };  // forced mode
            _pt=DateTime.Now.AddMilliseconds(10);
            _st=2;
            busy=true;
          } else if(_st==2) {
            buf=new byte[] { ADDR, 0x03, 0x01, 0x08, 0xF7 };
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=3;
            busy=true;
          } else {
            busy=true;
          }
          //if(oldSt!=_st) {
          //  Log.Debug("{0} st={1}, busy={2}", _T.parent.path, _st, busy);
          //}
        } else {
          busy=_st!=1;
        }
        return busy;
      }
      public override void Reset() {
        _st=-4;
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(2800, 4000));
        _present.value=false;
      }
    }
    private class BH1750 : TWICommon {
      private byte ADDR;
      private DVar<long> _val;
      private DVar<bool> _present;
      private int _st;
      private DateTime _pt;

      public BH1750(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        if(pin.name=="BH1750_0") {
          _val=pin as DVar<long>;
          ADDR=0x23;
        } else if(pin.name=="BH1750_1") {
          _val=pin as DVar<long>;
          ADDR=0x5C;
        } else {
          throw new ArgumentException();
        }
        _present=_val.Get<bool>("present");
        _present.saved=false;
        _present.value=false;
        Reset();
      }
      public override bool VarChanged(Topic snd, bool delete) {
        if(snd==_val) {
          return true;
        }
        return false;
      }
      public override bool Recv(byte[] buf) {
        if(buf[0]==ADDR) {
          if(buf[1]==0x10 && _st==2 && buf.Length==6) {
            _val.value=(long)(((buf[4]<<8) | buf[5])/1.2);
            _present.value=true;
            _pt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
            _st=0;
          } else {
            _present.value=false;
            _pt=DateTime.Now.AddSeconds(_rand.Next(135, 165));
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
            buf=new byte[] { ADDR, 0x01, 0x01, 0x00, 0x23 };  // One time L-resolution mode
            _pt=DateTime.Now.AddMilliseconds(20);
            _st=1;
            busy=true;
          } else if(_st==1) {
            buf=new byte[] { ADDR, 0x02, 0x00, 0x02 };  // Read measurement result. 
            _pt=DateTime.Now.AddMilliseconds(500);
            _st=2;
            busy=true;
          } else {
            Reset();
          }
        } else {
          busy=_st!=0;
        }
        return busy;
      }
      public override void Reset() {
        _pt=DateTime.Now.AddSeconds(_rand.Next(15, 30));
        _st=0;
        _present.value=false;
      }
    }
    private class Blinky : TWICommon {
      private byte _addr;
      private Topic _owner;
      private DVar<long> _R;
      private DVar<long> _G;
      private DVar<long> _B;
      private DVar<long> _fade;
      private DVar<bool> _present;
      private int _st;
      private DateTime _rt;  // Wait TWI error timer
      private bool _waitError;

      public Blinky(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        if(pin.name.StartsWith("BLINKM_") && byte.TryParse(pin.name.Substring(7), out _addr) && _addr>7) {
          _owner=pin;
        } else {
          throw new ArgumentException();
        }

        var dc=_owner.Get<string>("_declarer", _owner);
        dc.saved=true;
        dc.value="TWI_BLINKM";
        _R=_owner.Get<long>("Red");
        _G=_owner.Get<long>("Green");
        _B=_owner.Get<long>("Blue");
        _fade=_owner.Get<long>("Fade");
        _present=_owner.Get<bool>("present");
        _present.saved=false;
        _present.value=false;
        _owner.Subscribe("+", PinChanged);
        Reset();
      }
      public override bool VarChanged(Topic snd, bool delete) {
        if(snd==_owner) {
          if(delete && _owner!=null) {
            _owner.Unsubscribe("+", PinChanged);
          }
          return true;
        }
        return false;
      }
      private void PinChanged(Topic snd, TopicChanged tc) {
        if(snd==_R || snd==_B || snd==_G) {
          _st|=1;
        } else if(snd==_fade) {
          _st|=2;
        }
      }
      public override bool Recv(byte[] buf) {
        if(buf[0]==_addr) {
          if(buf[1]!=0x10) {
            if(TWIDriver._verbose) {
              Log.Error("{0}.recv - {1}", _owner.path, (AckFlags)buf[1]);
            }
            _st=3;
            _present.value=false;
            _rt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
          }
          _waitError=false;
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        buf=null;
        bool busy=false;
        var now=DateTime.Now;
        if(_waitError && _rt<now) {
          _present.value=true;
          _waitError=false;
        }
        if(_present.value || _rt<now) {
          if((_st & 2)!=0) {
            _st&=1;
            buf=new byte[] { _addr, 0x01, 0x02, 0x00, 0x66, (byte)(_fade.value) };
            busy=true;
            if(!_waitError) {
              _rt=now.AddMilliseconds(500);
              _waitError=true;
            }
          } else if((_st & 1)!=0) {
            _st&=2;
            buf=new byte[] { _addr, 0x01, 0x04, 0x00, 0x63, (byte)(_R.value), (byte)(_G.value), (byte)(_B.value) };
            busy=true;
            if(!_waitError) {
              _rt=now.AddMilliseconds(500);
              _waitError=true;
            }
          }
        }
        return busy;
      }
      public override void Reset() {
        _st=3;
        _present.value=false;
        _waitError=false;
      }
    }
    private class Expander : TWICommon {
      private byte ADDR;
      private Topic _mnt;
      private DVar<bool>[] _pins;
      private ushort _gpo, _ipol, _iodir, _inps;
      private int _flags;       // 1 - _gpo, 2 - _ipol, 4 - _iodir
      private bool _busy, _waitResp, _waitError;
      private DateTime _pt;
      private DateTime _rt;  // Wait TWI error timer

      public Expander(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        if(pin.name.Length==5 && pin.name.StartsWith("EXP_") && pin.name[4]>='0' && pin.name[4]<='7') {
          _mnt=pin;
          ADDR=(byte)(0x20 | (byte)(pin.name[4]-'0'));
        } else {
          throw new ArgumentException();
        }

        var dc=_mnt.Get<string>("_declarer", _mnt);
        dc.saved=true;
        dc.value="TWI_Expander";
        _pins=new DVar<bool>[18]; // 16 - present, 17 - IRQ

        _pins[16]=_mnt.Get<bool>("present");
        _pins[16].saved=false;
        _pins[16].value=false;
        _iodir=0xFFFF;
        _mnt.Subscribe("+", PinChanged);
        Reset();
      }
      public override bool VarChanged(Topic snd, bool delete) {
        if(snd==_mnt) {
          if(delete && _mnt!=null) {
            _mnt.Unsubscribe("+", PinChanged);
          }
          return true;
        }
        return false;
      }
      public override bool Recv(byte[] buf) {
        if(buf[0]==ADDR) {
          if(buf[1]==0x10 && buf.Length==6) {
            ushort n_gpi=(ushort)(buf[4] | (buf[5]<<8));
            for(int i=0; i<16; i++) {
              if(_pins[i]!=null && _pins[i].name[0]=='I') {
                ushort mask=(ushort)(1<<i);
                _pins[i].value=((n_gpi & mask)!=0);
              }
            }
            _pins[16].value=true;
            _pt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
            _busy=false;
          } else {
            _pins[16].value=false;
            _pt=DateTime.Now.AddSeconds(_rand.Next(135, 165));
            _busy=false;
          }
          _waitError=false;
          _waitResp=false;
          return true;
        }
        return false;
      }
      public override bool Poll(out byte[] buf) {
        buf=null;
        var now=DateTime.Now;
        if(_waitError && _rt<now) {
          _pins[16].value=true;
          _waitError=false;
        }
        if(_waitResp) {
          if(_pt<now) {  // read timeout
            Reset();
            _busy=false;
          }
          return true;
        } else if(_pt<now && (_pins[16].value || _rt<now)) {
          if((_flags & 4)!=0) {
            _flags&=3;
            buf=new byte[] { ADDR, 0x01, 0x03, 0x00, 0x06, (byte)_iodir, (byte)(_iodir>>8) };  // Access to IODIR
            _pt=now.AddMilliseconds(30);
            _busy=true;
            if(!_waitError) {
              _rt=now.AddMilliseconds(500);
              _waitError=true;
            }
          } else if((_flags & 2)!=0) {
            _flags&=5;
            buf=new byte[] { ADDR, 0x01, 0x03, 0x00, 0x04, (byte)_ipol, (byte)(_ipol>>8) };  // Access to IPOL
            _pt=now.AddMilliseconds(30);
            _busy=true;
            if(!_waitError) {
              _rt=now.AddMilliseconds(500);
              _waitError=true;
            }
          } else if((_flags & 1)!=0) {
            _flags&=6;
            buf=new byte[] { ADDR, 0x01, 0x03, 0x00, 0x00, (byte)_gpo, (byte)(_gpo>>8) };  // Access to GP
            _pt=now.AddMilliseconds(30);
            _busy=true;
            if(!_waitError) {
              _rt=now.AddMilliseconds(500);
              _waitError=true;
            }
          } else if(_inps!=0) {
            buf=new byte[] { ADDR, 0x03, 0x01, 0x02, 0x00 };  // Access to GP
            _pt=DateTime.Now.AddMilliseconds(500);
            _busy=true;
            _waitResp=true;
          } else {
            _busy=false;
            _pt=DateTime.Now.AddSeconds(_rand.Next(45, 75));
          }
        }
        return _busy;
      }
      public override void Reset() {
        _flags=7;
        _busy=false;
        _waitResp=false;
        _waitError=false;
        _pt=DateTime.Now.AddMilliseconds(_rand.Next(850, 1500));
        _pins[16].value=false;
      }
      private void PinChanged(Topic src, TopicChanged tc) {
        DVar<bool> pin=src as DVar<bool>;
        int idx=-1;
        if(pin==null || tc.Initiator==_mnt) {
          return;
        }
        if(tc.Art==TopicChanged.ChangeArt.Remove) {
          for(idx=0; idx<_pins.Length; idx++) {
            if(_pins[idx]==pin) {
              _pins[idx]=null;
              ushort n_ipol=_ipol, n_iodir=_iodir;
              n_iodir&=(ushort)(~(1<<idx));
              _inps&=(ushort)(~(1<<idx));
              if(_iodir!=n_iodir) {
                _iodir=n_iodir;
                _flags|=4;
              }
              n_ipol&=(ushort)(~(1<<idx));
              if(_ipol!=n_ipol) {
                _ipol=n_ipol;
                _flags|=2;
              }
              break;
            }
          }
        } else {
          for(int i=0; i<_pins.Length; i++) {
            if(_pins[i]==pin) {
              idx=i;
              break;
            }
          }
          if(idx==-1) {
            ushort n_ipol=_ipol, n_iodir=_iodir;
            if(pin.name.Length==3 
              && (pin.name[0]=='I' || pin.name[0]=='O') 
              && (pin.name[1]=='p' || pin.name[1]=='n')
              && ((pin.name[2]>='0' && pin.name[2]<='9') || (pin.name[2]>='A' && pin.name[2]<='F'))) {
              idx=pin.name[2]-'0';
              if(idx>9) {
                idx-=7;
              }
              if(pin.name[0]=='I') {
                n_iodir|=(ushort)(1<<idx);
                _inps|=(ushort)(1<<idx);
              } else {
                n_iodir&=(ushort)(~(1<<idx));
              }
              if(_iodir!=n_iodir) {
                _iodir=n_iodir;
                _flags|=4;
              }
              if(pin.name[1]=='p') {
                n_ipol&=(ushort)(~(1<<idx));
              } else {
                n_ipol|=(ushort)(1<<idx);
              }
              if(_ipol!=n_ipol) {
                _ipol=n_ipol;
                _flags|=2;
              }
            } else if(pin.name=="IRQ") {
              if(pin.value && !_busy && _flags==0) {
                _pt=DateTime.Now.AddMilliseconds(1);
              }
              return;
            } else {
              return; // unknown variable
            }
            if(_pins[idx]!=pin) {
              if(_pins[idx]!=null) {
                _pins[idx].Remove(tc.Initiator);
              }
              _pins[idx]=pin;
            }
          }
        }
        if(pin.name[0]=='O') {
          ushort n_gpo=_gpo;
          bool val=((_ipol & (1<<idx))==0)?pin.value:!pin.value;
          if(val) {
            n_gpo|=(ushort)(1<<idx);
          } else {
            n_gpo&=(ushort)(~(1<<idx));
          }
          if(_gpo!=n_gpo) {
            _gpo=n_gpo;
            _flags|=1;
          }
        }
        if(!_busy && _flags!=0) {
          _pt=DateTime.Now.AddMilliseconds(1);
        }
      }
    }
    /*
    private class SI1143 : TWICommon {
      private DVar<long> _var;
      private byte _addr;
      private int _st;
      private DateTime _pt;

      public SI1143(Topic pin) {
        if(pin==null) {
          throw new ArgumentNullException();
        }
        _var=pin as DVar<long>;
        _addr=0x5A;
        _valueCur=new int[3];
        _valueOld=new int[3];
        _thresold=new int[3];
        _thresold[0]=400;
        _thresold[1]=400;
        _thresold[2]=400;
        _edge=new long[6];

      }
      public override bool VarChanged(Topic snd, bool delete) {
        if(snd==_var) {
          return true;
        }
        return false;
      }

      private int[] _valueCur, _valueOld, _thresold;
      private long[] _edge;
      private const long TICK_TOLERANCE=20;

      public override bool Recv(byte[] buf) {
        if(buf==null || buf.Length<4) {
          return false;
        }
        if(buf[0]!=_addr) {
          return false;
        }
        if((buf[1]&0xE0)!=0) {
          _st=-1;
          _pt=DateTime.Now.AddSeconds(120);
          return true;
        }
        if(_st==INIT_CMD_COUNTER+1 && (buf[1]&0x10)==0x10) {
          long startTick, duration, dx, dy, curTick=DateTime.Now.Ticks/100000;  // *10ms
          int i;
          Dir rez=Dir.none;

          _valueCur[0]=((buf[9]<<8) | buf[8]);
          _valueCur[1]=((buf[11]<<8) | buf[10]);
          _valueCur[2]=((buf[13]<<8) | buf[12]);
          for(i=0; i<3; i++) {
            if(_valueOld[i] < _thresold[i] && _valueCur[i]>_thresold[i]) {  // Rising Edge Detection
              if(_edge[i]==0) {
                _edge[i]=curTick;
              }
            } else if(_valueOld[i] > _thresold[i] && _valueCur[i] < _thresold[i]) {  // Falling Edge Detection
              if(_edge[i+3]==0) {
                _edge[i+3]=curTick;
              }
            }
          }

          if(_edge[0]!=0 && _edge[1]!=0 && _edge[2]!=0) {  // Check if rising edge group is ready to be processed:
            startTick=Math.Min(_edge[0], Math.Min(_edge[1], _edge[2]));
            duration=Math.Max(_edge[0], Math.Max(_edge[1], _edge[2]))-startTick;
            // Process rising edge group (this code implements the conditional event/gesture table)
            dx=_edge[1]-_edge[0];
            dy=_edge[2]-_edge[0];
            if(dx>duration/2) {
              rez|=Dir.enter_left;
            } else if(dx<-duration/2) {
              rez|=Dir.enter_right;
            }

            if(dy>duration/3) {
              rez|=Dir.enter_bottom;
            } else if(dy<-duration/3) {
              rez|=Dir.enter_top;
            }

            if((rez & Dir.enter_center)==Dir.none) {
              rez|=Dir.enter_center;
            }

            Log.Debug("SI1143 dx={0}, dy={1}, rez={2}", dx, dy, rez);
            _edge[0]=0;
            _edge[1]=0;
            _edge[2]=0;
          }

          if(_edge[3]!=0 && _edge[4]!=0 && _edge[5]!=0) {  // Check if falling edge group is ready to be processed:
            startTick=Math.Min(_edge[3], Math.Min(_edge[4], _edge[5]));
            duration=Math.Max(_edge[3], Math.Max(_edge[4], _edge[5]))-startTick;
            // Process falling edge group (this code implements the conditional event/gesture table)
            dx=_edge[4]-_edge[3];
            dy=_edge[5]-_edge[3];
            if(dx>duration/2) {
              rez|=Dir.leave_right;
            } else if(dx<-duration/2) {
              rez|=Dir.leave_left;
            }

            if(dy>duration/3) {
              rez|=Dir.leave_top;
            } else if(dy<-duration/3) {
              rez|=Dir.leave_bottom;
            }

            if((rez & Dir.leave_center)==Dir.none) {
              rez|=Dir.leave_center;
            }

            Log.Debug("SI1143 dx={0}, dy={1}, rez={2}", dx, dy, rez);
            _edge[3]=0;
            _edge[4]=0;
            _edge[5]=0;
          }

          curTick-=50;
          for(i=0; i<3; i++) {
            _thresold[i]=(_valueCur[i]*5/4+_thresold[i]*255)/256;
            _valueOld[i]=_valueCur[i];
            if(_edge[i]<curTick) {
              _edge[i]=0;
            }
            if(_edge[i+3]<curTick) {
              _edge[i+3]=0;
            }
          }
          _var.value=((long)buf[7] << 56) | ((long)buf[6] << 48) | ((long)buf[9] << 40) | ((long)buf[8] << 32) | (long)((byte)rez);
          //_var.value=((long)buf[9] << 40) | ((long)buf[8] << 32) | ((long)buf[11] << 24) | ((long)buf[10] << 16) | ((long)buf[13] << 8) | ((long)buf[12] << 0);
          //_var.value=((long)buf[7] << 56) | ((long)buf[6] << 48) | ((long)buf[9] << 40) | ((long)buf[8] << 32) | ((long)buf[11] << 24) | ((long)buf[10] << 16) | ((long)buf[13] << 8) | ((long)buf[12] << 0);
        }
        _st=INIT_CMD_COUNTER;
        _pt=DateTime.Now.AddMilliseconds(30);
        return true;
      }
      [Flags]
      private enum Dir {
        none=0x00,
        enter_left=0x01,
        enter_right=0x02,
        enter_bottom=0x04,
        enter_bottom_left=0x05,
        enter_bottom_right=0x06,
        enter_top=0x08,
        enter_top_left=0x09,
        enter_top_right=0x0A,
        enter_center=0x0F,

        leave_left=0x10,
        leave_right=0x20,
        leave_bottom=0x40,
        leave_bottom_left=0x50,
        leave_bottom_right=0x60,
        leave_top=0x80,
        leave_top_left=0x90,
        leave_top_right=0xA0,
        leave_center=0xF0,
      }


      public override bool Poll(out byte[] buf) {
        if(_st>=0 && _st<INIT_CMD_COUNTER) {
          if(DateTime.Now>_pt) {
            buf=new byte[_initSeq[_st].Length+4];
            buf[0]=_addr;
            buf[1]=1;
            buf[2]=(byte)_initSeq[_st].Length;
            buf[3]=0;
            Buffer.BlockCopy(_initSeq[_st], 0, buf, 4, _initSeq[_st].Length);
            _st++;
            _pt=DateTime.Now.AddMilliseconds(50);
            return true;
          }
          buf=null;
          return true;
        } else if(_st==INIT_CMD_COUNTER && DateTime.Now>_pt) {
          buf=new byte[] { _addr, 0x03, 0x01, 0x0C, 0x22 };
          _st=INIT_CMD_COUNTER+1;
          return true;
        }
        buf=null;
        return false;
      }
      private const int INIT_CMD_COUNTER=18;
      private static byte[][] _initSeq=new byte[][]{ 
        new byte[]{0x07, 0x17},  //HW_KEY - The system must write the value 0x17 to this register for proper Si114x operation.
        new byte[]{0x03, 0x03},  // turn on interrupts
        new byte[]{0x04, 0x10},  // turn on interrupt on PS3
        new byte[]{0x06, 0x01},  // interrupt on ps3 measurement
        new byte[]{0x08, 0x96},  // The device wakes up every 30 ms (0x03C0 x 31.25 µs)
        new byte[]{0x09, 0x32},  // ALS Measurements made every 10 times the device wakes up.
        new byte[]{0x0A, 0x08},  // PS Measurements made every time the device wakes up 
        new byte[]{0x0F, 0x55},  // LED current for LEDs 1 (red) & 2 (IR1)
        new byte[]{0x10, 0x05},  // LED current for LED 3 (IR2)
        //new byte[]{0x03, 0x03, 0x10, 0x00, 0x01, 0x17, 0x96, 0x32, 0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0x05}, 
        new byte[]{0x17, 0x77, 0xA1},  // PARAM_CH_LIST - all measurements on
        new byte[]{0x17, 0x00, 0xAB},  // PARAM_PS_ADC_GAIN - 
        new byte[]{0x17, 0x21, 0xA2},  // PARAM_PSLED12_SELECT - select LEDs on
        new byte[]{0x17, 0x04, 0xA3},  // PARAM_PSLED3_SELECT - 3 only
        new byte[]{0x17, 0x03, 0xA7},  // PARAM_PS1_ADCMUX - PS1 photodiode select
        new byte[]{0x17, 0x03, 0xA8},  // PARAM_PS2_ADCMUX - PS2 photodiode select
        new byte[]{0x17, 0x03, 0xA9},  // PARAM_PS3_ADCMUX - PS3 photodiode select
        new byte[]{0x17, 0x70, 0xAA},  // PARAM_PS_ADC_COUNTER - is default
        new byte[]{0x18, 0x0F},  // starts an autonomous read loop
      };
      public override void Reset() {
        _st=0;
      }
    }
     */
  }
}
