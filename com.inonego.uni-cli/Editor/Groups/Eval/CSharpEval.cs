using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using InoCLI;

using Microsoft.CSharp;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// C# code compilation and execution via CSharpCodeProvider.
   /// </summary>
   // ============================================================
   public static class CSharpEval
   {

   #region Internal data

      private struct CachedEval
      {
         public Assembly Assembly;
         public string   ClassName;
      }

   #endregion

   #region Fields

      private static readonly Dictionary<string, CachedEval> evalCache = new Dictionary<string, CachedEval>();
      private static int evalCounter = 0;

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Compiles C# code. Safe to call from any thread.
      /// </summary>
      // ------------------------------------------------------------
      public static (Assembly assembly, string className) Compile(CommandArgs args)
      {
         string code = args[0];

         if (string.IsNullOrEmpty(code))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Code argument required.");
         }

         string[] usings   = args.All("using", new List<string>()).ToArray();
         string   cacheKey = BuildCacheKey(code, usings);

         if (evalCache.TryGetValue(cacheKey, out CachedEval cached))
         {
            return (cached.Assembly, cached.ClassName);
         }

         string className = $"DynamicEval_{evalCounter++}";
         string source    = WrapEvalCode(code, className, usings);

         var (compiled, errors) = TryCompile(source);

         if (compiled == null)
         {
            throw new CLIException(Constants.Error.CompileError, string.Join("\n", errors));
         }

         evalCache[cacheKey] = new CachedEval { Assembly = compiled, ClassName = className };

         return (compiled, className);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Executes a compiled assembly. Must run on main thread.
      /// </summary>
      // ------------------------------------------------------------
      public static object Execute(Assembly assembly, string className)
      {
         Type type = assembly.GetType(className);

         if (type == null)
         {
            throw new CLIException(Constants.Error.InternalError, $"Type {className} not found in compiled assembly.");
         }

         MethodInfo runMethod = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

         if (runMethod == null)
         {
            throw new CLIException(Constants.Error.InternalError, "Run method not found.");
         }

         try
         {
            return runMethod.Invoke(null, null);
         }
         catch (TargetInvocationException ex)
         {
            var inner = ex.InnerException ?? ex;
            throw new CLIException(Constants.Error.RuntimeError, inner.Message);
         }
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Wraps user code in a compilable static class.
      /// </summary>
      // ------------------------------------------------------------
      private static string WrapEvalCode(string code, string className, string[] extraUsings)
      {
         string body = $"{code}\n        return null;";

         var sb = new StringBuilder();

         sb.AppendLine("using System;");
         sb.AppendLine("using System.Collections;");
         sb.AppendLine("using System.Collections.Generic;");
         sb.AppendLine("using System.Linq;");
         sb.AppendLine();
         sb.AppendLine("using UnityEngine;");
         sb.AppendLine();
         sb.AppendLine("using UnityEditor;");

         if (extraUsings != null && extraUsings.Length > 0)
         {
            sb.AppendLine();

            foreach (string ns in extraUsings)
            {
               sb.AppendLine($"using {ns};");
            }
         }

         sb.AppendLine();
         sb.AppendLine($"public static class {className}");
         sb.AppendLine("{");
         sb.AppendLine("    public static object Run()");
         sb.AppendLine("    {");
         sb.AppendLine($"        {body}");
         sb.AppendLine("    }");
         sb.AppendLine("}");

         return sb.ToString();
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// Compiles source code using CSharpCodeProvider.
      /// References are passed via a response file (@file) to avoid the
      /// Windows CreateProcess cmdline length limit (~32k) when the project
      /// has many assemblies.
      /// </summary>
      // ----------------------------------------------------------------------
      private static (Assembly assembly, List<string> errors) TryCompile(string source)
      {
         var errors     = new List<string>();
         var provider   = new CSharpCodeProvider();
         var references = CollectReferences();

         string rspPath = Path.Combine(Path.GetTempPath(), $"unicli_refs_{Guid.NewGuid():N}.rsp");

         try
         {
            var sb = new StringBuilder();

            foreach (string path in references)
            {
               sb.Append("-r:\"").Append(path).Append("\"\n");
            }

            File.WriteAllText(rspPath, sb.ToString(), Encoding.UTF8);

            var parameters = new CompilerParameters
            {
               GenerateInMemory      = true,
               GenerateExecutable    = false,
               TreatWarningsAsErrors = false,
               CompilerOptions       = $"@\"{rspPath}\""
            };

            var results = provider.CompileAssemblyFromSource(parameters, source);

            if (results.Errors.HasErrors)
            {
               foreach (CompilerError error in results.Errors)
               {
                  if (!error.IsWarning)
                  {
                     errors.Add($"({error.Line},{error.Column}): {error.ErrorText}");
                  }
               }

               return (null, errors);
            }

            return (results.CompiledAssembly, errors);
         }
         finally
         {
            try { File.Delete(rspPath); } catch {}
         }
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// Collects unique referenced assembly locations for compilation.
      /// </summary>
      // ----------------------------------------------------------------------
      private static List<string> CollectReferences()
      {
         var list  = new List<string>();
         var added = new HashSet<string>();

         foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
         {
            if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location))
            {
               continue;
            }

            try
            {
               var name = asm.GetName().Name;

               if (!added.Add(name))
               {
                  continue;
               }

               if (name == "mscorlib")
               {
                  continue;
               }

               if (IsBclFacade(asm))
               {
                  continue;
               }

               list.Add(asm.Location);
            }
            catch {}
         }

         return list;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Builds a cache key from code and usings.
      /// </summary>
      // ------------------------------------------------------------
      private static string BuildCacheKey(string code, string[] usings)
      {
         if (usings == null || usings.Length == 0)
         {
            return code;
         }

         return $"{code}|{string.Join(",", usings)}";
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// Checks if an assembly is a BCL facade (type-forwarding only).
      /// These cause duplicate reference errors during compilation.
      /// </summary>
      // ----------------------------------------------------------------------
      private static bool IsBclFacade(Assembly asm)
      {
         var name = asm.GetName().Name;

         if (!name.StartsWith("System."))
         {
            return false;
         }

         if (name.StartsWith("System.Private."))
         {
            return false;
         }

         try
         {
            foreach (var attr in asm.GetCustomAttributesData())
            {
               if (attr.AttributeType.Name == "TypeForwardedToAttribute")
               {
                  return true;
               }
            }
         }
         catch {}

         return false;
      }

   #endregion

   }
}
