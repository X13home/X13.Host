﻿using AvalonDock;
using Newtonsoft.Json.Linq;
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
	private DVar<string> _src;
	private DP_Compiler _compiler;

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


	private void DocumentView_Loaded(object sender, RoutedEventArgs e) {
	  _model.changed+=_model_changed;
	  _src=_model.Get<string>("_src");
	  _src.changed+=_src_changed;
	}
	private void DocumentView_Closed(object sender, EventArgs e) {
	  _src.changed-=_src_changed;
	  _model.changed-=_model_changed;
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
		_compiler=new DP_Compiler();
	  }
	  _src.value=this.textEditor.Text;
	  var bytes=_compiler.Parse(this.textEditor.Text);
	  if(bytes!=null) {
		if(_compiler.ioList!=null) {
		  foreach(var v in _compiler.ioList) {
			if((new string[] { "Ip", "Op", "In", "On" }).Any(z => z==v)) {
			  _model.parent.Get<bool>(v);
			} else {
			  _model.parent.Get<long>(v);
			}
		  }
		}
		if(_compiler.varList!=null) {
		  JObject o = new JObject();
		  o["+"]="Newtonsoft.Json.Linq.JObject";
		  foreach(var kv in _compiler.varList) {
			o[kv.Key]=kv.Value;
		  }
		  _model.Get<JObject>("_vars").value=o;
		}
		_model.Get<PLC.ByteArray>("pa0").value=new PLC.ByteArray(bytes);
	  }
	}

  }
}
