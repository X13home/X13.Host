using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.PLC {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class PiTracer{
    [Newtonsoft.Json.JsonProperty]
    public readonly string path;

    public PiTracer(string path) {
      this.path=path;
    }
  }
}
