using AvalonDock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using X13.WOUM;

namespace X13.CC {
  /// <summary>
  /// Interaction logic for JSView.xaml
  /// </summary>
  public partial class JSView : DocumentView {
    private Topic _model;
    public JSView(string lName) {
      this.Name = lName;
      if(!Topic.root.Exist(ExConverter.Name2String2("JS_", lName) + "/pa0", out _model)) {
        throw new ArgumentNullException("Not exist");
      }
      this.Title = _model.parent.name;
      InitializeComponent();
      this.textEditor.ShowLineNumbers = true;
      this.textEditor.Options.EnableHyperlinks = false;
      this.textEditor.Options.EnableEmailHyperlinks = false;
      this.textEditor.Options.EnableTextDragDrop = false;
    }
    public override Topic model { get { return _model; } }
  }
}
