using System;
using System.Collections.Generic;
using Jurassic.Compiler;

namespace Jurassic.Library {
  /// <summary>
  /// Represents the built-in JSON object.
  /// </summary>
  [Serializable]
  public class JSONObject : ObjectInstance {

    //     INITIALIZATION
    //_________________________________________________________________________________________

    /// <summary>
    /// Creates a new JSON object.
    /// </summary>
    /// <param name="prototype"> The next object in the prototype chain. </param>
    internal JSONObject(ObjectInstance prototype)
      : base(prototype) {
    }



    //     .NET ACCESSOR PROPERTIES
    //_________________________________________________________________________________________

    /// <summary>
    /// Gets the internal class name of the object.  Used by the default toString()
    /// implementation.
    /// </summary>
    protected override string InternalClassName {
      get { return "JSON"; }
    }
    private static System.Text.RegularExpressions.Regex _dtRegEx;
    internal static IEnumerable<Token> ParseLex(ScriptEngine engine, string text) {
      if(string.IsNullOrEmpty(text)) {
        yield break;
      }
      int line=1;
      int row=1;
      int startRow=1;
      int pos=0;
      int startPos=0;
      int state=0;
      int subSt=0;
      char ch;
      while(text.Length>pos || (text.Length==pos && state!=0)) {
        ch=text.Length>pos?text[pos]:'\n';
        switch(state) {
        case 0:     // init
          startPos=pos;
          startRow=row;
          switch(ch) {
          case '\t': // whitespace
          case '\n':
          case '\r':
          case ' ':
            state=0;
            break;
          case '0':
            state=1;
            break;
          case '1':
          case '2':
          case '3':
          case '4':
          case '5':
          case '6':
          case '7':
          case '8':
          case '9':
            state=2;
            break;
          case '-':
            state=14;
            break;
          case '"':
            state=6;
            subSt=1;
            break;
          case '\'':
            state=6;
            subSt=2;
            break;
          case '{':
            yield return PunctuatorToken.LeftBrace;
            break;
          case '[':
            yield return PunctuatorToken.LeftBracket;
            break;
          case '}':
            yield return PunctuatorToken.RightBrace;
            break;
          case ']':
            yield return PunctuatorToken.RightBracket;
            break;
          case ',':
            yield return PunctuatorToken.Comma;
            break;
          case ':':
            yield return PunctuatorToken.Colon;
            break;
          default:
            if(ch=='_' || char.IsLetter(ch)) {
              state=30;
            } else {
              throw new JavaScriptException(engine, "SyntaxError", string.Format("Unexpected character '{0}'@{1}:{2}.", ch, line, row));
            }
            break;
          }
          break;
        case 1:     // integer, 0
          if(",}] \t\n\r".IndexOf(ch)>=0) {
            yield return new LiteralToken(0L);
            state=0;
            goto case 0;
          } else if(ch=='x' || ch=='X') {
            throw new JavaScriptException(engine, "SyntaxError", "Hexidecimal literals are not supported in JSON. @"+line.ToString()+":"+row.ToString());
          } else if(char.IsDigit(ch)) {
            throw new JavaScriptException(engine, "SyntaxError", "Octal literals are not supported in JSON. @"+line.ToString()+":"+row.ToString());
          } else if(ch=='.') {
            state=13;
          } else if(ch=='E' || ch=='e') {
            state=5;
            subSt=0;
          } else {
            throw new JavaScriptException(engine, "SyntaxError", string.Format("Invalid number \"{0}\"@{1}:{2}", text.Substring(startPos, pos-startPos), line, row));
          }
          break;
        case 2:   //integer
          if(",}] \t\n\r".IndexOf(ch)>=0) {
            yield return new LiteralToken(long.Parse(text.Substring(startPos, pos-startPos)));
            state=0;
            goto case 0;
          } else if(char.IsDigit(ch)) {
            state=2;
          } else if(ch=='.') {
            state=13;
          } else if(ch=='E' || ch=='e') {
            state=5;
            subSt=0;
          } else {
            throw new JavaScriptException(engine, "SyntaxError", string.Format("Invalid number \"{0}\"@{1}:{2}", text.Substring(startPos, pos-startPos), line, row));
          }
          break;
        case 3: // empty
          throw new NotImplementedException();
        case 4:   //float
          if(",}] \t\n\r".IndexOf(ch)>=0) {
            double dv;
            if(double.TryParse(text.Substring(startPos, pos-startPos), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dv)) {
              yield return new LiteralToken(dv);
              state=0;
              goto case 0;
            } else {
              throw new JavaScriptException(engine, "SyntaxError", string.Format("Invalid number \"{0}\"@{1}:{2}", text.Substring(startPos, pos-startPos), line, row));
            }
          } else if(char.IsDigit(ch)) {
            state=4;
          } else if(ch=='E' || ch=='e') {
            state=5;
            subSt=0;
          } else {
            throw new JavaScriptException(engine, "SyntaxError", string.Format("Invalid number \"{0}\"@{1}:{2}", text.Substring(startPos, pos-startPos), line, row));
          }
          break;
        case 5:  // Exponent
          if(subSt==-1) {
            if(char.IsDigit(ch)) {
              throw new JavaScriptException(engine, "SyntaxError", string.Format("Invalid number \"{0}\"@{1}:{2}", text.Substring(startPos, pos-startPos), line, row));
            } else {
              state=4;
              goto case 4;
            }
          } else if((subSt==0 || subSt==1) && ch=='0') {
            subSt=-1;
          } else if(subSt==0 && (ch=='+' || ch=='-')) {
            subSt=1;
          } else if(char.IsDigit(ch)) {
            state=4;
          } else {
            throw new JavaScriptException(engine, "SyntaxError", string.Format("Invalid number \"{0}\"@{1}:{2}", text.Substring(startPos, pos-startPos), line, row));
          }
          break;
        case 6:   // string
          if((subSt==1 && ch=='"') || (subSt==2 && ch=='\'')) {
            string s=System.Text.RegularExpressions.Regex.Unescape(text.Substring(startPos+1, pos-startPos-1));
            if(_dtRegEx==null) {
              _dtRegEx=new System.Text.RegularExpressions.Regex(@"^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2}(?:\.\d*))(?:Z|(\+|-)([\d|:]*))?$");
            }
            if(s.Length>18 && s.Length<25 && s[10]=='T' && _dtRegEx.IsMatch(s)) {
              yield return new LiteralToken(DateParser.Parse(s));
            } else {
              yield return new LiteralToken(s);
            }
            state=0;
          } else if(ch=='\\') {
            state=7;
          } else if(ch=='\t' || !char.IsControl(ch)) {
            state=6;
          } else {
            throw new JavaScriptException(engine, "SyntaxError", string.Format("Unexpected character in string literal \"{0}\"@{1}:{2}", text.Substring(startPos, pos-startPos), line, row));
          }
          break;
        case 7: // string escaped char
          if("'\"\\/bfnrt0".IndexOf(ch)>=0) {
            state=6;
          } else if(ch=='U' || ch=='u') {
            state=8;
          } else {
            throw new JavaScriptException(engine, "SyntaxError", string.Format("Unexpected character in escape sequence. \"{0}\"@{1}:{2}", text.Substring(startPos, pos-startPos), line, row));
          }
          break;
        case 8: // string unicode
        case 9:
        case 10:
        case 11:
          if(char.IsDigit(ch) || (ch>='A' && ch<='F') || (ch>='a' && ch<='f')) {
            state=state==11?6:state+1;
          } else {
            throw new JavaScriptException(engine, "SyntaxError", string.Format("Unexpected character in escape sequence. \"{0}\"@{1}:{2}", text.Substring(startPos, pos-startPos), line, row));
          }
          break;
        case 13:    // point
          if(char.IsDigit(ch)) {
            state=4;
          } else {
            throw new JavaScriptException(engine, "SyntaxError", string.Format("Unexpected character '{0}'@{1}:{2}.", ch, line, row));
          }
          break;
        case 14:    // -
          if(ch=='0') {
            state=1;
          }else if(char.IsDigit(ch)) {
            state=2;
          } else {
            throw new JavaScriptException(engine, "SyntaxError", string.Format("Unexpected character '{0}'@{1}:{2}.", ch, line, row));
          }
          break;
        case 30:
          if(ch=='_' || char.IsLetterOrDigit(ch)) {
            state=30;
          } else if("{}[]().,+-*/%&|^!<>?:;~= \t\r\n".IndexOf(ch)>=0) {
            string keyword = text.Substring(startPos, pos-startPos);
            if(keyword == Null.NullString) {
              yield return LiteralToken.Null;
            } else if(keyword == BooleanConstructor.FalseString) {
              yield return LiteralToken.False;
            } else if(keyword == BooleanConstructor.TrueString) {
              yield return LiteralToken.True;
            } else {
              throw new JavaScriptException(engine, "SyntaxError", string.Format("Unexpected keyword '{0}'@{1}:{2}", keyword, line, row));
            }
            goto case 0;
          } else {
            throw new JavaScriptException(engine, "SyntaxError", string.Format("Unexpected character in keyword '{0}'@{1}:{2}", text.Substring(startPos, pos-startPos), line, row));
          }
          break;
        }
        if(ch=='\n') {
          line++;
          row=1;
        } else {
          row++;
        }
        pos++;
      }
    }

