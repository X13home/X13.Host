using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.WAMP {
  internal class IdGenerator {
    private static Random _rand;

    static IdGenerator() {
      _rand = new Random();
    }

    public static long Generate() {
      long id=_rand.Next(0, int.MaxValue);
      id<<=(50-31);
      id^=_rand.Next(0, int.MaxValue);
      return id;
    }
  }
  public enum WampMessageType : ushort {
    Hello = 1,
    Welcome = 2,
    Abort = 3,
    Challenge = 4,
    Authenticate = 5,
    Goodbye = 6,
    Heartbeat = 7,
    Error = 8,

    Publish = 16,
    Published = 17,
    Subscribe = 32,
    Subscribed = 33,
    Unsubscribe = 34,
    Unsubscribed = 35,
    Event = 36,

    Call = 48,
    Cancel = 49,
    Result = 50,
    Register = 64,
    Registered = 65,
    Unregister = 66,
    Unregistered = 67,
    Invocation = 68,
    Interrupt = 69,
    Yield = 70,
  }
  internal enum AllOptions {
    None,
    SubscribeMatchPrefix,
    SubscribeMatchWildcard,
  }
  internal interface IWampCommand {
    WampMessageType cmd { get; }
  }
  internal class WampSubscribe : IWampCommand {
    private long _reqId;

    public WampSubscribe(string path, long reqId, AllOptions options=AllOptions.None) {
      this.path=path;
      if(options==AllOptions.SubscribeMatchPrefix || options==AllOptions.SubscribeMatchWildcard) {
        this.options=options;
      } else {
        this.options=AllOptions.None;
      }
      _reqId=reqId;
    }
    public readonly string path;
    public readonly AllOptions options;
    public WampMessageType cmd { get { return WampMessageType.Subscribe; } }
    public override string ToString() {
      StringBuilder sb=new StringBuilder();
      sb.AppendFormat("[{0}, {1}, ", (int)WampMessageType.Subscribe, _reqId);
      if(options==AllOptions.SubscribeMatchPrefix) {
        sb.Append("{\"match\": \"prefix\"}");
      } else if(options==AllOptions.SubscribeMatchWildcard) {
        sb.Append("{\"match\": \"wildcard\"}");
      } else{
        sb.Append("{ }");
      }
      sb.AppendFormat(", {0}]", path);
      return sb.ToString();
    }
  }

}
