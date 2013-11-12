#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

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

namespace X13.CC {
  /// <summary>
  /// Interaktionslogik für Security.xaml
  /// </summary>
  public partial class SecurityView  : DockableContent {
    private static char[] _delmiter=new char[] { '/' };
    private static SecurityView _instance;
    internal static void SetSelected(Topic t) {
      if(t!=null && _instance!=null) {
        _instance.Dispatcher.BeginInvoke(new Action<Topic>(_instance.ShowInfo), t);
      }
    }

    private Topic _cur;
    private Topic _acls;
    private Topic _groups;

    public SecurityView() {
      _instance=this;
      _acls=Topic.root.Get("/etc/Broker/security/acls");
      _groups=Topic.root.Get("/etc/Broker/security/groups");
      InitializeComponent();
    }

    private void ShowInfo(Topic t) {
      _cur=t;
      Topic tmp=_acls;
      DVar<long> acl=null;
      var lvls=_cur.path.Split(_delmiter, StringSplitOptions.RemoveEmptyEntries);
      for(int i=0; i<lvls.Length; i++) {
        if(!tmp.Exist(lvls[i], out tmp)) {
          break;
        }
        if(tmp is DVar<long>) {
          acl=tmp as DVar<long>;
        }
      }

      tbPath.Text=_cur.path;
      cbGroup.Items.Clear();
      foreach(var gr in _groups.children){
        cbGroup.Items.Add(gr.GetValue());
      }
      if(acl==null) {
        laInherited.Content="/";
        cbGroup.SelectedValue=string.Empty;
        cbGrRead.IsChecked=false;
        cbGrWrite.IsChecked=false;
        cbGrCreate.IsChecked=false;
        cbGrRemove.IsChecked=false;
        cbEvRead.IsChecked=false;
        cbEvWrite.IsChecked=false;
        cbEvCreate.IsChecked=false;
        cbEvRemove.IsChecked=false;
      } else {
        laInherited.Content=acl.path.Substring(_acls.path.Length);
        Topic grp;
        if(_groups.Exist((acl.value&0xFFFF).ToString(), out grp)) {
          cbGroup.SelectedValue=grp.GetValue();
        } else {
          cbGroup.SelectedValue=string.Empty;
        }
        var aclAll=(TopicAcl)((acl.value>>28) & 0x0F);
        var aclOwner=(TopicAcl)((acl.value>>24) & 0x0F);
               
        cbGrRead.IsChecked=(aclOwner&TopicAcl.Subscribe)!=TopicAcl.None;
        cbGrWrite.IsChecked=(aclOwner&TopicAcl.Change)!=TopicAcl.None;
        cbGrCreate.IsChecked=(aclOwner&TopicAcl.Create)!=TopicAcl.None;
        cbGrRemove.IsChecked=(aclOwner&TopicAcl.Delete)!=TopicAcl.None;
        cbEvRead.IsChecked=(aclAll&TopicAcl.Subscribe)!=TopicAcl.None;
        cbEvWrite.IsChecked=(aclAll&TopicAcl.Change)!=TopicAcl.None;
        cbEvCreate.IsChecked=(aclAll&TopicAcl.Create)!=TopicAcl.None;
        cbEvRemove.IsChecked=(aclAll&TopicAcl.Delete)!=TopicAcl.None;
      }
    }

    private void ClearClick(object sender, RoutedEventArgs e) {
      if(_cur==null) {
        return;
      }
      Topic acl;
      if(_acls.Exist(_cur.path.Substring(1), out acl)) {
        acl.Remove();
        System.Threading.Thread.Sleep(300);
        ShowInfo(_cur);
      }
    }

    private void SetClick(object sender, RoutedEventArgs e) {
      var acl=_acls.Get<long>(_cur.path.Substring(1));
      if(cbGroup.SelectedValue==null) {
        Log.Warning("Set acl: group is empty");
        return;
      }
      Topic group=_groups.children.FirstOrDefault(z => cbGroup.SelectedValue.Equals(z.GetValue()));
      uint val=0;
      if(group==null || !uint.TryParse(group.name, out val)) {
        Log.Warning("Set acl: unknown group");
        return;
      }
      val|=(uint)((cbGrRead.IsChecked==true)?(1<<24):0);
      val|=(uint)((cbGrWrite.IsChecked==true)?(1<<25):0);
      val|=(uint)((cbGrCreate.IsChecked==true)?(1<<26):0);
      val|=(uint)((cbGrRemove.IsChecked==true)?(1<<27):0);
      val|=(uint)((cbEvRead.IsChecked==true)?(1<<28):0);
      val|=(uint)((cbEvWrite.IsChecked==true)?(1<<29):0);
      val|=(uint)((cbEvCreate.IsChecked==true)?(1<<30):0);
      val|=(uint)((cbEvRemove.IsChecked==true)?(1<<31):0);
      acl.saved=true;
      acl.value=0;
      acl.value=val;
    }
  }
}
