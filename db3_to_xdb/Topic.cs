#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  internal class Topic {
    static Topic() {
      root=new Topic(null, "/", string.Empty, string.Empty);
    }
    public static readonly Topic root;

    public static void Add(string path, string type, string val) {
      if(string.IsNullOrEmpty(path)) {
        return; // root
      }
      string[] pt=path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
      if(pt.Length==0) {
        return;
      }
      Topic parent=root;
      Topic cur;
      for(int i=0; i<pt.Length; i++) {
        if(parent.children==null) {
          parent.children=new List<Topic>();
        }
        cur=parent.children.FirstOrDefault(z => z.name==pt[i]);
        if(cur==null) {
          if(i==pt.Length-1) {
            cur=new Topic(parent, pt[i], type, val);
            return;
          } else {
            cur=new Topic(parent, pt[i], string.Empty, string.Empty);
          }
        } else if(i==pt.Length-1) {
          cur.type=type;
          cur.val=val;
          return;
        }
        parent=cur;
      }
    }

    private Topic(Topic parent, string name, string type, string val) {
      this.parent=parent;
      this.name=name;
      this.type=type;
      this.val=val;
      if(parent!=null) {
        if(parent.children==null) {
          parent.children=new List<Topic>();
        }
        lvl=parent.lvl+1;
        parent.children.Add(this);
      } else {
        lvl=1;
      }
    }

    public readonly string name;
    public readonly Topic parent;
    public readonly int lvl;
    public string type;
    public string val;
    public int pos;
    public List<Topic> children;
  }
}
