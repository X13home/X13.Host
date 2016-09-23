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

	public static SayTimeRu instance { get; private set; }
	public static void PlayWav(string fname) {
	  instance._toPlay.Enqueue(fname);
	  instance._pr.Set();
	}

	private System.Collections.Concurrent.ConcurrentQueue<string> _toPlay;
	private AutoResetEvent _pr;

	public SayTimeRu() {
	  instance=this;
	  _toPlay=new System.Collections.Concurrent.ConcurrentQueue<string>();
	  _pr=new AutoResetEvent(true);

	  DateTime DTNow=DateTime.Now;
	  if(DTNow.Hour>=7 && DTNow.Hour<22) {
		SayTime(false);
	  }
	  ThreadPool.QueueUserWorkItem(AudioThread);
	}
	public bool Muted { get; set; }
	public void SayTime(bool fl=false) {
	  string[] Hour1= { "_99", "_1", "_2", "_3", "_4", "_5", "_6", "_7", "_8", "_9", "_10", "_11", "_12", "_13", "_14", "_15", "_16", "_17", "_18", "_19", "_20", "_21", "_22", "_23" };
	  string[] Hour2= { "_102", "_100", "_101", "_101", "_101", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_102", "_100", "_101", "_101" };
	  string[] Min1= { "", "_1001", "_1002", "_3", "_4", "_5", "_6", "_7", "_8", "_9", "_10", "_11", "_12", "_13", "_14", "_15", "_16", "_17", "_18", "_19" };
	  string[] Min2= { "_112", "_110", "_111", "_111", "_111", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112", "_112" };
	  DateTime DTNow=DateTime.Now.AddSeconds(2);
	  int hour=DTNow.Hour;
	  int min=DTNow.Minute;
	  if(fl) {
		PlayWav("DingDong");
	  } else {
		PlayWav("kuranty");
	  }

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
	  DateTime DTNow;
	  while(true) {
		if(!_toPlay.TryPeek(out rname)) {
		  DTNow=DateTime.Now.AddSeconds(2);
		  if(!_pr.WaitOne((3600-DTNow.Minute*60-DTNow.Second)*1000)) {
			DTNow=DateTime.Now.AddSeconds(2);
			if(DTNow.Hour>=7 && DTNow.Hour<=22) {
			  SayTime(true);
			} else {
			  continue;
			}
		  }
		}
		if(!_toPlay.TryDequeue(out rname)) {
		  continue;
		}
		try {
		  int hour=DateTime.Now.Hour;
		  if(!Muted) {
			if(hour>22 || hour<7) {
			  waveOutSetVolume(IntPtr.Zero, 0x03FFFFFF);
			} else {
			  waveOutSetVolume(IntPtr.Zero, uint.MaxValue);
			}
			SoundPlayer payer=new SoundPlayer(Properties.Resources.ResourceManager.GetStream(rname));
			payer.PlaySync();
		  }
		}
		catch(Exception ex) {
		  Log.Error(ex.ToString());
		}

	  }
	}
  }
}
