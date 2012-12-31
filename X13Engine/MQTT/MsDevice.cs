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
using System.Threading;
using X13.PLC;
using X13.MQTT;

namespace X13.MQTT {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class MsDevice : ITopicOwned {
    private const int ACK_TIMEOUT=550;

    static MsDevice() {
      if(Topic.brokerMode) {
        SortedList<string, string> menu=new SortedList<string, string>();
        #region jeeGate/jeeNode
        menu.Clear();
        menu["Add/bool"]="1Z";
        menu["Add/byte"]="2B";
        menu["Add/short"]="3W";
        menu["Add/int"]="4I";
        menu["Add/string"]="5S";

        menu["P1.A/Ip16"]="6zX1";
        menu["P1.A/Ip16/_description"]="Digital input, P1.A";
        menu["P1.A/In16"] ="7zX1";
        menu["P1.A/In16/_description"]="Digital Pull-Up inverted input, P1.A";
        menu["P1.A/Op16"]="8zX1";
        menu["P1.A/Op16/_description"]="Digital output, P1.A";
        menu["P1.A/On16"]="9zX1";
        menu["P1.A/On16/_description"]="Digital inverted output, P1.A";
        menu["P1.A/Ai0"]="AwX1";
        menu["P1.A/Ai0/_description"]="Analog input, internal reference, P1.A";
        menu["P1.A/Av0"]="BwX1";
        menu["P1.A/Av0/_description"]="Analog input, AVcc reference, P1.A";

        menu["P2.A/Ip17"]="6zX2";
        menu["P2.A/Ip17/_description"]="Digital input, P2.A";
        menu["P2.A/In17"] ="7zX2";
        menu["P2.A/In17/_description"]="Digital Pull-Up inverted input, P2.A";
        menu["P2.A/Op17"]="8zX2";
        menu["P2.A/Op17/_description"]="Digital output, P2.A";
        menu["P2.A/On17"]="9zX2";
        menu["P2.A/On17/_description"]="Digital inverted output, P2.A";
        menu["P2.A/Ai1"]="AwX2";
        menu["P2.A/Ai1/_description"]="Analog input, internal reference, P2.A";
        menu["P2.A/Av1"]="BwX2";
        menu["P2.A/Av1/_description"]="Analog input, AVcc reference, P2.A";

        menu["P3.A/Ip18"]="6zX3";
        menu["P3.A/Ip18/_description"]="Digital input, P3.A";
        menu["P3.A/In18"] ="7zX3";
        menu["P3.A/In18/_description"]="Digital Pull-Up inverted input, P3.A";
        menu["P3.A/Op18"]="8zX3";
        menu["P3.A/Op18/_description"]="Digital output, P3.A";
        menu["P3.A/On18"]="9zX3";
        menu["P3.A/On18/_description"]="Digital inverted output, P3.A";
        menu["P3.A/Ai2"]="AwX3";
        menu["P3.A/Ai2/_description"]="Analog input, internal reference, P3.A";
        menu["P3.A/Av2"]="BwX3";
        menu["P3.A/Av2/_description"]="Analog input, AVcc reference, P3.A";

        menu["P4.A/Ip19"]="6zX4";
        menu["P4.A/Ip19/_description"]="Digital input, P4.A";
        menu["P4.A/In19"] ="7zX4";
        menu["P4.A/In19/_description"]="Digital Pull-Up inverted input, P4.A";
        menu["P4.A/Op19"]="8zX4";
        menu["P4.A/Op19/_description"]="Digital output, P4.A";
        menu["P4.A/On19"]="9zX4";
        menu["P4.A/On19/_description"]="Digital inverted output, P4.A";
        menu["P4.A/Ai3"]="AwX4";
        menu["P4.A/Ai3/_description"]="Analog input, internal reference, P4.A";
        menu["P4.A/Av3"]="BwX4";
        menu["P4.A/Av3/_description"]="Analog input, AVcc reference, P4.A";

        menu["P?.I/Ip26"]="6zX9";
        menu["P?.I/Ip26/_description"]="Digital input, P?.I";
        menu["P?.I/In26"] ="7zX9";
        menu["P?.I/In26/_description"]="Digital Pull-Up inverted input, P?.I";
        menu["P?.I/Op26"]="8zX9";
        menu["P?.I/Op26/_description"]="Digital output, P?.I";
        menu["P?.I/On26"]="9zX9";
        menu["P?.I/On26/_description"]="Digital inverted output, P?.I";

        menu["P1.D/Ip28"]="6zX11";
        menu["P1.D/Ip28/_description"]="Digital input, P1.D";
        menu["P1.D/In28"] ="7zX11";
        menu["P1.D/In28/_description"]="Digital Pull-Up inverted input, P1.D";
        menu["P1.D/Op28"]="8zX11";
        menu["P1.D/Op28/_description"]="Digital output, P1.D";
        menu["P1.D/On28"]="9zX11";
        menu["P1.D/On28/_description"]="Digital inverted output, P1.D";

        menu["P2.D/Ip29"]="6zX12";
        menu["P2.D/Ip29/_description"]="Digital input, P2.D";
        menu["P2.D/In29"] ="7zX12";
        menu["P2.D/In29/_description"]="Digital Pull-Up inverted input, P2.D";
        menu["P2.D/Op29"]="8zX12";
        menu["P2.D/Op29/_description"]="Digital output, P2.D";
        menu["P2.D/On29"]="9zX12";
        menu["P2.D/On29/_description"]="Digital inverted output, P2.D";
        menu["P2.D/Pp29"]="8bX12";
        menu["P2.D/Pp29/_description"]="PWM positive, P2.D";
        menu["P2.D/Pn29"]="9bX12";
        menu["P2.D/Pn29/_description"]="PWM negative, P2.D";

        menu["P3.D/Ip30"]="6zX13";
        menu["P3.D/Ip30/_description"]="Digital input, P3.D";
        menu["P3.D/In30"] ="7zX13";
        menu["P3.D/In30/_description"]="Digital Pull-Up inverted input, P3.D";
        menu["P3.D/Op30"]="8zX13";
        menu["P3.D/Op30/_description"]="Digital output, P3.D";
        menu["P3.D/On30"]="9zX13";
        menu["P3.D/On30/_description"]="Digital inverted output, P3.D";
        menu["P3.D/Pp30"]="8bX13";
        menu["P3.D/Pp30/_description"]="PWM positive, P3.D";
        menu["P3.D/Pn30"]="9bX13";
        menu["P3.D/Pn30/_description"]="PWM negative, P3.D";

        menu["P4.D/Ip31"]="6zX14";
        menu["P4.D/Ip31/_description"]="Digital input, P4.D";
        menu["P4.D/In31"] ="7zX14";
        menu["P4.D/In31/_description"]="Digital Pull-Up inverted input, P4.D";
        menu["P4.D/Op31"]="8zX14";
        menu["P4.D/Op31/_description"]="Digital output, P4.D";
        menu["P4.D/On31"]="9zX14";
        menu["P4.D/On31/_description"]="Digital inverted output, P4.D";

        menu["Vcc/Av14"]="BwX15";
        menu["Vcc/Av14/_description"]="Analog value, Vcc=1.1*1024/Av14";

        menu["rename"]="YR";
        menu["remove"]="ZD";
        StoreDeclarer(menu, "/system/declarers/J_Gate", "/CC;component/Images/ty_jgate.png");

        menu["TX/Ip24"]="6zX7";
        menu["TX/Ip24/_description"]="Digital input, TX";
        menu["TX/In24"] ="7zX7";
        menu["TX/In24/_description"]="Digital Pull-Up inverted input, TX";
        menu["TX/Op24"]="8zX7";
        menu["TX/Op24/_description"]="Digital output, TX";
        menu["TX/On24"]="9zX7";
        menu["TX/On24/_description"]="Digital inverted output, TX";
        menu["TX/St4"]="AsX7";
        menu["TX/St4/_description"]="TxD 38400, TX";

        menu["RX/Ip25"]="6zX8";
        menu["RX/Ip25/_description"]="Digital input, RX";
        menu["RX/In25"] ="7zX8";
        menu["RX/In25/_description"]="Digital Pull-Up inverted input, RX";
        menu["RX/Op25"]="8zX8";
        menu["RX/Op25/_description"]="Digital output, RX";
        menu["RX/On25"]="9zX8";
        menu["RX/On25/_description"]="Digital inverted output, RX";
        menu["RX/Sr4"]="AsX8";
        menu["RX/Sr4/_description"]="RxD 38400, RX";
        StoreDeclarer(menu, "/system/declarers/J_Node", "/CC;component/Images/ty_jnode.png");
        #endregion jeeGate/jeeNode

        #region uGate/uNode
        menu.Clear();
        menu["Add/bool"]="1Z";
        menu["Add/byte"]="2B";
        menu["Add/short"]="3W";
        menu["Add/int"]="4I";
        menu["Add/string"]="5S";

        menu["SV1-01/Ai7"]="6wX6";
        menu["SV1-01/Ai7/_description"]="Analog input, internal reference, SV1-01";
        menu["SV1-01//Av7"]="7wX6";
        menu["SV1-01//Av7/_description"]="Analog input, AVcc reference, SV1-01";

        menu["SV1-03/Ip16"]="6zX1";
        menu["SV1-03/Ip16/_description"]="Digital input, SV1-03";
        menu["SV1-03/In16"] ="7zX1";
        menu["SV1-03/In16/_description"]="Digital Pull-Up inverted input, SV1-03";
        menu["SV1-03/Op16"]="8zX1";
        menu["SV1-03/Op16/_description"]="Digital output, SV1-03";
        menu["SV1-03/On16"]="9zX1";
        menu["SV1-03/On16/_description"]="Digital inverted output, SV1-03";
        menu["SV1-03/Ai0"]="AwX1";
        menu["SV1-03/Ai0/_description"]="Analog input, internal reference, SV1-03";
        menu["SV1-03/Av0"]="BwX1";
        menu["SV1-03/Av0/_description"]="Analog input, AVcc reference, SV1-03";

        menu["SV1-04/Ip17"]="6zX2";
        menu["SV1-04/Ip17/_description"]="Digital input, SV1-04";
        menu["SV1-04/In17"] ="7zX2";
        menu["SV1-04/In17/_description"]="Digital Pull-Up inverted input, SV1-04";
        menu["SV1-04/Op17"]="8zX2";
        menu["SV1-04/Op17/_description"]="Digital output, SV1-04";
        menu["SV1-04/On17"]="9zX2";
        menu["SV1-04/On17/_description"]="Digital inverted output, SV1-04";
        menu["SV1-04/Ai1"]="AwX2";
        menu["SV1-04/Ai1/_description"]="Analog input, internal reference, SV1-04";
        menu["SV1-04/Av1"]="BwX2";
        menu["SV1-04/Av1/_description"]="Analog input, AVcc reference, SV1-04";

        menu["SV1-05/Ip18"]="6zX3";
        menu["SV1-05/Ip18/_description"]="Digital input, SV1-05";
        menu["SV1-05/In18"] ="7zX3";
        menu["SV1-05/In18/_description"]="Digital Pull-Up inverted input, SV1-05";
        menu["SV1-05/Op18"]="8zX3";
        menu["SV1-05/Op18/_description"]="Digital output, SV1-05";
        menu["SV1-05/On18"]="9zX3";
        menu["SV1-05/On18/_description"]="Digital inverted output, SV1-05";
        menu["SV1-05/Ai2"]="AwX3";
        menu["SV1-05/Ai2/_description"]="Analog input, internal reference, SV1-05";
        menu["SV1-05/Av2"]="BwX3";
        menu["SV1-05/Av2/_description"]="Analog input, AVcc reference, SV1-05";

        menu["SV1-06/Ip19"]="6zX4";
        menu["SV1-06/Ip19/_description"]="Digital input, SV1-06";
        menu["SV1-06/In19"] ="7zX4";
        menu["SV1-06/In19/_description"]="Digital Pull-Up inverted input, SV1-06";
        menu["SV1-06/Op19"]="8zX4";
        menu["SV1-06/Op19/_description"]="Digital output, SV1-06";
        menu["SV1-06/On19"]="9zX4";
        menu["SV1-06/On19/_description"]="Digital inverted output, SV1-06";
        menu["SV1-06/Ai3"]="AwX4";
        menu["SV1-06/Ai3/_description"]="Analog input, internal reference, SV1-06";
        menu["SV1-06/Av3"]="BwX4";
        menu["SV1-06/Av3/_description"]="Analog input, AVcc reference, SV1-06";

        menu["SV1-13/Ip26"]="6zX9";
        menu["SV1-13/Ip26/_description"]="Digital input, SV1-13";
        menu["SV1-13/In26"] ="7zX9";
        menu["SV1-13/In26/_description"]="Digital Pull-Up inverted input, SV1-13";
        menu["SV1-13/Op26"]="8zX9";
        menu["SV1-13/Op26/_description"]="Digital output, SV1-13";
        menu["SV1-13/On26"]="9zX9";
        menu["SV1-13/On26/_description"]="Digital inverted output, SV1-13";

        menu["SV1-14/Ip27"]="6zX10";
        menu["SV1-14/Ip27/_description"]="Digital input, SV1-14";
        menu["SV1-14/In27"] ="7zX10";
        menu["SV1-14/In27/_description"]="Digital Pull-Up inverted input, SV1-14";
        menu["SV1-14/Op27"]="8zX10";
        menu["SV1-14/Op27/_description"]="Digital output, SV1-14";
        menu["SV1-14/On27"]="9zX10";
        menu["SV1-14/On27/_description"]="Digital inverted output, SV1-14";

        menu["SV1-15/Ip28"]="6zX11";
        menu["SV1-15/Ip28/_description"]="Digital input, SV1-15";
        menu["SV1-15/In28"] ="7zX11";
        menu["SV1-15/In28/_description"]="Digital Pull-Up inverted input, SV1-15";
        menu["SV1-15/Op28"]="8zX11";
        menu["SV1-15/Op28/_description"]="Digital output, SV1-15";
        menu["SV1-15/On28"]="9zX11";
        menu["SV1-15/On28/_description"]="Digital inverted output, SV1-15";

        menu["SV1-16/Ip29"]="6zX12";
        menu["SV1-16/Ip29/_description"]="Digital input, SV1-16";
        menu["SV1-16/In29"] ="7zX12";
        menu["SV1-16/In29/_description"]="Digital Pull-Up inverted input, SV1-16";
        menu["SV1-16/Op29"]="8zX12";
        menu["SV1-16/Op29/_description"]="Digital output, SV1-16";
        menu["SV1-16/On29"]="9zX12";
        menu["SV1-16/On29/_description"]="Digital inverted output, SV1-16";
        menu["SV1-16/Pp29"]="8bX12";
        menu["SV1-16/Pp29/_description"]="PWM positive, SV1-16";
        menu["SV1-16/Pn29"]="9bX12";
        menu["SV1-16/Pn29/_description"]="PWM negative, SV1-16";

        menu["SV1-17/Ip30"]="6zX13";
        menu["SV1-17/Ip30/_description"]="Digital input, SV1-17";
        menu["SV1-17/In30"] ="7zX13";
        menu["SV1-17/In30/_description"]="Digital Pull-Up inverted input, SV1-17";
        menu["SV1-17/Op30"]="8zX13";
        menu["SV1-17/Op30/_description"]="Digital output, SV1-17";
        menu["SV1-17/On30"]="9zX13";
        menu["SV1-17/On30/_description"]="Digital inverted output, SV1-17";
        menu["SV1-17/Pp30"]="8bX13";
        menu["SV1-17/Pp30/_description"]="PWM positive, SV1-17";
        menu["SV1-17/Pn30"]="9bX13";
        menu["SV1-17/Pn30/_description"]="PWM negative, SV1-17";

        menu["SV1-18/Ip31"]="6zX14";
        menu["SV1-18/Ip31/_description"]="Digital input, SV1-18";
        menu["SV1-18/In31"] ="7zX14";
        menu["SV1-18/In31/_description"]="Digital Pull-Up inverted input, SV1-18";
        menu["SV1-18/Op31"]="8zX14";
        menu["SV1-18/Op31/_description"]="Digital output, SV1-18";
        menu["SV1-18/On31"]="9zX14";
        menu["SV1-18/On31/_description"]="Digital inverted output, SV1-18";

        menu["Vcc/Av14"]="BwX15";
        menu["Vcc/Av14/_description"]="Analog value, Vcc=1.1*1024/Av14, SV1-06";

        menu["rename"]="YR";
        menu["remove"]="ZD";
        StoreDeclarer(menu, "/system/declarers/U_Gate", "/CC;component/Images/ty_jgate.png");

        menu["SV1-11/Ip24"]="6zX7";
        menu["SV1-11/Ip24/_description"]="Digital input, SV1-11";
        menu["SV1-11/In24"] ="7zX7";
        menu["SV1-11/In24/_description"]="Digital Pull-Up inverted input, SV1-11";
        menu["SV1-11/Op24"]="8zX7";
        menu["SV1-11/Op24/_description"]="Digital output, SV1-11";
        menu["SV1-11/On24"]="9zX7";
        menu["SV1-11/On24/_description"]="Digital inverted output, SV1-11";
        menu["SV1-11/St4"]="AsX7";
        menu["SV1-11/St4/_description"]="TxD 38400, SV1-11";

        menu["SV1-12/Ip25"]="6zX8";
        menu["SV1-12/Ip25/_description"]="Digital input, SV1-12";
        menu["SV1-12/In25"] ="7zX8";
        menu["SV1-12/In25/_description"]="Digital Pull-Up inverted input, SV1-12";
        menu["SV1-12/Op25"]="8zX8";
        menu["SV1-12/Op25/_description"]="Digital output, SV1-12";
        menu["SV1-12/On25"]="9zX8";
        menu["SV1-12/On25/_description"]="Digital inverted output, SV1-12";
        menu["SV1-12/Sr4"]="AsX8";
        menu["SV1-12/Sr4/_description"]="RxD 38400, SV1-12";

        StoreDeclarer(menu, "/system/declarers/U_Node", "/CC;component/Images/ty_jnode.png");
        #endregion uGate/uNode

        #region PS_Gate/PS_Node
        menu.Clear();
        menu["Add/bool"]="1Z";
        menu["Add/byte"]="2B";
        menu["Add/short"]="3W";
        menu["Add/int"]="4I";
        menu["Add/string"]="5S";

        menu["1.02/Ip08"]="6zX15";
        menu["1.02/Ip08/_description"]="Digital input, 1.02";
        menu["1.02/In08"] ="7zX15";
        menu["1.02/In08/_description"]="Digital Pull-Up inverted input, 1.02";
        menu["1.02/Op08"]="8zX15";
        menu["1.02/Op08/_description"]="Digital output, 1.02";
        menu["1.02/On08"]="9zX15";
        menu["1.02/On08/_description"]="Digital inverted output, 1.02";

        menu["1.03/Ip09"]="6zX16";
        menu["1.03/Ip09/_description"]="Digital input, 1.03";
        menu["1.03/In09"] ="7zX16";
        menu["1.03/In09/_description"]="Digital Pull-Up inverted input, 1.03";
        menu["1.03/Op09"]="8zX16";
        menu["1.03/Op09/_description"]="Digital output, 1.03";
        menu["1.03/On09"]="9zX16";
        menu["1.03/On09/_description"]="Digital inverted output, 1.03";

        menu["1.04/Ip16"]="6zX1";
        menu["1.04/Ip16/_description"]="Digital input, 1.04";
        menu["1.04/In16"] ="7zX1";
        menu["1.04/In16/_description"]="Digital Pull-Up inverted input, 1.04";
        menu["1.04/Op16"]="8zX1";
        menu["1.04/Op16/_description"]="Digital output, 1.04";
        menu["1.04/On16"]="9zX1";
        menu["1.04/On16/_description"]="Digital inverted output, 1.04";
        menu["1.04/Ai0"]="AwX1";
        menu["1.04/Ai0/_description"]="Analog input, internal reference, 1.04";
        menu["1.04/Av0"]="BwX1";
        menu["1.04/Av0/_description"]="Analog input, AVcc reference, 1.04";

        menu["1.05/Ip17"]="6zX2";
        menu["1.05/Ip17/_description"]="Digital input, 1.05";
        menu["1.05/In17"] ="7zX2";
        menu["1.05/In17/_description"]="Digital Pull-Up inverted input, 1.05";
        menu["1.05/Op17"]="8zX2";
        menu["1.05/Op17/_description"]="Digital output, 1.05";
        menu["1.05/On17"]="9zX2";
        menu["1.05/On17/_description"]="Digital inverted output, 1.05";
        menu["1.05/Ai1"]="AwX2";
        menu["1.05/Ai1/_description"]="Analog input, internal reference, 1.05";
        menu["1.05/Av1"]="BwX2";
        menu["1.05/Av1/_description"]="Analog input, AVcc reference, 1.05";

        menu["1.06/Ip18"]="6zX3";
        menu["1.06/Ip18/_description"]="Digital input, 1.06";
        menu["1.06/In18"] ="7zX3";
        menu["1.06/In18/_description"]="Digital Pull-Up inverted input, 1.06";
        menu["1.06/Op18"]="8zX3";
        menu["1.06/Op18/_description"]="Digital output, 1.06";
        menu["1.06/On18"]="9zX3";
        menu["1.06/On18/_description"]="Digital inverted output, 1.06";
        menu["1.06/Ai2"]="AwX3";
        menu["1.06/Ai2/_description"]="Analog input, internal reference, 1.06";
        menu["1.06/Av2"]="BwX3";
        menu["1.06/Av2/_description"]="Analog input, AVcc reference, 1.06";

        menu["1.08/Ip19"]="6zX4";
        menu["1.08/Ip19/_description"]="Digital input, 1.08";
        menu["1.08/In19"] ="7zX4";
        menu["1.08/In19/_description"]="Digital Pull-Up inverted input, 1.08";
        menu["1.08/Op19"]="8zX4";
        menu["1.08/Op19/_description"]="Digital output, 1.08";
        menu["1.08/On19"]="9zX4";
        menu["1.08/On19/_description"]="Digital inverted output, 1.08";
        menu["1.08/Ai3"]="AwX4";
        menu["1.08/Ai3/_description"]="Analog input, internal reference, 1.08";
        menu["1.08/Av3"]="BwX4";
        menu["1.08/Av3/_description"]="Analog input, AVcc reference, 1.08";

        menu["1.11/Ai6"]="6wX5";
        menu["1.11/Ai6/_description"]="Analog input, internal reference, 1.11";
        menu["1.11//Av6"]="7wX5";
        menu["1.11//Av6/_description"]="Analog input, AVcc reference, 1.11";

        menu["1.12/Ai7"]="6wX6";
        menu["1.12/Ai7/_description"]="Analog input, internal reference, 1.12";
        menu["1.12//Av7"]="7wX6";
        menu["1.12//Av7/_description"]="Analog input, AVcc reference, 1.12";


        menu["2.07/Ip27"]="6zX10";
        menu["2.07/Ip27/_description"]="Digital input, 2.07";
        menu["2.07/In27"] ="7zX10";
        menu["2.07/In27/_description"]="Digital Pull-Up inverted input, 2.07";
        menu["2.07/Op27"]="8zX10";
        menu["2.07/Op27/_description"]="Digital output, 2.07";
        menu["2.07/On27"]="9zX10";
        menu["2.07/On27/_description"]="Digital inverted output, 2.07";

        menu["2.06/Ip28"]="6zX11";
        menu["2.06/Ip28/_description"]="Digital input, 2.06";
        menu["2.06/In28"] ="7zX11";
        menu["2.06/In28/_description"]="Digital Pull-Up inverted input, 2.06";
        menu["2.06/Op28"]="8zX11";
        menu["2.06/Op28/_description"]="Digital output, 2.06";
        menu["2.06/On28"]="9zX11";
        menu["2.06/On28/_description"]="Digital inverted output, 2.06";

        menu["2.05/Ip29"]="6zX12";
        menu["2.05/Ip29/_description"]="Digital input, 2.05";
        menu["2.05/In29"] ="7zX12";
        menu["2.05/In29/_description"]="Digital Pull-Up inverted input, 2.05";
        menu["2.05/Op29"]="8zX12";
        menu["2.05/Op29/_description"]="Digital output, 2.05";
        menu["2.05/On29"]="9zX12";
        menu["2.05/On29/_description"]="Digital inverted output, 2.05";
        menu["2.05/Pp29"]="8bX12";
        menu["2.05/Pp29/_description"]="PWM positive, 2.05";
        menu["2.05/Pn29"]="9bX12";
        menu["2.05/Pn29/_description"]="PWM negative, 2.05";

        menu["2.04/Ip30"]="6zX13";
        menu["2.04/Ip30/_description"]="Digital input, 2.04";
        menu["2.04/In30"] ="7zX13";
        menu["2.04/In30/_description"]="Digital Pull-Up inverted input, 2.04";
        menu["2.04/Op30"]="8zX13";
        menu["2.04/Op30/_description"]="Digital output, 2.04";
        menu["2.04/On30"]="9zX13";
        menu["2.04/On30/_description"]="Digital inverted output, 2.04";
        menu["2.04/Pp30"]="8bX13";
        menu["2.04/Pp30/_description"]="PWM positive, 2.04";
        menu["2.04/Pn30"]="9bX13";
        menu["2.04/Pn30/_description"]="PWM negative, 2.04";

        menu["2.03/Ip31"]="6zX14";
        menu["2.03/Ip31/_description"]="Digital input, 2.03";
        menu["2.03/In31"] ="7zX14";
        menu["2.03/In31/_description"]="Digital Pull-Up inverted input, 2.03";
        menu["2.03/Op31"]="8zX14";
        menu["2.03/Op31/_description"]="Digital output, 2.03";
        menu["2.03/On31"]="9zX14";
        menu["2.03/On31/_description"]="Digital inverted output, 2.03";

        menu["Vcc/Av14"]="BwX17";
        menu["Vcc/Av14/_description"]="Analog value, Vcc=1.1*1024/Av14, SV1-06";

        menu["rename"]="YR";
        menu["remove"]="ZD";
        StoreDeclarer(menu, "/system/declarers/PS_Gate", "/CC;component/Images/ty_jgate.png");

        menu["2.09/Ip24"]="6zX7";
        menu["2.09/Ip24/_description"]="Digital input, 2.09";
        menu["2.09/In24"] ="7zX7";
        menu["2.09/In24/_description"]="Digital Pull-Up inverted input, 2.09";
        menu["2.09/Op24"]="8zX7";
        menu["2.09/Op24/_description"]="Digital output, 2.09";
        menu["2.09/On24"]="9zX7";
        menu["2.09/On24/_description"]="Digital inverted output, 2.09";
        menu["2.09/St4"]="AsX7";
        menu["2.09/St4/_description"]="TxD 38400, 2.09";

        menu["2.08/Ip25"]="6zX8";
        menu["2.08/Ip25/_description"]="Digital input, 2.08";
        menu["2.08/In25"] ="7zX8";
        menu["2.08/In25/_description"]="Digital Pull-Up inverted input, 2.08";
        menu["2.08/Op25"]="8zX8";
        menu["2.08/Op25/_description"]="Digital output, 2.08";
        menu["2.08/On25"]="9zX8";
        menu["2.08/On25/_description"]="Digital inverted output, 2.08";
        menu["2.08/Sr4"]="AsX8";
        menu["2.08/Sr4/_description"]="RxD 38400, 2.08";

        StoreDeclarer(menu, "/system/declarers/PS_Node", "/CC;component/Images/ty_jnode.png");
        #endregion uGate/uNode
      }
    }

