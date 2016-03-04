using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace X13.Agent3 {
  /// <summary>
  /// Interaction logic for EventDlg.xaml
  /// </summary>
  public partial class EventDlg : Window {
    public EventDlg(DateTime DT) {
      InitializeComponent();
      this.lbTitel.Content = "Add memo for " + DT.ToLongDateString();
      this.tbMemo.Focus();
    }
    public string Memo { get { return tbMemo.Text; } set { tbMemo.Text = value; } }

    private void btnDialogOk_Click(object sender, RoutedEventArgs e) {
      this.DialogResult = true;
    }
  }
}
