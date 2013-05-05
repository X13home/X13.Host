#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

namespace X13 {
  public interface IPlugModul {
    void Start();
    void Stop();
  }
  public interface IPlugModulData {
    int priority { get; }
    string name { get; }
  }
}
