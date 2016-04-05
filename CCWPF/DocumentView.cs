using AvalonDock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.CC {
  public abstract class DocumentView : DocumentContent {
    public abstract Topic model { get; }
  }
}
