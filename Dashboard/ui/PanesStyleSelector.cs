using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace X13 {
  class PanesStyleSelector : StyleSelector {
    public Style LoStyle {
      get;
      set;
    }
    public Style InStyle {
      get;
      set;
    }

    public override System.Windows.Style SelectStyle(object item, System.Windows.DependencyObject container) {
      var it=item as ItemViewModel;
      if(it!=null) {
        if(it.view==Projection.LO) {
          return LoStyle;
        }
        if(it.view==Projection.IN) {
          return InStyle;
        }
      }
      return base.SelectStyle(item, container);
    }
  }
}
