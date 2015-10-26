using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Media;
using System.Runtime.InteropServices;

namespace X13.Agent2 {
  internal class SayTimeRu {
    [DllImport("winmm.dll")]
    private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

    private static SayTimeRu _instance;
    public static void PlayWav(string fname) {
      _instance._toPlay.Enqueue(fname);
      _instance._pr.Set();
    }

    private System.Collections.Concurrent.ConcurrentQueue<string> _toPlay;
    private AutoResetEvent _pr;

    public SayTimeRu() {
      _instance=this;
      _toPlay=new System.Collections.Concurrent.ConcurrentQueue<string>();
      _pr=new AutoResetEvent(true);

      DateTime DTNow=DateTime.Now;
      if(DTNow.Hour>=7 && DTNow.Hour<22) {
        SayTime(true);
      }
      ThreadPool.QueueUserWorkItem(AudioThread);
    }
    public void SayTime(bool fl=false) {
      string[] Hour1= { "_99", "_1", "_2", "_3", "_4", "_5", "_6", "_7", "_8", "_9", "_10", "_11", "_12", "_13", "_14", "_15", "_16", "_17", "_18", "_19", "_20", "_21", "_22", "_23" };
      string[] Hour2= { "_102", "_100", "_101", "_101", "_101", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_100", "_101", "_101" };
      string[] Min1= { "", "_1001", "_1002", "_3", "_4", "_5", "_6", "_7", "_8", "_9", "_10", "_11", "_12", "_13", "_14", "_15", "_16", "_17", "_18", "_19" };
      string[] Min2= { "_112", "_110", "_111", "_111", "_111", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112" };
      DateTime DTNow=DateTime.Now.AddSeconds(3);
      int hour=DTNow.Hour;
      int min=DTNow.Minute;
      PlayWav("DingDong");

      PlayWav(Hour1[hour]);
      PlayWav(Hour2[hour]);

      if(min==0)
        PlayWav("_103");
      else if(min<20 && min>0) {
        PlayWav(Min1[min]);
        PlayWav(Min2[min]);
      } else if(min>19&&min<30)
        PlayWav("_20");
      else if(min>29&&min<40)
        PlayWav("_30");
      else if(min>39&&min<50)
        PlayWav("_40");
      else if(min>49)
        PlayWav("_50");

      if(min>19) {
        if(min%10!=0) {
          PlayWav(Min1[min%10]);
        }
        PlayWav(Min2[min%10]);
      }
    }
    private void AudioThread(object o) {
      string rname;
      while(true) {
        if(!_toPlay.TryPeek(out rname)) {
          DateTime DTNow=DateTime.Now.AddSeconds(2);
          int hour=DTNow.Hour;
          int min=DTNow.Minute;

          double hm=hour+min/60.0+DTNow.Second/3600.0;
          int Interval;
          if(hm<7) {    // time of still between 22:01 and 6:59
            Interval=(int)(3600000*(7-hm));
          } else if(hm>21) {
            Interval=(int)(3600000*(31-hm));
          } else {
            Interval=(int)(3600000*(hour+1-hm));
          }

          if(!_pr.WaitOne(Interval)) {
            SayTime(true);
          }
        }
        if(!_toPlay.TryDequeue(out rname)) {
          continue;
        }
        try {
          int hour=DateTime.Now.Hour;
          waveOutSetVolume(IntPtr.Zero, (hour>22 || hour<7)?0x3FFFFFFF:uint.MaxValue);
          SoundPlayer payer=new SoundPlayer(Properties.Resources.ResourceManager.GetStream(rname));
          payer.PlaySync();
        }
        catch(Exception ex) {
          Log.Error(ex.ToString());
        }

      }
    }
  }
}