    //     JAVASCRIPT FUNCTIONS
    //_________________________________________________________________________________________

    /// <summary>
    /// Parses the JSON source text and transforms it into a value.
    /// </summary>
    /// <param name="text"> The JSON text to parse. </param>
    /// <param name="reviver"> A function that will be called for each value. </param>
    /// <returns> The value of the JSON text. </returns>
    [JSInternalFunction(Name = "parse", Flags = JSFunctionFlags.HasEngineParameter)]
    public static object Parse(ScriptEngine engine, string text, [DefaultParameterValue(null)] object reviver = null) {
      //var parser = new JSONParser(engine, new JSONLexer(engine, new System.IO.StringReader(text)));
      var parser = new JSONParser(engine, ParseLex(engine, text).GetEnumerator());
      parser.ReviverFunction = reviver as FunctionInstance;
      return parser.Parse();
    }

    /// <summary>
    /// Serializes a value into a JSON string.
    /// </summary>
    /// <param name="value"> The value to serialize. </param>
    /// <param name="replacer"> Either a function that can transform each value before it is
    /// serialized, or an array of the names of the properties to serialize. </param>
    /// <param name="spacer"> Either the number of spaces to use for indentation, or a string
    /// that is used for indentation. </param>
    /// <returns> The JSON string representing the value. </returns>
    [JSInternalFunction(Name = "stringify", Flags = JSFunctionFlags.HasEngineParameter)]
    public static string Stringify(ScriptEngine engine, object value, [DefaultParameterValue(null)] object replacer = null, [DefaultParameterValue(null)] object spacer = null) {
      var serializer = new JSONSerializer(engine);

      // The replacer object can be either a function or an array.
      serializer.ReplacerFunction = replacer as FunctionInstance;
      if(replacer is ArrayInstance) {
        var replacerArray = (ArrayInstance)replacer;
        var serializableProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach(object elementValue in replacerArray.ElementValues) {
          if(elementValue is string || elementValue is int || elementValue is double || elementValue is StringInstance || elementValue is NumberInstance)
            serializableProperties.Add(TypeConverter.ToString(elementValue));
        }
        serializer.SerializableProperties = serializableProperties;
      }

      // The spacer argument can be the number of spaces or a string.
      if(spacer is NumberInstance)
        spacer = ((NumberInstance)spacer).Value;
      else if(spacer is StringInstance)
        spacer = ((StringInstance)spacer).Value;
      if(spacer is double)
        serializer.Indentation = new string(' ', Math.Max(Math.Min(TypeConverter.ToInteger((double)spacer), 10), 0));
      else if(spacer is int)
        serializer.Indentation = new string(' ', Math.Max(Math.Min(TypeConverter.ToInteger((int)spacer), 10), 0));
      else if(spacer is string)
        serializer.Indentation = ((string)spacer).Substring(0, Math.Min(((string)spacer).Length, 10));

      // Serialize the value.
      return serializer.Serialize(value);
    }

  }
}
