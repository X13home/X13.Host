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
using System.Threading;
using System.Collections;

namespace X13.WOUM {
  
  public class BlockingQueue<T>:IDisposable {
    // TODO: use System.Collections.Concurrent.BlockingCollection<>
    private Queue<T> _queue;
    private AutoResetEvent _sync;
    private bool _paused;

    public BlockingQueue(Action<T> process=null, Action idle=null) {
      _sync=new AutoResetEvent(false);
      _queue = new Queue<T>();
      this.process=process;
      this.idle=idle;
      ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadProc));
    }
    /// <summary>Remove all items from the queue</summary>
    public void Clear() {
      lock(((ICollection)_queue).SyncRoot) {
        _queue.Clear();
      }
    }
    /// <summary>Add an item to the end of the queue</summary>
    /// <param name="item">Item to add</param>
    public void Enqueue(T item) {
      lock(((ICollection)_queue).SyncRoot) {
        _queue.Enqueue(item);
      }
      try {
        _sync.Set();
      }
      catch(ObjectDisposedException) {
      }
    }
    public bool paused {
      get { return _paused; }
      set {
        if(_paused && !value) {
          _paused=value;
          _sync.Set();
        } else {
          _paused=value;
        }
      }
    }
    public int timeout=Timeout.Infinite;
    private Action<T> process;
    private Action idle;
    public Action final;

    private void ThreadProc(Object stateInfo) {
      T val=default(T);
      bool to;
      while(true) {
        try {
          to=_sync.WaitOne(timeout);
        }
        catch(Exception) {
          break;
        }
        if(paused) {
          continue;
        }
        if(to) {
          while(_queue.Count>0) {
            lock(((ICollection)_queue).SyncRoot) {
              val = _queue.Dequeue();
            }

            if(process!=null) {
              process(val);
            }
            if(paused) {
              break;
            }
          }
        } else {
          if(idle!=null) {
            idle();
          }
        }
      }
      if(final!=null) {
        final();
      }
    }

    public void Dispose() {
      _sync.Close();
    }
  }
}
