using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using X13.lib;

namespace X13 {
  public partial class MainWindow : Window {
    private WAMP.WampClient _client;
    private string _cfgPath;
    private string _connectionUrl;

    public MainWindow() {
      _cfgPath=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)+"/X13/Dashboard.cfg";
      InitializeComponent();
      this.DataContext = Workspace.This;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) {
      string layoutS=null;
      try {
        if(!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(_cfgPath))) {
          System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_cfgPath));
        } else if(System.IO.File.Exists(_cfgPath)) {
          var xd=new XmlDocument();
          xd.Load(_cfgPath);
          var conn=xd.SelectSingleNode("/Config/Url");
          if(conn!=null) {
            _connectionUrl=conn.InnerText;
          } else {
            _connectionUrl="ws://localhost:80/";
          }
          var window=xd.SelectSingleNode("/Config/Window");
          if(window!=null) {
            WindowState st;
            double tmp;
            if(window.Attributes["Top"]!=null && double.TryParse(window.Attributes["Top"].Value, out tmp)) {
              this.Top=tmp;
            }
            if(window.Attributes["Left"]!=null && double.TryParse(window.Attributes["Left"].Value, out tmp)) {
              this.Left=tmp;
            }
            if(window.Attributes["Width"]!=null && double.TryParse(window.Attributes["Width"].Value, out tmp)) {
              this.Width=tmp;
            }
            if(window.Attributes["Height"]!=null && double.TryParse(window.Attributes["Height"].Value, out tmp)) {
              this.Height=tmp;
            }
            if(window.Attributes["State"]!=null && Enum.TryParse(window.Attributes["State"].Value, out st)) {
              this.WindowState=st;
            }
          }
          var xlay=xd.SelectSingleNode("/Config/LayoutRoot");
          if(xlay!=null) {
            layoutS=xlay.OuterXml;
          }
        }
        if(!string.IsNullOrWhiteSpace(_connectionUrl)) {
          _client=new WAMP.WampClient(_connectionUrl);
          _client.Open();
        }
        if(layoutS!=null) {
          var layoutSerializer = new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(dockManager);
          layoutSerializer.LayoutSerializationCallback += (s, e1) => {
            if(!string.IsNullOrWhiteSpace(e1.Model.ContentId)) {
              e1.Content = Workspace.This.Open(e1.Model.ContentId);
            }
          };
          layoutSerializer.Deserialize(new System.IO.StringReader(layoutS));
        }
      }
      catch(Exception ex) {
        Log.Error("Load config - {0}", ex.Message);
      }
    }

    private void BlocksPanel_MLD(object sender, MouseButtonEventArgs e) {

    }

    private void BlocksPanel_MLU(object sender, MouseButtonEventArgs e) {

    }

    private void BlocksPanel_MM(object sender, MouseEventArgs e) {

    }

    private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
      ListViewItem li;
      ItemViewModel it=sender as ItemViewModel;
      if((li= sender as ListViewItem)!=null && (it=li.DataContext as ItemViewModel)!=null){
        Workspace.This.AddFile(it);
      }
    }
    private void Window_Closing(object sender, CancelEventArgs e) {
      if(_client!=null) {
        _client.Close();
        _client=null;
      }
      var layoutSerializer = new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(dockManager);
      try {
        var lDoc=new XmlDocument();
        using(var ix=lDoc.CreateNavigator().AppendChild()) {
          layoutSerializer.Serialize(ix);
        }

        var xd=new XmlDocument();
        var root=xd.CreateElement("Config");
        xd.AppendChild(root);
        if(!string.IsNullOrWhiteSpace(_connectionUrl)) {
          var xUrl=xd.CreateElement("Url");
          xUrl.InnerText=_connectionUrl;
          root.AppendChild(xUrl);
        }
        var window=xd.CreateElement("Window");
        {
          var tmp=xd.CreateAttribute("State");
          tmp.Value=this.WindowState.ToString();
          window.Attributes.Append(tmp);

          tmp=xd.CreateAttribute("Left");
          tmp.Value=this.Left.ToString();
          window.Attributes.Append(tmp);

          tmp=xd.CreateAttribute("Top");
          tmp.Value=this.Top.ToString();
          window.Attributes.Append(tmp);

          tmp=xd.CreateAttribute("Width");
          tmp.Value=this.Width.ToString();
          window.Attributes.Append(tmp);

          tmp=xd.CreateAttribute("Height");
          tmp.Value=this.Height.ToString();
          window.Attributes.Append(tmp);
        }
        root.AppendChild(window);
        root.AppendChild(xd.ImportNode(lDoc.FirstChild, true));
        xd.Save(_cfgPath);
      }
      catch(Exception ex) {
        Log.Error("Save config - {0}", ex.Message);
      }

    }
  }
}
