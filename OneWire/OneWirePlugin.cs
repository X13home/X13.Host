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
      System.Threading.ThreadPool.QueueUserWorkItem(Process);
    }

    private void ConnectAdapter(string an, string pn) {
      List<byte[]> ids=new List<byte[]>();
      PortAdapter adapter;
      try {
        adapter=AccessProvider.GetAdapter(an, pn);
        adapter.BeginExclusive(true);
        adapter.Speed = OWSpeed.SPEED_REGULAR;
        adapter.TargetAllFamilies();
        byte[] id = new byte[8];
        DVar<OneWireGate> tGate=null;
        if(adapter.GetFirstDevice(id, 0)) {
          do {
            ids.Add(id);
            id = new byte[8];
          } while(adapter.GetNextDevice(id, 0));
          if(ids.Any()) {
            var gId=ids.FirstOrDefault(z => z[0]==0x81);
            if(gId==null) {
              Log.Warning("1Wire adapter({0}, {1}) - id not found", an, pn);
              adapter.EndExclusive();
              adapter.Dispose();
              return;
            }
            tGate=_dev1w.children.FirstOrDefault(z => {
              var g=z as DVar<OneWireGate>;
              return g!=null && g.value!=null && id.SequenceEqual(g.value.rom);
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
            Log.Info("{0} connected", tGate.path);
            _gates.Add(tGate.value);
            foreach(var did in ids.Where(z => z!=null && !gId.SequenceEqual(z))) {
              var dev=_dev1w.children.FirstOrDefault(z => {
                var d=z.GetValue() as OneWireBase;
                return d!=null && d.rom.SequenceEqual(did);
              });
              if(dev==null) {
                switch(did[0]) {
                case 0x28:    // DS18B20
                  dev=_dev1w.Get<DS18B20>(string.Format("DS18B20_{0:X2}{1:X2}{2:X2}{3:X2}", did[4], did[3], did[2], did[1]));
                  (dev as DVar<DS18B20>).value=new DS18B20(tGate.value, did);
                  break;
                }
              } else {
                (dev.GetValue() as OneWireBase).gate=tGate.value;
              }
              (dev.GetValue() as OneWireBase).present=true;

            }
          }
        }
      }
      catch(DalSemi.OneWire.Adapter.AdapterException) {
      }
      catch(Exception ex) {
        Log.Debug("1Wire.Start [{0}, {1}] - {2}", an, pn, ex.Message);
      }
    }
    private void Process(object o){
      while(true) {
        foreach(var g in _gates) {
          g.Proccess();
        }
        Thread.Sleep(15);
      }
    }
    public void Stop() {
    }
    private void Dummy(Topic src, TopicChanged arg) {
    }
  }
}