    private static void StoreDeclarer(SortedList<string, string> menu, string path, string image) {
      if(!Topic.root.Exist(path)) {
        var t1=Topic.root.Get<string>(path);
        t1.saved=true;
        t1.value=image;

        foreach(var mi in menu) {
          var t2=t1.Get<string>(mi.Key);
          t2.saved=true;
          t2.value=mi.Value;
        }
      }
    }

    private int _duration=3000;
    private DVar<MsDeviceState> _stateVar;

    public MsDeviceState state {
      get { return _stateVar!=null?_stateVar.value:MsDeviceState.Disconnected; }
      private set {
        if(_stateVar!=null) {
          try {
            _stateVar.value=value;
          }
          catch(ObjectDisposedException) {
            _stateVar=null;
          }
        }
        if(_present!=null) {
          try {
            _present.value=(state==MsDeviceState.Connected || state==MsDeviceState.ASleep || state==MsDeviceState.AWake);
          }
          catch(ObjectDisposedException) {
            _present=null;
          }
        }
      }
    }
    private string _willPath;
    private byte[] _wilMsg;
    private bool _willRetain;
    private Timer _activeTimer;
    // TODO: Save/Restore _topics & _subsscriptions
    private List<TopicInfo> _topics;
    private List<Topic.Subscription> _subsscriptions;
    private Queue<MsMessage> _sendQueue;
    private int _tryCounter;
    private int _topicIdGen=0;
    private int _messageIdGen=0;
    private byte _addr;
    private DVar<bool> _present;

