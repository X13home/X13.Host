using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Media;

namespace X13.View {
  internal class SayTimeRu {
    private System.Threading.Timer t;
    private TimerCallback timeCB;

    public SayTimeRu(){
      int hour, Interval;
      DateTime DTNow;

      timeCB=new TimerCallback(SayTimeThread);

      DTNow=DateTime.Now;
      hour=DTNow.Hour;
      Interval=60000*(59-DTNow.Minute)+1000*(62-DTNow.Second);
      if(hour<7)    // time of still between 21:01 and 7:59
        Interval+=3600000*(6-hour);
      else if (hour>21)
        Interval+=3600000*(30-hour);

      Log.Debug("SayTimeRu.Interval={0}", Interval);
      t=new System.Threading.Timer(timeCB, (object)true, Interval, Timeout.Infinite);
    }
    public void SayTime() {
      timeCB.BeginInvoke((object)false,null,null);
    }

    private void SayTimeThread(object fl) {
      try{
      string[] Hour1= { "_99", "_1", "_2", "_3", "_4", "_5", "_6", "_7", "_8", "_9", "_10", "_11", "_12", "_13", "_14", "_15", "_16", "_17", "_18", "_19", "_20", "_21", "_22", "_23" };
      string[] Hour2= { "_102", "_100", "_101", "_101", "_101", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_100", "_101", "_101" };
      string[] Min1=  { "", "_1001", "_1002", "_3", "_4", "_5", "_6", "_7", "_8", "_9", "_10", "_11", "_12", "_13", "_14", "_15", "_16", "_17", "_18", "_19" };
      string[] Min2=  { "112", "_110", "_111", "_111", "_111", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112" };
      DateTime DTNow=DateTime.Now;
      int hour=DTNow.Hour;
      int min=DTNow.Minute;
      int Interval=60000*(59-min)+1000*(62-DTNow.Second);

      if(hour<7)    // time of still between 22:01 and 6:59
        Interval+=3600000*(6-hour);
      else if(hour>21)
        Interval+=3600000*(30-hour);

      Log.Debug("SayTimeRu.Interval={0}", Interval);
      t.Change(Interval, Timeout.Infinite);

      if((bool)fl)
        PlayWav("DingDong");

      PlayWav(Hour1[hour]);
      PlayWav(Hour2[hour]);

      if(min==0)
        PlayWav("_103");
      else if (min<20 && min>0){
        PlayWav(Min1[min]);
        PlayWav(Min2[min]);
      }else if (min>19&&min<30)
        PlayWav("_20");
      else if (min>29&&min<40)
        PlayWav("_30");
      else if (min>39&&min<50)
        PlayWav("_40");
      else if (min>49)
        PlayWav("_50");

      if(min>19) {
        if(min%10!=0)
          PlayWav(Min1[min%10]);
        PlayWav(Min2[min%10]);
      }
      }catch(Exception ex){
        Log.Error(ex.ToString());
      }
    }
    public void PlayWav(string fname) {
      try {
        SoundPlayer payer=new SoundPlayer(Properties.Resources.ResourceManager.GetStream(fname));
        payer.PlaySync();
      }
      catch(Exception ex) {
        Log.Error(ex.ToString());
      }
    }
  }
}
