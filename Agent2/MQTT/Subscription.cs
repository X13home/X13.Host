using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.MQTT {
  internal class Subscription : IDisposable {
    private MqClient _client;
    public readonly string remotePath;
    public readonly Action<string, string> cb;

    public Subscription(MqClient client, string mask, Action<string, string> cb) {
      this._client = client;
      this.remotePath = mask;
      this.cb = cb;
    }

    public void Dispose() {
      _client.Unsubscribe(this);
    }
  }
}
