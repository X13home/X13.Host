#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using DalSemi.OneWire.Adapter;
using DalSemi.OneWire;
using System.Threading;

namespace X13.Periphery {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 7)]
  [ExportMetadata("name", "1Wire")]
  public class OneWirePlugin : IPlugModul {
    private Topic _dev1w;
    private List<OneWireGate> _gates;
    public void Init() {
      _gates=new List<OneWireGate>();
      Topic.root.Subscribe("/etc/1Wire/#", Dummy);
      Topic.root.Subscribe("/etc/declarers/1Wire/#", Dummy);
      Topic.root.Subscribe("/dev/1Wire/+", Dummy);
    }

    public void Start() {
      _dev1w=Topic.root.Get("/dev/1Wire");
      foreach(var pnn in new string[] { "USB0", "USB1", "USB2", "USB3", "USB4", "USB5", "USB6", "USB7" }) {
        ConnectAdapter("{DS9490}", pnn);
      }
      foreach(var pnn in System.IO.Ports.SerialPort.GetPortNames()) {
        ConnectAdapter("DS9097U", pnn);
      }
      foreach(var g in _gates) {
        g.Start();
      }
    }

    private void ConnectAdapter(string an, string pn) {
      byte[] gId=null;
      PortAdapter adapter;
      try {
        adapter=AccessProvider.GetAdapter(an, pn);
        adapter.BeginExclusive(true);
        adapter.Speed = OWSpeed.SPEED_REGULAR;
        adapter.StartPowerDelivery(OWPowerStart.CONDITION_AFTER_BYTE);
        adapter.TargetAllFamilies();
        adapter.TargetFamily(0x81);
        byte[] id = new byte[8];
        DVar<OneWireGate> tGate=null;
        if(adapter.GetFirstDevice(id, 0)) {
          do {
            if(id[0]==0x81) {
              gId=id;
              break;
            }
          } while(adapter.GetNextDevice(id, 0));
        }
        if(gId==null) {
          gId=new byte[] { 0x81, 0, 0, 0, 0, 0, 0, 0 };
          BitConverter.GetBytes((an+pn).GetHashCode()).CopyTo(gId, 1);
        }
        tGate=_dev1w.children.FirstOrDefault(z => {
          var g=z as DVar<OneWireGate>;
          return g!=null && g.value!=null && gId.SequenceEqual(g.value.rom);
        }) as DVar<OneWireGate>;
        if(tGate==null) {
          tGate=_dev1w.Get<OneWireGate>(string.Format("{0}_{1:X2}{2:X2}{3:X2}{4:X2}", adapter.AdapterName, gId[4], gId[3], gId[2], gId[1]));
          tGate.value=new OneWireGate(adapter, gId);
        } else if(tGate.value.present) {
          //Log.Debug("Adapter with ID already exist. [{0}, {1}], id={2}", an, pn, tGate.name);
          adapter.EndExclusive();
          adapter.Dispose();
          return;
        } else {
          tGate.value.adapter=adapter;
        }
        tGate.value.present=true;
        _gates.Add(tGate.value);
      }
      catch(DalSemi.OneWire.Adapter.AdapterException) {
      }
      catch(Exception ex) {
        Log.Debug("1Wire.Start [{0}, {1}] - {2}", an, pn, ex.Message);
      }
    }
    public void Stop() {
      foreach(var g in _gates) {
        g.Stop();
      }
    }
    private void Dummy(Topic src, TopicChanged arg) {
    }
  }
}