    internal MsDevice() {
      if(Topic.brokerMode) {
        _activeTimer=new Timer(new TimerCallback(TimeOut));
        _topics=new List<TopicInfo>(16);
        _subsscriptions=new List<Topic.Subscription>(4);
        _sendQueue=new Queue<MsMessage>();
      }
    }

    public byte Addr {
      get { return _addr; }
      set {
        _addr=value;
      }
    }

    public DVar<MsDevice> Owner { get; private set; }

    internal void Connect(MsConnect msg) {
      Addr=msg.Addr;
      _topicIdGen=0;
      if(msg.CleanSession) {
        foreach(var s in _subsscriptions) {
          Owner.Unsubscribe(s.path, s.func);
        }
        _subsscriptions.Clear();
        _topics.Clear();
        lock(_sendQueue) {
          _sendQueue.Clear();
        }
      } else {
        try {
          _topicIdGen=_topics.Where(z => z.it==TopicIdType.Normal).Max(z => z.TopicId);
        }
        catch(InvalidOperationException) {
          _topicIdGen=1;
        }
      }
      _duration=msg.Duration*1100;
      ResetTimer();
      if(msg.Will) {
        state=MsDeviceState.WillTopic;
        _willPath=string.Empty;
        _wilMsg=null;
        Send(new MsMessage(MsMessageType.WILLTOPICREQ));
      } else {
        if(state==MsDeviceState.Disconnected || state==MsDeviceState.Lost) {
          Log.Info("{0}.state={1}->connected", Owner.path, state);
        }
        state=MsDeviceState.Connected;
        Send(new MsConnack(MsReturnCode.Accepted));
      }
    }
    internal void WillTopic(MsWillTopic msg) {
      if(state==MsDeviceState.WillTopic) {
        _willPath=msg.Path;
        _willRetain=msg.Retain;
        state=MsDeviceState.WillMsg;
        ProccessAcknoledge(msg);
      }
    }
    internal void WillMsg(MsWillMsg msg) {
      if(state==MsDeviceState.WillMsg) {
        _wilMsg=msg.Payload;
        state=MsDeviceState.Connected;
        ProccessAcknoledge(msg);
        Send(new MsConnack(MsReturnCode.Accepted));
        Log.Info("{0} connected", Owner.path);
      }
    }
    internal void Register(MsRegister msg) {
      ResetTimer();
      try {
        TopicInfo ti = GetTopicInfo(msg.TopicPath, false);
        Send(new MsRegAck(ti.TopicId, msg.MessageId, MsReturnCode.Accepted));
      }
      catch(Exception) {
        Send(new MsRegAck(0, msg.MessageId, MsReturnCode.NotSupportes));
        Log.Warning("Unknown variable type by register {0}, {1}", Owner.path, msg.TopicPath);
      }
    }
    internal void RegAck(MsRegAck msg) {
      ProccessAcknoledge(msg);
      TopicInfo ti=_topics.FirstOrDefault(z => z.TopicId==msg.TopicId);
      if(ti==null) {
        if(msg.TopicId!=0) {
          Log.Warning("{0} RegAck({1:X4}) for unknown variable", Owner.path, msg.TopicId);
        }
        return;
      }
      if(msg.RetCode==MsReturnCode.Accepted) {
        ti.registred=true;
        if(ti.it!=TopicIdType.PreDefined) {
          Send(new MsPublish(ti.topic, ti.TopicId, QoS.AtLeastOnce));
        }
      } else {
        Log.Warning("{0} registred failed: {1}", ti.path, msg.RetCode.ToString());
        _topics.Remove(ti);
        ti.topic.Remove();
      }
    }
    internal void Subscibe(MsSubscribe msg) {
      SyncMsgId(msg.MessageId);
      Topic.Subscription s=null;
      ushort topicId=msg.topicId;
      if(msg.topicIdType!=TopicIdType.Normal || msg.path.IndexOfAny(new[] { '+', '#' })<0) {
        TopicInfo ti=null;
        if(msg.topicIdType==TopicIdType.Normal) {
          ti=GetTopicInfo(msg.path, false);
        } else {
          ti=GetTopicInfo(msg.topicId, msg.topicIdType);
        }
        topicId=ti.TopicId;
      }
      //if(s!=null) {
        Send(new MsSuback(msg.qualityOfService, topicId, msg.MessageId, MsReturnCode.Accepted));
        s=Owner.Subscribe(msg.path, PublishTopic, msg.qualityOfService);
        _subsscriptions.Add(s);
      //} else {
      //  Send(new MsSuback(QoS.AtMostOnce, topicId, msg.MessageId, MsReturnCode.InvalidTopicId));
      //}
    }
    internal void Publish(MsPublish msg) {
      TopicInfo ti=_topics.Find(z => z.TopicId==msg.TopicId && z.it==msg.topicIdType);
      if(ti==null && msg.topicIdType!=TopicIdType.Normal) {
        ti=GetTopicInfo(msg.TopicId, msg.topicIdType, false);
      }
      if(msg.qualityOfService==QoS.AtMostOnce) {
        ResetTimer();
      } else if(msg.qualityOfService==QoS.AtLeastOnce) {
        SyncMsgId(msg.MessageId);
        Send(new MsPubAck(msg.TopicId, msg.MessageId, ti!=null?MsReturnCode.Accepted:MsReturnCode.InvalidTopicId));
      } else if(msg.qualityOfService==QoS.ExactlyOnce) {
        SyncMsgId(msg.MessageId);
        // QoS2 not supported, use QoS1
        Send(new MsPubAck(msg.TopicId, msg.MessageId, ti!=null?MsReturnCode.Accepted:MsReturnCode.InvalidTopicId));
      } else {
        throw new NotSupportedException("QoS -1 not supported "+Owner.path);
      }
      SetValue(ti, msg.Data);
    }
    //TODO: Unsubscribe
    private void SetValue(TopicInfo ti, byte[] msgData) {
      if(ti!=null) {
        object val;
        var tc=Type.GetTypeCode(ti.topic.valueType);
        switch(tc) {
        case TypeCode.Boolean:
          val=(msgData[0]!=0);
          break;
        case TypeCode.SByte:
          val=(sbyte)msgData[0];
          break;
        case TypeCode.Byte:
          val=msgData[0];
          break;
        case TypeCode.Int16:
          val=(short)((msgData[1]<<8) | msgData[0]);
          break;
        case TypeCode.UInt16:
          val=(ushort)((msgData[1]<<8) | msgData[0]);
          break;
        case TypeCode.Int32:
          val=(int)((msgData[3]<<24) | (msgData[2]<<16) | (msgData[1]<<8) | msgData[0]);
          break;
        case TypeCode.UInt32:
          val=((msgData[3]<<24) | (msgData[2]<<16) | (msgData[1]<<8) | msgData[0]);
          break;
        case TypeCode.String:
          val=Encoding.UTF8.GetString(msgData);
          break;
        case TypeCode.Object:
          if(ti.topic.valueType==typeof(byte[])) {
            val=msgData;
            break;
          } else {
            return;
          }
        default:
          return;
        }
        ti.topic.SetValue(val, new TopicChanged(TopicChanged.ChangeArt.Value, Owner));
      }
    }
    internal void PubAck(MsPubAck msg) {
      ProccessAcknoledge(msg);
    }
    internal void PingReq(MsPingReq msg) {
      if(state==MsDeviceState.ASleep) {
        if(string.IsNullOrEmpty(msg.ClientId) || msg.ClientId==Owner.name) {
          state=MsDeviceState.AWake;
          ProccessAcknoledge(msg);    // resume send proccess
        } else {
          Send(new MsDisconnect());
          state=MsDeviceState.Lost;
          Log.Warning("{0} PingReq from unknown device: {1}", Owner.path, msg.ClientId);
        }
      } else {
        ResetTimer();
        Send(new MsMessage(MsMessageType.PINGRESP));
      }
    }
    internal void Disconnect(ushort duration=0) {
      if(!string.IsNullOrEmpty(_willPath)) {
        TopicInfo ti = GetTopicInfo(_willPath, false);
        SetValue(ti, _wilMsg);
      }
      if(duration>0) {
        ResetTimer(duration*1550);
        this.Send(new MsDisconnect());
        _tryCounter=0;
        state=MsDeviceState.ASleep;
        var st=Owner.Get<short>(PredefinedTopics._WSleepTime.ToString(), Owner);
        st.saved=true;
        st.SetValue((short)duration, new TopicChanged(TopicChanged.ChangeArt.Value, Owner){ Source=st });
      } else if(state!=MsDeviceState.Lost) {
        state=MsDeviceState.Disconnected;
        if(Owner!=null) {
          Log.Info("{0} Disconnected", Owner.path);
        }
        _activeTimer.Dispose();
        _activeTimer=null;
      }
    }
    private void OwnerChanged(Topic topic, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Remove) {
        Send(new MsDisconnect());
        state=MsDeviceState.Disconnected;
        return;
      }
    }

