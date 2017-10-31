﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SplitAndMerge
{
  public partial class Utils
  {
    public static Variable GetItem(ParsingScript script)
    {
      script.MoveForwardIf(Constants.NEXT_ARG, Constants.SPACE);
      Utils.CheckNotEnd(script);

      Variable value = new Variable();
      bool inQuotes = script.Current == Constants.QUOTE;

      if (script.Current == Constants.START_GROUP) {
        // We are extracting a list between curly braces.
        script.Forward(); // Skip the first brace.
        bool isList = true;
        value.Tuple = GetArgs(script,
              Constants.START_GROUP, Constants.END_GROUP, out isList);
        return value;
      } else {
        // A variable, a function, or a number.
        Variable var = script.Execute(Constants.NEXT_OR_END_ARRAY);
        value = var.Clone();
      }

      if (inQuotes) {
        script.MoveForwardIf(Constants.QUOTE);
      }
      script.MoveForwardIf(Constants.END_ARG, Constants.SPACE);
      return value;
    }

    public static string GetToken(ParsingScript script, char[] to)

    {
      char curr = script.TryCurrent();
      char prev = script.TryPrev();

      if (!to.Contains(Constants.SPACE)) {
        // Skip a leading space unless we are inside of quotes
        while (curr == Constants.SPACE && prev != Constants.QUOTE) {
          script.Forward();
          curr = script.TryCurrent();
          prev = script.TryPrev();
        }
      }

      // String in quotes
      bool inQuotes = curr == Constants.QUOTE;
      if (inQuotes) {
        int qend = script.Find(Constants.QUOTE, script.Pointer + 1);
        if (qend == -1) {
          throw new ArgumentException("Unmatched quotes in [" +
                                 script.FromPrev() + "]");
        }
        string result = script.Substr(script.Pointer + 1, qend - script.Pointer - 1);
        script.Pointer = qend + 1;
        return result;
      }

      script.MoveForwardIf(Constants.QUOTE);

      int end = script.FindFirstOf(to);
      end = end < 0 ? script.Size() : end;

      // Skip found characters that have a backslash before.
      while (end > 0 && end + 1 < script.Size() &&
             script.String[end - 1] == '\\') {
        end = script.FindFirstOf(to, end + 1);
      }

      end = end < 0 ? script.Size() : end;

      if (script.At(end - 1) == Constants.QUOTE) {
        end--;
      }

      string var = script.Substr(script.Pointer, end - script.Pointer);
      // \"yes\" --> "yes"
      var = var.Replace("\\\"", "\"");
      script.Pointer = end;

      script.MoveForwardIf(Constants.QUOTE, Constants.SPACE);

      return var;
    }

    public static string GetNextToken(ParsingScript script)
    {
      if (!script.StillValid()) {
        return "";
      }
      int end = script.FindFirstOf(Constants.TOKEN_SEPARATION);

      if (end < 0) {
        return "";
      }

      string var = script.Substr(script.Pointer, end - script.Pointer);
      script.Pointer = end;
      return var;
    }

    public static void SkipRestExpr(ParsingScript script)
    {
      int argRead = 0;
      bool inQuotes = false;
      char previous = Constants.EMPTY;

      while (script.StillValid()) {
        char currentChar = script.Current;
        if (inQuotes && currentChar != Constants.QUOTE) {
          script.Forward();
          continue;
        }

        switch (currentChar) {
          case Constants.QUOTE:
            if (previous != '\\') {
              inQuotes = !inQuotes;
            }
            break;
          case Constants.START_ARG:
            argRead++;
            break;
          case Constants.END_ARG:
            argRead--;
            if (argRead < 0) {
              return;
            }
            break;
          case Constants.END_STATEMENT:
            return;
          case Constants.NEXT_ARG:
            if (argRead <= 0) {
              return;
            }
            break;
          default:
            break;
        }

        script.Forward();
        previous = currentChar;
      }
    }

    public static string GetStringOrVarValue(ParsingScript script)
    {
      script.MoveForwardIf(Constants.SPACE);

      // If this token starts with a quote then it is a string constant.
      // Otherwide we treat it as a variable, but if the variable doesn't exist then it
      // will be still treated as a string constant.
      bool stringConstant = script.Rest.StartsWith(Constants.QUOTE.ToString());

      string token = Utils.GetToken(script, Constants.NEXT_OR_END_ARRAY);
      // Check if this is a variable definition:
      stringConstant = stringConstant || !ParserFunction.FunctionExists(token);
      if (!stringConstant) {
        Variable sourceValue = ParserFunction.GetFunction(token).GetValue(script);
        token = sourceValue.String;
      }

      return token;
    }

    public static bool IsCompareSign(char ch)
    {
      return ch == '<' || ch == '>' || ch == '=';
    }

    public static bool IsAndOrSign(char ch)
    {
      return ch == '&' || ch == '|';
    }

    // Checks whether there is an argument separator (e.g.  ',') before the end of the
    // function call. E.g. returns true for "a,b)" and "a(b,c),d)" and false for "b),c".
    public static bool SeparatorExists(ParsingScript script)
    {
      if (!script.StillValid()) {
        return false;
      }

      int argumentList = 0;
      for (int i = script.Pointer; i < script.Size(); i++) {
        char ch = script.At(i);
        switch (ch) {
          case Constants.NEXT_ARG:
            return true;
          case Constants.START_ARG:
            argumentList++;
            break;
          case Constants.END_STATEMENT:
          case Constants.END_GROUP:
          case Constants.END_ARG:
            if (--argumentList < 0) {
              return false;
            }
            break;
        }
      }

      return false;
    }

    public static List<string> GetFunctionArgs(ParsingScript script)
    {
      bool isList;
      List<Variable> args = Utils.GetArgs(script,
          Constants.START_ARG, Constants.END_ARG, out isList);

      List<string> result = new List<string>();
      for (int i = 0; i < args.Count; i++) {
        result.Add(args[i].AsString());
      }
      return result;
    }

    public static List<Variable> GetArgs(ParsingScript script,
        char start, char end, out bool isList)
    {
      List<Variable> args = new List<Variable>();
      isList = script.StillValid() && script.Current == Constants.START_GROUP;

      if (!script.StillValid() || script.Current == Constants.END_STATEMENT) {
        return args;
      }

      ParsingScript tempScript = new ParsingScript(script.String, script.Pointer);
      string body = Utils.GetBodyBetween(tempScript, start, end);
      string rest = script.Rest;
      // After the statement above tempScript.Parent will point to the last
      // character belonging to the body between start and end characters. 

      while (script.Pointer < tempScript.Pointer) {
        Variable item = Utils.GetItem(script);
        args.Add(item);
        rest = script.Rest;
        if (script.Pointer == tempScript.Pointer) {
          rest = script.Rest;
        }
        script.MoveForwardIf(Constants.NEXT_ARG);
      }

      if (script.Pointer <= tempScript.Pointer) {
        // Eat closing parenthesis, if there is one, but only if it closes
        // the current argument list, not one after it. 
        script.MoveForwardIf(Constants.END_ARG);
      }

      script.MoveForwardIf(Constants.SPACE);
      //script.MoveForwardIf(Constants.SPACE, Constants.END_STATEMENT);
      return args;
    }

    public static string[] GetFunctionSignature(ParsingScript script)
    {
      script.MoveForwardIf(Constants.START_ARG, Constants.SPACE);

      int endArgs = script.FindFirstOf(Constants.END_ARG.ToString());
      if (endArgs < 0) {
        throw new ArgumentException("Couldn't extract function signature");
      }

      string argStr = script.Substr(script.Pointer, endArgs - script.Pointer);
      string[] args = argStr.Split(Constants.NEXT_ARG_ARRAY);

      script.Pointer = endArgs + 1;

      return args;
    }

    public static bool EndsWithFunction(string buffer, List<string> functions)
    {
      foreach (string key in functions) {
        if (buffer.EndsWith(key, StringComparison.OrdinalIgnoreCase)) {
          char prev = key.Length >= buffer.Length ?
              Constants.END_STATEMENT :
              buffer[buffer.Length - key.Length - 1];
          if (Constants.TOKEN_SEPARATION.Contains(prev)) {
            return true;
          }
        }
      }

      return false;
    }

    public static bool SpaceNotNeeded(char next)
    {
      return (next == Constants.SPACE || next == Constants.START_ARG ||
  next == Constants.START_GROUP || next == Constants.START_ARRAY ||
  next == Constants.EMPTY);
    }

    public static bool KeepSpace(StringBuilder sb, char next)
    {
      if (SpaceNotNeeded(next)) {
        return false;
      }

      return EndsWithFunction(sb.ToString(), Constants.FUNCT_WITH_SPACE);
    }
    public static bool KeepSpaceOnce(StringBuilder sb, char next)
    {
      if (SpaceNotNeeded(next)) {
        return false;
      }

      return EndsWithFunction(sb.ToString(), Constants.FUNCT_WITH_SPACE_ONCE);
    }

    public static string ConvertToScript(string source, out Dictionary<int, int> char2Line)
    {
      StringBuilder sb = new StringBuilder(source.Length);
      char2Line = new Dictionary<int, int>();

      bool inQuotes = false;
      bool spaceOK = false;
      bool inComments = false;
      bool simpleComments = false;
      char previous = Constants.EMPTY;

      int parentheses = 0;
      int groups = 0;
      int lineNumber = 0;
      int lastScriptLength = 0;

      for (int i = 0; i < source.Length; i++) {
        char ch = source[i];
        char next = i + 1 < source.Length ? source[i + 1] : Constants.EMPTY;

        if (ch == '\n') {
          if (sb.Length > lastScriptLength) {
            char2Line[sb.Length - 1] = lineNumber;
            lastScriptLength = sb.Length;
          }
          lineNumber++;
        }

        if (inComments && ((simpleComments && ch != '\n') ||
                          (!simpleComments && ch != '*'))) {
          continue;
        }

        switch (ch) {
          case '/':
            if (!inQuotes && (inComments || next == '/' || next == '*')) {
              inComments = true;
              simpleComments = simpleComments || next == '/';
              continue;
            }
            break;
          case '*':
            if (!inQuotes && (inComments && next == '/')) {
              i++; // skip next character
              inComments = false;
              continue;
            }
            break;
          case '“':
          case '”':
          case '"':
            ch = '"';
            if (!inComments) {
              if (previous != '\\') inQuotes = !inQuotes;
            }
            break;
          case ' ':
            if (inQuotes) {
              sb.Append(ch);
            } else {
              bool keepSpace = KeepSpace(sb, next);
              spaceOK = keepSpace ||
                   (previous != Constants.EMPTY && previous != Constants.NEXT_ARG && spaceOK);
              bool spaceOKonce = KeepSpaceOnce(sb, next);
              if (spaceOK || spaceOKonce) {
                sb.Append(ch);
              }
            }
            continue;
          case '\t':
          case '\r':
            if (inQuotes) sb.Append(ch);
            continue;
          case '\n':
            if (simpleComments) {
              inComments = simpleComments = false;
            }
            spaceOK = false;
            continue;
          case Constants.END_ARG:
            if (!inQuotes) {
              parentheses--;
              spaceOK = false;
            }
            break;
          case Constants.START_ARG:
            if (!inQuotes) {
              parentheses++;
            }
            break;
          case Constants.END_GROUP:
            if (!inQuotes) {
              groups--;
              spaceOK = false;
            }
            break;
          case Constants.START_GROUP:
            if (!inQuotes) {
              groups++;
            }
            break;
          case Constants.END_STATEMENT:
            if (!inQuotes) {
              spaceOK = false;
            }
            break;
          default: break;
        }
        if (!inComments) {
          sb.Append(ch);
        }
        previous = ch;
      }

      return sb.ToString();
    }

    public static string BeautifyScript(string script, string header)
    {
      StringBuilder result = new StringBuilder();
      char[] extraSpace = ("<>=&|+-*/%").ToCharArray();

      int indent = Constants.INDENT;
      result.AppendLine(header);

      bool inQuotes = false;
      bool lineStart = true;

      for (int i = 0; i < script.Length; i++) {
        char ch = script[i];
        inQuotes = ch == Constants.QUOTE ? !inQuotes : inQuotes;

        if (inQuotes) {
          result.Append(ch);
          continue;
        }

        bool needExtra = extraSpace.Contains(ch) && i > 0 && i < script.Length - 1;
        if (needExtra && !extraSpace.Contains(script[i - 1])) {
          result.Append(" ");
        }

        switch (ch) {
          case Constants.START_GROUP:
            result.AppendLine(" " + Constants.START_GROUP);
            indent += Constants.INDENT;
            lineStart = true;
            break;
          case Constants.END_GROUP:
            indent -= Constants.INDENT;
            result.AppendLine(new String(' ', indent) + Constants.END_GROUP);
            lineStart = true;
            break;
          case Constants.END_STATEMENT:
            result.AppendLine(ch.ToString());
            lineStart = true;
            break;
          default:
            if (lineStart) {
              result.Append(new String(' ', indent));
              lineStart = false;
            }
            result.Append(ch.ToString());
            break;
        }
        if (needExtra && !extraSpace.Contains(script[i + 1])) {
          result.Append(" ");
        }
      }

      result.AppendLine(Constants.END_GROUP.ToString());
      return result.ToString();
    }

    public static string GetBodyBetween(ParsingScript script, char open, char close)
    {
      // We are supposed to be one char after the beginning of the string, i.e.
      // we must not have the opening char as the first one.
      StringBuilder sb = new StringBuilder(script.Size());
      int braces = 0;
      bool inQuotes = false;
      bool checkBraces = true;

      for (; script.StillValid(); script.Forward()) {
        char ch = script.Current;

        if (close != Constants.QUOTE) {
          checkBraces = !inQuotes;
          if (ch == Constants.QUOTE) {
            inQuotes = !inQuotes;
          }
        }

        if (string.IsNullOrWhiteSpace(ch.ToString()) && sb.Length == 0) {
          continue;
        } else if (checkBraces && ch == open) {
          braces++;
        } else if (checkBraces && ch == close) {
          braces--;
        }

        sb.Append(ch);
        if (braces == -1) {
          if (ch == close) {
            sb.Remove(sb.Length - 1, 1);
          }
          break;
        }
      }

      return sb.ToString();
    }

    public static string IsNotSign(string data)
    {
      //return data.StartsWith(Constants.NOT) ? Constants.NOT : null;
      return data.StartsWith(Constants.NOT) && !data.StartsWith(Constants.NOT_EQUAL) ? Constants.NOT : null;
    }

    public static string ValidAction(string rest)
    {
      string action = Utils.StartsWith(rest, Constants.ACTIONS);
      return action;
    }

    public static string StartsWith(string data, string[] items)
    {
      foreach (string item in items) {
        if (data.StartsWith(item)) {
          return item;
        }
      }
      return null;
    }

    public static List<Variable> GetArrayIndices(ref string varName)
    {
      int end = 0;
      return GetArrayIndices(ref varName, ref end);
    }

    public static List<Variable> GetArrayIndices(ref string varName, ref int end)
    {
      List<Variable> indices = new List<Variable>();

      int argStart = varName.IndexOf(Constants.START_ARRAY);
      if (argStart < 0) {
        return indices;
      }
      int firstIndexStart = argStart;

      while (argStart < varName.Length &&
             varName[argStart] == Constants.START_ARRAY) {
        int argEnd = varName.IndexOf(Constants.END_ARRAY, argStart + 1);
        if (argEnd == -1 || argEnd <= argStart + 1) {
          break;
        }

        ParsingScript tempScript = new ParsingScript(varName, argStart);
        tempScript.MoveForwardIf(Constants.START_ARG, Constants.START_ARRAY);

        Variable index = tempScript.ExecuteTo(Constants.END_ARRAY);

        indices.Add(index);
        argStart = argEnd + 1;
      }

      if (indices.Count > 0) {
        varName = varName.Substring(0, firstIndexStart);
        end = argStart - 1;
      }

      return indices;
    }

    public static Variable ExtractArrayElement(Variable array,
                                               List<Variable> indices)
    {
      Variable currLevel = array;

      for (int i = 0; i < indices.Count; i++) {
        Variable index = indices[i];
        int arrayIndex = currLevel.GetArrayIndex(index);

        int tupleSize = currLevel.Tuple != null ? currLevel.Tuple.Count : 0;
        if (arrayIndex < 0 || arrayIndex >= tupleSize) {
          throw new ArgumentException("Unknown index [" + index.AsString() +
                             "] for tuple of size " + tupleSize);
        }
        currLevel = currLevel.Tuple[arrayIndex];
      }
      return currLevel;
    }

    public static string GetLinesFromList(ParsingScript script)
    {
      Variable lines = Utils.GetItem(script);
      if (lines.Tuple == null) {
        throw new ArgumentException("Expected a list argument");
      }

      StringBuilder sb = new StringBuilder(80 * lines.Tuple.Count);
      foreach (Variable line in lines.Tuple) {
        sb.AppendLine(line.String);
      }

      return sb.ToString();
    }

  }
}