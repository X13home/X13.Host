using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  internal interface Idb3 {
    bool Open();
    bool Read(out string path, out string type, out string val);
    void Close();
  }
}
