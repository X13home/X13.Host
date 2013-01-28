#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

namespace X13.PLC {
  public enum ItemAction : ushort {
    empty=' ',
    createNodeMask='N',
    createBoolMask='Z',
    createLongMask='I',
    createDoubleMask='G',
    createStringMask='S',

    createBoolDef='z',
    createLongDef='i',
    createDoubleDef='g',
    createStringDef='s',
    createObjectDef='o',

    addToLogram='A',
    rename='R',
    remove='D',
    open='O',
  }
}
