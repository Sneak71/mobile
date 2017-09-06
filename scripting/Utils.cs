using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SplitAndMerge
{
    public partial class Utils
    {
        public static void CheckArgs(int args, int expected, string msg)
        {
            if (args < expected) {
                throw new ArgumentException("Expecting " + expected +
                    " arguments but got " + args + " in " + msg);
            }
        }
        public static void CheckPosInt(Variable variable)
        {
            CheckInteger(variable);
            if (variable.Value <= 0) {
                throw new ArgumentException("Expected a positive integer instead of [" +
                                               variable.Value + "]");
            }
        }
        public static void CheckNonNegativeInt(Variable variable)
        {
            CheckInteger(variable);
            if (variable.Value < 0) {
                throw new ArgumentException("Expected a non-negative integer instead of [" +
                                               variable.Value + "]");
            }
        }
        public static void CheckInteger(Variable variable)
        {
            CheckNumber(variable);
            if (variable.Value % 1 != 0.0) {
                throw new ArgumentException("Expected an integer instead of [" +
                                               variable.Value + "]");
            }
        }
        public static void CheckNumber(Variable variable)
        {
      if (variable.Type != Variable.VarType.NUMBER) {
                throw new ArgumentException ("Expected a number instead of [" +
                                               variable.AsString() + "]");
            }
        }
    public static void CheckNotEmpty(ParsingScript script, string varName, string name)
    {
      if (!script.StillValid() || string.IsNullOrWhiteSpace(varName)) {
        throw new ArgumentException("Incomplete arguments for [" + name + "]");
      }
    }
    public static void CheckNotEnd(ParsingScript script, string name)
    {
      if (!script.StillValid()) {
        throw new ArgumentException("Incomplete arguments for [" + name + "]");
      }
    }
    public static void CheckNotNull(object obj, string name)
    {
        if (obj == null) {
            throw new ArgumentException("Invalid argument in function [" + name + "]");
        }
    }
    public static void CheckNotEnd(ParsingScript script)
    {
      if (!script.StillValid()) {
        throw new ArgumentException("Incomplete function definition.");
      }
    }
    public static void CheckNotEmpty(string varName, string name)
    {
      if (string.IsNullOrEmpty(varName)) {
        throw new ArgumentException("Incomplete arguments for [" + name + "]");
      }
    }
    public static void CheckNotNull(string name, ParserFunction func)
    {
      if (func == null) {
        throw new ArgumentException("Variable or function [" + name + "] doesn't exist");
      }
    }

        public static string GetPathDetails(FileSystemInfo fs, string name)
        {
            string pathname = fs.FullName;
            bool isDir = (fs.Attributes & FileAttributes.Directory) != 0;

            char d = isDir ? 'd' : '-';
      string last  = fs.LastWriteTime.ToString("MMM dd yyyy HH:mm");

      string user = string.Empty;
      string group = string.Empty;
      string links = null;
      string permissions = "rwx";
      long size = 0;

#if __MonoCS__
            Mono.Unix.UnixFileSystemInfo info;
            if (isDir) {
                info = new Mono.Unix.UnixDirectoryInfo(pathname);
            } else {
                info = new Mono.Unix.UnixFileInfo(pathname);
            }

            char ur = (info.FileAccessPermissions & Mono.Unix.FileAccessPermissions.UserRead)     != 0 ? 'r' : '-';
            char uw = (info.FileAccessPermissions & Mono.Unix.FileAccessPermissions.UserWrite)    != 0 ? 'w' : '-';
            char ux = (info.FileAccessPermissions & Mono.Unix.FileAccessPermissions.UserExecute)  != 0 ? 'x' : '-';
            char gr = (info.FileAccessPermissions & Mono.Unix.FileAccessPermissions.GroupRead)    != 0 ? 'r' : '-';
            char gw = (info.FileAccessPermissions & Mono.Unix.FileAccessPermissions.GroupWrite)   != 0 ? 'w' : '-';
            char gx = (info.FileAccessPermissions & Mono.Unix.FileAccessPermissions.GroupExecute) != 0 ? 'x' : '-';
            char or = (info.FileAccessPermissions & Mono.Unix.FileAccessPermissions.OtherRead)    != 0 ? 'r' : '-';
            char ow = (info.FileAccessPermissions & Mono.Unix.FileAccessPermissions.OtherWrite)   != 0 ? 'w' : '-';
            char ox = (info.FileAccessPermissions & Mono.Unix.FileAccessPermissions.OtherExecute) != 0 ? 'x' : '-';

            permissions = string.Format("{0}{1}{2}{3}{4}{5}{6}{7}{8}",
                ur, uw, ux, gr, gw, gx, or, ow, ox);

            user  = info.OwnerUser.UserName;
            group = info.OwnerGroup.GroupName;
            links = info.LinkCount.ToString();

            size = info.Length;

            if (info.IsSymbolicLink) {
                d = 's';
            }

            #else

      if (isDir) {
              user = Directory.GetAccessControl(fs.FullName).GetOwner(
          typeof(System.Security.Principal.NTAccount)).ToString();

              DirectoryInfo di = fs as DirectoryInfo;
              size = di.GetFileSystemInfos().Length;
            } else {
              user = File.GetAccessControl(fs.FullName).GetOwner(
          typeof(System.Security.Principal.NTAccount)).ToString();
              FileInfo fi = fs as FileInfo;
              size = fi.Length;

              string[] execs = new string[] { "exe", "bat", "msi"};
              char x = execs.Contains(fi.Extension.ToLower()) ? 'x' : '-';
              char w = !fi.IsReadOnly ? 'w' : '-';
              permissions = string.Format("r{0}{1}", w, x);
            }
            #endif

            string data  = string.Format("{0}{1} {2,4} {3,8} {4,8} {5,9} {6,23} {7}",
                d, permissions, links, user, group, size, last, name);

            return data;
        }

    public static List<Variable> GetPathnames(string path) 
    {
      string pathname = Path.GetFullPath(path);
      int index = pathname.IndexOf('*');
      if (index < 0 && !Directory.Exists(pathname) && !File.Exists(pathname)) {
        throw new ArgumentException ("Path [" + pathname + "] doesn't exist");
      }

      List<Variable> results = new List<Variable>();
      if (index < 0) {
        results.Add(new Variable(pathname));
        return results;
      }

      string dirName = Path.GetDirectoryName(path);

      try {
        string pattern = Path.GetFileName(pathname);

        pathname = index > 0 ? dirName : ".";

        /*if (index > 0) {
          string prefix = pathname.Substring(0, index);
          DirectoryInfo di = Directory.GetParent(prefix);
          pathname = di.FullName;
        } else {
          pathname = ".";
        }

        string dir = Path.GetFullPath(pathname);*/
        // First get contents of the directory
        DirectoryInfo dirInfo = new DirectoryInfo(dirName);
        FileInfo [] fileNames = dirInfo.GetFiles(pattern);
        foreach (FileInfo fi in fileNames) {
          try {
            string newPath = Path.Combine(dirName, fi.Name);
            results.Add(new Variable(newPath));
          } catch (Exception) {
            continue;
          }
        }

        // Then get contents of all of the subdirs in the directory
        DirectoryInfo [] dirInfos = dirInfo.GetDirectories (pattern);
        foreach (DirectoryInfo di in dirInfos) {
          try {
            string newPath = Path.Combine(dirName, di.Name);
            results.Add (new Variable(newPath));
          } catch (Exception) {
            continue;
          }
        }
      } catch (Exception exc) {
        throw new ArgumentException ("Couldn't get files from " + path + ": " + exc.Message);
      }
      return results;
    }

    public static void Copy(string src, string dst)
    {
      bool isFile = File.Exists(src);
      bool isDir = Directory.Exists(src);
      if (!isFile && !isDir) {
        throw new ArgumentException("[" + src + "] doesn't exist");
      }
      try {
        if (isFile) {
          if (Directory.Exists(dst)) {
            string filename = Path.GetFileName(src);
            dst = Path.Combine(dst, filename);
          }

          File.Copy(src, dst, true);
        } else {
          Utils.DirectoryCopy(src, dst);
        }
      } catch (Exception exc) {
        throw new ArgumentException("Couldn't copy to [" + dst + "]: " + exc.Message);
      }
    }

    public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs = true)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

      if (!dir.Exists) {
        throw new ArgumentException(sourceDirName + " directory doesn't exist");
      }
      if (sourceDirName.Equals(destDirName, StringComparison.InvariantCultureIgnoreCase)) {
        //throw new ArgumentException(sourceDirName + ": directories are same");
        string addPath = Path.GetFileName(sourceDirName);
        destDirName = Path.Combine (destDirName, addPath);
      }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))    {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
        File.Copy(file.FullName, tempPath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

    public static List<string> GetFiles(string path, string[] patterns, bool addDirs = true)
        {
      List<string> files = new List<string>();
      GetFiles(path, patterns, ref files, addDirs);
      return files;
        }

    public static string GetFileEntry(string dir, int i, string startsWith)
    {
      List<string> files = new List<string>();
      string[] patterns = { startsWith + "*" };
      GetFiles(dir, patterns, ref files, true, false);

      if (files.Count == 0) {
        return "";
      }
      i = i % files.Count;

      string pathname = files[i];
      if (files.Count == 1) {
        pathname += Directory.Exists(Path.Combine(dir, pathname)) ?
                    Path.DirectorySeparatorChar.ToString() : " ";
      }
      return pathname;
    }

    public static void GetFiles(string path, string[] patterns, ref List<string> files,
      bool addDirs = true, bool recursive = true)
    {
      SearchOption option = recursive ? SearchOption.AllDirectories :
                                        SearchOption.TopDirectoryOnly;
      if (string.IsNullOrEmpty (path)) {
        path = Directory.GetCurrentDirectory();
      }

      List<string> dirs = patterns.SelectMany(
        i => Directory.EnumerateDirectories(path, i, option )
      ).ToList<string>();

      List<string> extraFiles = patterns.SelectMany(
        i => Directory.EnumerateFiles(path, i, option)
      ).ToList<string>();

      if (addDirs) {
        files.AddRange (dirs);
      }
      files.AddRange(extraFiles);

      if (!recursive) {
        files = files.Select(p => Path.GetFileName(p)).ToList<string>();
        files.Sort();
        return;
      }
      /*foreach (string dir in dirs) {
        GetFiles (dir, patterns, addDirs);
      }*/
    }

    public static List<Variable> ConvertToResults(string[] items,
                                                        bool print = false)
        {
            List<Variable> results = new List<Variable>(items.Length);
            foreach (string item in items)
            {
                results.Add(new Variable(item));
                if (print) {
                    Interpreter.Instance.AppendOutput (item);
                }
            }

            return results;
        }

        public static List<string> GetStringInFiles(string path, string search,
            string[] patterns, bool ignoreCase = true)
        {
      List<string> allFiles = GetFiles(path, patterns, false /* no dirs */);
            List<string> results = new List<string>();

            if (allFiles == null && allFiles.Count == 0)
            {
                return results;
            }

            StringComparison caseSense = ignoreCase ? StringComparison.OrdinalIgnoreCase :
                StringComparison.Ordinal;
            Parallel.ForEach(allFiles, (currentFile) =>
                {
                    string contents = GetFileText(currentFile);
                    if (contents.IndexOf(search, caseSense) >= 0)
                    {
                        lock (s_mutexLock) { results.Add(currentFile); }
                    }
                });

            return results;
        }

    private static void WriteBlinkingText(string text, int delay, bool visible)
    {
      if (visible) {
        Console.Write (text);
      } else {
        Console.Write (new string(' ', text.Length));
      }
      Console.CursorLeft -= text.Length;
      System.Threading.Thread.Sleep(delay);
    }

    public static string GetLine(int chars = 40)
        {
            return string.Format("-").PadRight(chars, '-');
        }

        public static string GetFileText(string filename)
        {
            string fileContents = string.Empty;
            if (File.Exists(filename))
            {
                fileContents = File.ReadAllText(filename);
            }
            return fileContents;
        }

        public static string[] GetFileLines(string filename)
        {
            try {
                string[] lines = File.ReadAllLines(filename);
                return lines;
            } catch (Exception ex) {
                throw new ArgumentException ("Couldn't read file from disk: " + ex.Message);
            }
        }

        public static string[] GetFileLines(string filename, int from, int count)
        {
            try {
                var allLines = File.ReadLines(filename).ToArray();
                if (allLines.Length <= count) {
                    return allLines;
                }

                if (from < 0) {
                    // last n lines
                    from = allLines.Length - count;
                }

                string[] lines = allLines.Skip(from).Take(count).ToArray();
                return lines;
            } catch (Exception ex) {
                throw new ArgumentException ("Couldn't read file from disk: " + ex.Message);
            }
        }

        public static void WriteFileText(string filename, string text)
        {
            try {
                File.WriteAllText(filename, text);
            } catch (Exception ex) {
                throw new ArgumentException ("Couldn't write file to disk: " + ex.Message);
            }
        }

        public static void AppendFileText(string filename, string text)
        {
            try {
                File.AppendAllText(filename, text);
            } catch (Exception ex) {
                throw new ArgumentException ("Couldn't write file to disk: " + ex.Message);
            }
        }

    public static void ThrowException(ParsingScript script, string excName1,
                                      string errorToken = "", string excName2 = "")
    {
      string msg = Translation.GetErrorString(excName1);

      if (!string.IsNullOrWhiteSpace(errorToken)) {
        msg = string.Format(msg, errorToken);
        string candidate = Translation.TryFindError(errorToken, script);

        if (!string.IsNullOrWhiteSpace(candidate) &&
            !string.IsNullOrWhiteSpace(excName2)) {
          string extra = Translation.GetErrorString(excName2);
          msg += " " + string.Format(extra, candidate);
        }
      }

      if (!string.IsNullOrWhiteSpace(script.Filename)) {
        string fileMsg = Translation.GetErrorString("errorFile");
        msg += Environment.NewLine + string.Format(fileMsg, script.Filename);
      }

      int lineNumber = -1;
      string line = script.GetOriginalLine(out lineNumber);
      if (lineNumber >= 0) {
        string lineMsg = Translation.GetErrorString("errorLine");
        msg += string.IsNullOrWhiteSpace(script.Filename) ? Environment.NewLine : " ";
        msg += string.Format(lineMsg, lineNumber + 1, line.Trim());
      }
      throw new ArgumentException(msg);
    }

    public static void PrintList(List<Variable> list, int from)
        {
            Console.Write("Merging list:");
            for (int i = from; i < list.Count; i++)    {
                Console.Write(" ({0}, '{1}')", list[i].Value, list[i].Action);
            }
            Console.WriteLine();
        }

    public static void PrintColor(string output, ConsoleColor fgcolor)
    {
      ConsoleColor currentForeground = Console.ForegroundColor;
      Console.ForegroundColor = fgcolor;

      Interpreter.Instance.AppendOutput(output);
      //Console.Write(output);

      Console.ForegroundColor = currentForeground;
    }

        private static readonly object s_mutexLock = new object();

        public static int GetSafeInt(List<Variable> args, int index, int defaultValue = 0)
        {
            if (args.Count <= index) {
                return defaultValue;
            }
            Utils.CheckNumber(args[index]);
            return args[index].AsInt();
        }
        public static double GetSafeDouble(List<Variable> args, int index, double defaultValue = 0.0)
        {
            if (args.Count <= index) {
                return defaultValue;
            }
            Utils.CheckNumber(args[index]);
            return args[index].AsDouble();
        }
        public static string GetSafeString(List<Variable> args, int index, string defaultValue = "")
        {
            if (args.Count <= index) {
                return defaultValue;
            }
            return args[index].AsString();
        }
        public static Variable GetSafeVariable(List<Variable> args, int index, Variable defaultValue)
        {
            if (args.Count <= index) {
                return defaultValue;
            }
            return args[index];
        }

        public static Variable GetVariable(string varName, ParsingScript script)
        {
            ParserFunction func = ParserFunction.GetFunction(varName);
            Utils.CheckNotNull(varName, func);
            Variable varValue = func.GetValue(script);
            return varValue;
        }

        static Dictionary<string, Func<string, string>> m_compiledCode =
           new Dictionary<string, Func<string, string>>();
        
        public static Variable InvokeCall(Type type, string methodName, string paramName,
                                          string paramValue, object master = null)
        {
            string key = type + "_" + methodName + "_" + paramName;
		    Func<string, string> func = null;

		    // Cache compiled function:
		    if (!m_compiledCode.TryGetValue(key, out func)) {
		        MethodInfo methodInfo = type.GetMethod(methodName, new Type[] { typeof(string) });
		        ParameterExpression param = Expression.Parameter(typeof(string), paramName);

		        MethodCallExpression methodCall = master == null ? Expression.Call(methodInfo, param) :
		                                                     Expression.Call(Expression.Constant(master), methodInfo, param);
		        Expression<Func<string, string>> lambda = 
		            Expression.Lambda<Func<string, string>>(methodCall, new ParameterExpression[] { param });
		        func = lambda.Compile();
		        m_compiledCode[key] = func;
		    }

		    string result = func(paramValue);
		    return new Variable(result);
		}
    }
}
