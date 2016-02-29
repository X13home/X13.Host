using AvalonDock;
using ICSharpCode.AvalonEdit.AddIn;
using ICSharpCode.SharpDevelop.Editor;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
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
	private DVar<string> _src;
	private EP_Compiler _compiler;
    ITextMarkerService textMarkerService;

    public JSView(string lName) {
      this.Name = lName;
      if(!Topic.root.Exist(ExConverter.Name2String2("JS_", lName) + "/pa0", out _model)) {
        throw new ArgumentNullException("Not exist");
      }
      this.Title = _model.parent.name;
      InitializeComponent();
      InitializeTextMarkerService();
      this.textEditor.ShowLineNumbers = true;
      this.textEditor.Options.EnableHyperlinks = false;
      this.textEditor.Options.EnableEmailHyperlinks = false;
      this.textEditor.Options.EnableTextDragDrop = false;
    }
	public override Topic model { get { return _model; } }

	private void DocumentView_Loaded(object sender, RoutedEventArgs e) {
	  _model.changed+=_model_changed;
	  _src=_model.Get<string>("_src");
	  _src.changed+=_src_changed;
	}
	private void DocumentView_Closed(object sender, EventArgs e) {
	  _src.changed-=_src_changed;
	  _model.changed-=_model_changed;
	}
    private void InitializeTextMarkerService() {
      var textMarkerService = new TextMarkerService(textEditor.Document);
      textEditor.TextArea.TextView.BackgroundRenderers.Add(textMarkerService);
      textEditor.TextArea.TextView.LineTransformers.Add(textMarkerService);
      IServiceContainer services = (IServiceContainer)textEditor.Document.ServiceProvider.GetService(typeof(IServiceContainer));
      if(services != null)
        services.AddService(typeof(ITextMarkerService), textMarkerService);
      this.textMarkerService = textMarkerService;
    }

	private void _model_changed(Topic arg1, TopicChanged arg2) {
	}
	private void _src_changed(Topic arg1, TopicChanged arg2) {
	  this.Dispatcher.BeginInvoke(new Action(() => this.textEditor.Text=_src.value), System.Windows.Threading.DispatcherPriority.DataBind);
	}
	private void saveFileClick(object sender, RoutedEventArgs e) {
	  _src.value=this.textEditor.Text;
	}
	private void Compile_Click(object sender, RoutedEventArgs e) {
	  if(_compiler==null) {
		_compiler=new EP_Compiler();
        _compiler.CMsg += _compiler_CMsg;
	  }
      textMarkerService.RemoveAll(m => true);
	  _src.value=this.textEditor.Text;
	  if(_compiler.Parse(this.textEditor.Text)) {
		if(_compiler.ioList!=null) {
		  foreach(var v in _compiler.ioList) {
			if((new string[] { "Ip", "Op", "In", "On" }).Any(z => v.StartsWith(z))) {
			  _model.parent.Get<bool>(v);
			} else {
			  _model.parent.Get<long>(v);
			}
		  }
		}
		if(_compiler.varList!=null) {
          var maping = _model.Get("_map");
          maping.Get<long>("revision").value++;
          var toRemove = maping.children.Where(z => z != null && z.valueType == typeof(string)).Select(z => z.name).Except(_compiler.varList.Select(z => z.Key)).ToArray();
          for(var i = 0; i < toRemove.Length; i++) {
            maping.Get(toRemove[i]).Remove();
          }
          foreach(var kv in _compiler.varList) {
            maping.Get<string>(kv.Key).value = kv.Value;
          }
		}
        uint addr;
        var toDel = _model.children.Where(z => z.valueType == typeof(PLC.ByteArray) && z.name.StartsWith("pa") && uint.TryParse(z.name.Substring(2), out addr) && !_compiler.Hex.ContainsKey(addr)).ToArray();
        foreach(var t in toDel) {
          t.Remove();
        }
        _model.Get<long>("XD_StackBottom").value = (_compiler.StackBottom + 3) / 4;
        foreach(var kv in _compiler.Hex) {
          _model.Get<PLC.ByteArray>("pa" + kv.Key.ToString()).value = kv.Value;
        }
	  }
	}

    void _compiler_CMsg(NiL.JS.MessageLevel level, NiL.JS.Core.CodeCoordinates coords, string message) {
      ITextMarker marker = textMarkerService.Create(textEditor.Document.GetOffset(coords.Line, coords.Column), coords.Length);
      marker.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
      marker.ToolTip = message;

      switch(level) {
      case NiL.JS.MessageLevel.Error:
      case NiL.JS.MessageLevel.CriticalWarning:
      marker.MarkerColor = Colors.Red;
        break;
      case NiL.JS.MessageLevel.Warning:
        marker.MarkerColor = Colors.Yellow;
        break;
      case NiL.JS.MessageLevel.Recomendation:
      marker.MarkerColor = Colors.Blue;
        break;
      default:
      marker.MarkerColor = Colors.LightGray;
        break;
      }

    }
  }
}
