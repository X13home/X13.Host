using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace X13 {
  internal class PanesTemplateSelector : DataTemplateSelector {
    public PanesTemplateSelector() {

    }

    public DataTemplate InTemplate {
      get;
      set;
    }
    public DataTemplate LoTemplate {
      get;
      set;
    }

    public override System.Windows.DataTemplate SelectTemplate(object item, System.Windows.DependencyObject container) {
      var it=item as ItemViewModel;
      if(it!=null) {
        if(it.view==Projection.LO) {
          return LoTemplate;
        }
        if(it.view==Projection.IN) {
          return InTemplate;
        }
      }

      return base.SelectTemplate(item, container);
    }
  }
}
