#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using X13;


namespace X13.Svc {
  [RunInstaller(true)]
  public partial class ProjectInstaller : System.Configuration.Install.Installer {
    public ProjectInstaller() {
      InitializeComponent();
    }
  }
}
