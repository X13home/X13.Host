#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
using System.Threading;


#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Configuration.Install;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using X13.Svc;

namespace X13 {
  static class Program {
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    static void Main(string[] args) {
	  var svc = new X13Svc ();
	  svc.StartUp ();
	  Console.WriteLine ("X13Engine running; press Enter to Exit");
	  Console.ReadLine ();
	  svc.Shutdown ();
	}
  }
}