    internal void PublishTopic(Topic topic, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Add) {
        var ti=GetTopicInfo(topic);
        return;
      }
      if(state==MsDeviceState.Disconnected || state==MsDeviceState.Lost || param.Visited(Owner, true)) {
        return;
      }
      TopicInfo rez=_topics.FirstOrDefault(ti => ti.path==topic.path);
      if(rez==null && param.Art==TopicChanged.ChangeArt.Value) {
        rez=GetTopicInfo(topic, true);
      }
      if(rez==null || rez.TopicId>=0xFF00 || rez.TopicId==0xFE00 || !rez.registred) {
        return;
      }
      if(param.Art==TopicChanged.ChangeArt.Value) {
          Send(new MsPublish(rez.topic, rez.TopicId, param.Subscription.qos));
      } else {          // Remove by device
        Send(new MsRegister(0, rez.path.StartsWith(Owner.path)?rez.path.Remove(0, Owner.path.Length+1):rez.path));
        _topics.Remove(rez);
      }
    }

    /// <summary>Find or create TopicInfo by Topic</summary>
    /// <param name="tp">Topic as key</param>
    /// <param name="sendRegister">Send MsRegister for new TopicInfo</param>
    /// <returns>found TopicInfo or null</returns>
    private TopicInfo GetTopicInfo(Topic tp, bool sendRegister=true) {
      if(tp==null) {
        return null;
      }
      TopicInfo rez=_topics.FirstOrDefault(ti => ti.path==tp.path);
      string tpc=(tp.path.StartsWith(Owner.path))?tp.path.Remove(0, Owner.path.Length+1):tp.path;
      if(rez==null) {
        rez=new TopicInfo();
        rez.topic=tp;
        rez.path=tp.path;
        PredefinedTopics rtId;
        if(Enum.TryParse(tpc, false, out rtId)) {
          rez.TopicId=(ushort)rtId;
          rez.it=TopicIdType.PreDefined;
          rez.registred=true;
        } else {
          rez.TopicId=(ushort)Interlocked.Increment(ref _topicIdGen);
          rez.it=TopicIdType.Normal;
        }
        _topics.Add(rez);
      }
      if(!rez.registred) {
        if(sendRegister) {
          Send(new MsRegister(rez.TopicId, tpc));
        } else {
          rez.registred=true;
        }
      }
      return rez;
    }
    private TopicInfo GetTopicInfo(string path, bool sendRegister=true) {
      Topic cur=null;
      int idx=path.LastIndexOf('/');
      string cName=path.Substring(idx+1);

      var rec=_NTTable.FirstOrDefault(z => cName.StartsWith(z.name));
      TopicInfo ret;
      if(rec.name!=null) {
        cur=Topic.GetP(path, rec.type, Owner, Owner);
        ret=GetTopicInfo(cur, sendRegister);
      } else {
        ret=null;
      }
      return ret;
    }
    private TopicInfo GetTopicInfo(ushort topicId, TopicIdType topicIdType, bool sendRegister=true) {
      TopicInfo rez=_topics.Find(z => z.it==topicIdType && z.TopicId==topicId);
      if(rez==null) {
        if(topicIdType==TopicIdType.PreDefined) {
          PredefinedTopics a=(PredefinedTopics)topicId;
          if(Enum.IsDefined(typeof(PredefinedTopics), a)) {
            string cPath=Enum.GetName(typeof(PredefinedTopics), a);
            rez=GetTopicInfo(cPath, sendRegister);
          }
        } else if(topicIdType==TopicIdType.ShortName) {
          rez=GetTopicInfo(string.Format("{0}{1}", (char)(topicId>>8), (char)(topicId & 0xFF)), sendRegister);
        }
        if(rez!=null) {
          rez.it=topicIdType;
        }
      }
      return rez;
    }
    private ushort NextMsgId() {
      int rez=Interlocked.Increment(ref _messageIdGen);
      Interlocked.CompareExchange(ref _messageIdGen, 1, 0xFFFF);
      //Log.Debug("{0}.MsgId={1:X4}", Owner.name, rez);
      return (ushort)rez;
    }
    private void SyncMsgId(ushort p) {
      ResetTimer();
      int nid=p;
      if(nid==0xFFFE) {
        nid++;
        nid++;
      }
      if(nid>(int)_messageIdGen || (nid<0x0100 && _messageIdGen>0xFF00)) {
        _messageIdGen=(ushort)nid;      // synchronize messageId
      }
      //Log.Debug("{0}.MsgIdGen={1:X4}, p={2:X4}", Owner.name, _messageIdGen, p);
    }

    private void ProccessAcknoledge(MsMessage rMsg) {
      ResetTimer();
      MsMessage msg=null;
      lock(_sendQueue) {
        MsMessage reqMsg;
        if(_sendQueue.Count>0 && (reqMsg=_sendQueue.Peek()).MsgTyp==rMsg.ReqTyp && reqMsg.MessageId==rMsg.MessageId) {
          _sendQueue.Dequeue();
        }
        if(_sendQueue.Count>0 && !(msg=_sendQueue.Peek()).IsRequest) {
          _sendQueue.Dequeue();
        }
      }
      if(msg!=null || state==MsDeviceState.AWake) {
        if(msg!=null && msg.IsRequest) {
          _tryCounter=2;
        }
        SendIntern(msg);
      }
    }
    private void Send(MsMessage msg) {
      if((state!=MsDeviceState.Disconnected && state!=MsDeviceState.Lost) || (msg.MsgTyp==MsMessageType.DISCONNECT || msg.MsgTyp==MsMessageType.PINGRESP)) {
        msg.Addr=this.Addr;
        bool send=true;
        if(msg.MessageId==0 && (msg.MsgTyp==MsMessageType.PUBLISH?(msg as MsPublish).qualityOfService!=QoS.AtMostOnce:msg.IsRequest)) {
          msg.MessageId=NextMsgId();
          lock(_sendQueue) {
            if(_sendQueue.Count>0 || state==MsDeviceState.ASleep) {
              send=false;
            }
            _sendQueue.Enqueue(msg);
          }
        }
        if(send) {
          if(msg.IsRequest) {
            _tryCounter=2;
          }
          SendIntern(msg);
        }
      }
    }
    private void SendIntern(MsMessage msg) {
      MsGateway g;
      if(Owner==null || (g=(Owner.parent as DVar<MsGateway>).value)==null) {
        return;
      }
      while((msg!=null ||state==MsDeviceState.AWake) && (state!=MsDeviceState.ASleep || (msg.MsgTyp==MsMessageType.DISCONNECT || msg.MsgTyp==MsMessageType.PINGRESP))) {
        if(msg!=null) {
          g.Send(msg);
        }
        if(msg!=null && msg.IsRequest) {
          ResetTimer(ACK_TIMEOUT);
          break;
        } else {
          msg=null;
          lock(_sendQueue) {
            if(_sendQueue.Count==0 && state==MsDeviceState.AWake) {
              g.Send(new MsMessage(MsMessageType.PINGRESP) { Addr=this.Addr });
              state=MsDeviceState.ASleep;
              break;
            }
            if(_sendQueue.Count>0 && !(msg=_sendQueue.Peek()).IsRequest) {
              _sendQueue.Dequeue();
            //} else if(msg!=null && msg.MsgTyp==MsMessageType.PUBLISH && (msg as MsPublish).Dup) {
            //  break;
            }
          }
        }
      }
    }
    private void ResetTimer(int period=0) {
      if(period==0) {
        if(_sendQueue.Count>0) {
          period=ACK_TIMEOUT;
        } else if(_duration>0) {
          period=_duration;
          _tryCounter=1;
        }
      }
      //Log.Debug("$ {0}._activeTimer={1}", Owner.name, period);
      _activeTimer.Change(period, Timeout.Infinite);
    }
    private void TimeOut(object o) {
      //Log.Debug("$ {0}.TimeOut _tryCounter={1}", Owner.name, _tryCounter);
      if(_tryCounter>0) {
        MsMessage msg=null;
        lock(_sendQueue) {
          if(_sendQueue.Count>0) {
            msg=_sendQueue.Peek();
          }
        }
        if(msg!=null) {
          SendIntern(msg);
          _tryCounter--;
        } else {
          ResetTimer();
          _tryCounter=0;
        }
        return;
      }
      state=MsDeviceState.Lost;
      if(Owner!=null) {
        Disconnect();
        Log.Warning("{0} Lost", Owner.path);
      }
      lock(_sendQueue) {
        _sendQueue.Clear();
      }
      SendIntern(new MsDisconnect() { Addr=this.Addr });
    }

    #region ITopicOwned Members
    void ITopicOwned.SetOwner(Topic owner) {
      if(Owner!=owner) {
        if(Owner!=null) {
          if(_subsscriptions!=null) {
            foreach(var s in _subsscriptions) {
              Owner.Unsubscribe(s.path, s.func);
            }
            _subsscriptions.Clear();
          }
          _stateVar=null;
        }
        Owner=owner as DVar<MsDevice>;
        if(Topic.brokerMode && Owner!=null) {
          Owner.saved=true;
          _stateVar=Owner.Get<MsDeviceState>(PredefinedTopics._state.ToString());
          var dc=Owner.Get<string>(PredefinedTopics._declarer.ToString(), Owner);
          dc.saved=true;
          dc.value=_declarer;
          var st=Owner.Get<short>(PredefinedTopics._WSleepTime.ToString(), Owner);
          st.saved=true;
          _present=Owner.Get<bool>(PredefinedTopics.present.ToString(), Owner);
          _present.value=(state==MsDeviceState.Connected || state==MsDeviceState.ASleep || state==MsDeviceState.AWake);

          if(!string.IsNullOrEmpty(backName) && backName!=Owner.name && Owner.parent.Exist(backName)) {   // Device renamed
            var old=Owner.parent.Get<MsDevice>(backName);
            if(old!=null && old.value!=null) {
              _addr=old.value._addr;
              _stateVar.value=old.value._stateVar.value;
              Send(new MsPublish(null, (ushort)PredefinedTopics._sName, QoS.AtLeastOnce) { Data=Encoding.UTF8.GetBytes(Owner.name.Substring(0, Owner.name.Length)) });
              Send(new MsDisconnect());
            }
          }
          backName=Owner.name;
        }
      }
    }
    #endregion ITopicOwned Members

    private string _declarer="RF12_Default";

    [Newtonsoft.Json.JsonProperty]
    private string backName { get; set; }

    private class TopicInfo {
      public Topic topic;
      public ushort TopicId;
      public TopicIdType it;
      public bool registred;
      public string path;
    }
    private static NTRecord[] _NTTable= new NTRecord[]{ 
                                          new NTRecord("In", typeof(bool)),
                                          new NTRecord("Ip", typeof(bool)),
                                          new NTRecord("Op", typeof(bool)),
                                          new NTRecord("On", typeof(bool)),
                                          new NTRecord("Ai", typeof(short)),
                                          new NTRecord("Av", typeof(short)),
                                          new NTRecord("Ae", typeof(short)),
                                          new NTRecord("_B", typeof(byte)),
                                          new NTRecord("Pp", typeof(byte)),   // PWM positive[29, 30]
                                          new NTRecord("Pn", typeof(byte)),   // PWM negative[29, 30]
                                          new NTRecord("_W", typeof(short)),
                                          new NTRecord("_s", typeof(string)),
                                          new NTRecord("St", typeof(string)),  // Serial port transmit
                                          new NTRecord("Sr", typeof(string)),  // Serial port recieve
                                          new NTRecord("Tz", typeof(bool)),
                                          new NTRecord("Tb", typeof(sbyte)),
                                          new NTRecord("TB", typeof(byte)),
                                          new NTRecord("Tw", typeof(short)),
                                          new NTRecord("TW", typeof(ushort)),
                                          new NTRecord("Td", typeof(int)),
                                          new NTRecord("TD", typeof(uint)),
                                          new NTRecord("Ts", typeof(string)),
                                          new NTRecord("Ta", typeof(byte[])),
                                          new NTRecord("Xz", typeof(bool)),   // user defined
                                          new NTRecord("Xb", typeof(sbyte)),
                                          new NTRecord("XB", typeof(byte)),
                                          new NTRecord("Xw", typeof(short)),
                                          new NTRecord("XW", typeof(ushort)),
                                          new NTRecord("Xd", typeof(int)),
                                          new NTRecord("XD", typeof(uint)),
                                          new NTRecord("Xs", typeof(string)),
                                          new NTRecord("Xa", typeof(byte[])),
                                          new NTRecord(PredefinedTopics._declarer.ToString(), typeof(string)),
                                          new NTRecord(PredefinedTopics.present.ToString(), typeof(bool)),
                                        };
    private struct NTRecord {
      public NTRecord(string name, Type type) {
        this.name=name;
        this.type=type;
      }
      public readonly string name;
      public readonly Type type;
    }
  }

  internal enum TopicIdType {
    Normal=0,
    PreDefined=1,
    ShortName=2
  }
  internal enum PredefinedTopics : ushort {
    _declarer=0xFE00,
    _DeviceAddr=0xFE01,
    _WGroupID=0xFE02,
    _BChannel=0xFE03,
    _sName=0xFE04,
    _WSleepTime=0xFE05,
    _BRSSI=0xFE08,
    _state=0xFF01,
    present=0xFF02,
  }
  public enum MsDeviceState {
    Disconnected=0,
    WillTopic,
    WillMsg,
    Connected,
    ASleep,
    AWake,
    Lost,
  }
}
