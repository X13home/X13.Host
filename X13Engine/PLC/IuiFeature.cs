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
    createByteMask='B',
    createShortMask='W',
    createIntMask='I',
    createDecimalMask='C',
    createStringMask='S',

    createBoolDef='z',
    createByteDef='b',
    createShortDef='w',
    createIntDef='i',
    createDoubleDef='g',
    createDecimalDef='c',
    createStringDef='s',
    createObjectDef='o',

    addToLogram='A',
    rename='R',
    remove='D',
    open='O',
  }
}
