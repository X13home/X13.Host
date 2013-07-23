/**
 * String.format for JavaScript
 * Copyright (c) Daniel Mester Pirttijärvi 2009
 * 
 * www.masterdata.se/r/string_format_for_javascript/
 */

var msf={};(function(){function C(E){return(E<10?"0":"")+E}function D(E){E=E.toLowerCase();var F={name:"en-GB",d:"dd/MM/yyyy",D:"dd MMMM yyyy",t:"HH:mm",T:"HH:mm:ss",M:"d MMMM",Y:"MMMM yyyy",s:"yyyy-MM-ddTHH:mm:ss",_m:["January","February","March","April","May","June","July","August","September","October","November","December"],_d:["Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday"],_r:".",_t:",",_c:"£#,0.00",_ct:",",_cr:"."};if(E.substr(0,2)=="sv"){F.name="sv-SE";F.d="yyyy-MM-dd";F.D="den dd MMMM yyyy";F._m=["januari","februari","mars","april","maj","juni","juli","augusti","september","oktober","november","december"];F._d=["söndag","måndag","tisdag","onsdag","torsdag","fredag","lördag"];F._r=",";F._t=" ";F._ct=".";F._cr=",";F._c="#,0.00 kr"}else{if(E!="en-gb"){F.name="en-US";F.t="hh:mm tt";F.T="hh:mm:ss tt";F.d="MM/dd/yyyy";F.D="MMMM dd, yyyy";F.Y="MMMM, yyyy";F._c="$#,0.00"}}F.f=F.D+" "+F.t;F.F=F.D+" "+F.T;F.g=F.d+" "+F.t;F.G=F.d+" "+F.T;return F}function B(L,Q){var S=0,P=-1,H=-1,I=0,E=0,N=-1,F=false,O=true,J=[],G,K;function R(T){for(var U=0;U<T.length;U++){J.push(T.charAt(U));if(I>1&&I--%3==1){J.push(Q.t)}}}for(K=0;K<Q.f.length;K++){G=Q.f.charAt(K);E+=F;if(G=="0"){if(F){N=E}else{if(P<0){P=S}}}S+=!F&&(G=="0"||G=="#");F=F||G=="."}P=P<0?1:S-P;if(L<0){J.push("-")}L=(Math.round(Math.abs(L)*Math.pow(10,E))/Math.pow(10,E)).toString();H=L.indexOf(".");H=H<0?L.length:H;K=H-S;if(Q.f.match(/^[^\.]*[0#],[0#]/)){I=Math.max(H,P)}for(var M=0;M<Q.f.length;M++){G=Q.f.charAt(M);if(G=="#"||G=="0"){if(K<H){if(K>=0){if(O){R(L.substr(0,K))}R(L.charAt(K))}else{if(K>=H-P){R("0")}}O=false}else{if(N-->0||K<L.length){R(K>=L.length?"0":L.charAt(K))}}K++}else{if(G=="."){if(L.length>++K||N>0){J.push(Q.r)}}else{if(G!==","){J.push(G)}}}}return J.join("")}Number.prototype.__Format=function(F){var H=Number(this);if(F=="X"){return Math.round(H).toString(16).toUpperCase()}else{if(F=="x"){return Math.round(H).toString(16)}else{var G={t:msf.LC._t,r:msf.LC._r};var J="0.################",I=F?F.toLowerCase():null;if(I===null||I=="g"){F=J}else{if(I=="n"){F="#,"+J}else{if(I=="c"){F=msf.LC._c;G.r=msf.LC._cr;G.t=msf.LC._ct}else{if(I=="f"){F="0.00"}}}}if(F.indexOf(",.")!==-1){H/=1000}if(F.indexOf("%")!==-1){H*=100}var E=F.split(";");if(H<0&&E.length>1){H*=-1;G.f=E[1]}else{G.f=E[!H&&E.length>2?2:0]}return B(H,G)}}};Date.prototype.__Format=function(F){var G=this;var E="";var H;if(F.length==1){F=msf.LC[F]}return F.replace(/(d{1,4}|M{1,4}|yyyy|yy|HH|H|hh|h|mm|m|ss|s|tt)/g,function(){switch(arguments[0]){case"dddd":return msf.LC._d[G.getDay()];case"ddd":return msf.LC._d[G.getDay()].substr(0,3);case"dd":return C(G.getDate());case"d":return G.getDate();case"MMMM":return msf.LC._m[G.getMonth()];case"MMM":return msf.LC._m[G.getMonth()].substr(0,3);case"MM":return C(G.getMonth()+1);case"M":return G.getMonth()+1;case"yyyy":return G.getFullYear();case"yy":return G.getFullYear().toString().substr(2);case"HH":return C(G.getHours());case"hh":return C((G.getHours()-1)%12+1);case"H":return G.getHours();case"h":return(G.getHours()-1)%12+1;case"mm":return C(G.getMinutes());case"m":return G.getMinutes();case"ss":return C(G.getSeconds());case"s":return G.getSeconds();case"tt":return G.getHours()<12?"AM":"PM";default:return""}})};String.__Format=function(I,J,H,F){var G=arguments,E;return I.replace(/(\{*)\{((\d+)(\,(-?\d*))?(\:([^\}]*))?)\}/g,function(){var K=arguments;if(K[1]&&K[1].length%2==1){return K[0]}if((E=G[parseInt(K[3],10)+1])===undefined){throw"Missing argument"}var N=E.__Format?E.__Format(K[7]):E.toString();var L=parseInt(K[5],10)||0;var O=Math.abs(L)-N.length;if(O>0){var M=" ";while(M.length<O){M+=" "}N=L>0?N+M:M+N}return K[1]+N}).replace(/\{\{/g,"{")};msf.LC=null;msf.setCulture=function(E){msf.LC=D(E)||D(E.substr(0,2))||D()};msf.setCulture(navigator.systemLanguage||navigator.language||"en-US");var A=Date.prototype;A.format=A.format||A.__Format;A=Number.prototype;A.format=A.format||A.__Format;String.format=String.format||String.__Format})();