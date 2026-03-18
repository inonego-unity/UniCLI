using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

namespace inonego.UniCLI.Core
{
   using Attribute;

   // ============================================================
   /// <summary>
   /// Discovers and invokes CLI commands via reflection.
   /// Scans for [CLIGroup] classes and [CLICommand] methods.
   /// </summary>
   // ============================================================
   public static class CLIRegistry
   {

   #region Internal data

      // ------------------------------------------------------------
      /// <summary>
      /// Metadata for a single registered command.
      /// </summary>
      // ------------------------------------------------------------
      public class CommandInfo
      {
         public string     Group;
         public string     Name;
         public string     FullName;
         public string     GroupDescription;
         public string     Description;
         public MethodInfo Method;
         public bool       IsAsync;
      }

   #endregion

   #region Fields

      private static readonly Dictionary<string, CommandInfo> commands = new Dictionary<string, CommandInfo>();
      private static readonly Dictionary<string, string> groupDescriptions = new Dictionary<string, string>();
      private static bool initialized = false;

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Scans all loaded assemblies for CLI commands.
      /// Safe to call multiple times; only runs once.
      /// </summary>
      // ------------------------------------------------------------
      public static void Initialize()
      {
         if (initialized)
         {
            return;
         }

         initialized = true;
         commands.Clear();
         groupDescriptions.Clear();

         foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
         {
            ScanAssembly(assembly);
         }

         CLILog.Info($"Registered {commands.Count} commands in {groupDescriptions.Count} groups.");
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Invokes a command by group and command name.
      /// <br/> Async commands (Awaitable) are awaited automatically.
      /// <br/> Sync commands run on main thread via Awaitable.
      /// </summary>
      // ----------------------------------------------------------------------
      public static async Awaitable<object> InvokeCommand(string group, string command, CommandArgs args)
      {
         string key = BuildKey(group, command);

         if (!commands.TryGetValue(key, out CommandInfo info))
         {
            // Try group with empty command (default command)
            if (!string.IsNullOrEmpty(command))
            {
               string defaultKey = BuildKey(group, "");

               if (commands.TryGetValue(defaultKey, out info))
               {
                  // Shift: the "command" was actually a positional arg
                  var shiftedArgs = new string[args.ArgCount + 1];
                  shiftedArgs[0] = command;

                  for (int i = 0; i < args.ArgCount; i++)
                  {
                     shiftedArgs[i + 1] = args.Arg(i);
                  }

                  args = new CommandArgs(shiftedArgs, args.RawOptions);
               }
            }

            if (info == null)
            {
               if (!groupDescriptions.ContainsKey(group))
               {
                  throw new CLIException(ErrorCode.UNKNOWN_GROUP, $"Unknown group: {group}");
               }

               throw new CLIException(ErrorCode.UNKNOWN_COMMAND, $"Unknown command: {group} {command}");
            }
         }

         if (info.IsAsync)
         {
            var ret = info.Method.Invoke(null, new object[] { args });
            return await (Awaitable<object>)ret;
         }
         else
         {
            await Awaitable.MainThreadAsync();
            return info.Method.Invoke(null, new object[] { args });
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Returns all registered group names and descriptions.
      /// </summary>
      // ------------------------------------------------------------
      public static Dictionary<string, string> GetGroups()
      {
         return new Dictionary<string, string>(groupDescriptions);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Returns all commands in a group.
      /// </summary>
      // ------------------------------------------------------------
      public static List<CommandInfo> GetCommands(string group)
      {
         return commands.Values
            .Where(c => c.Group == group)
            .ToList();
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Returns all registered commands.
      /// </summary>
      // ------------------------------------------------------------
      public static List<CommandInfo> GetAllCommands()
      {
         return commands.Values.ToList();
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Scans a single assembly for CLI groups and commands.
      /// </summary>
      // ------------------------------------------------------------
      private static void ScanAssembly(Assembly assembly)
      {
         Type[] types;

         try
         {
            types = assembly.GetTypes();
         }
         catch (ReflectionTypeLoadException ex)
         {
            types = ex.Types.Where(t => t != null).ToArray();
         }
         catch
         {
            return;
         }

         foreach (var type in types)
         {
            var groupAttr = type.GetCustomAttribute<CLIGroupAttribute>();

            if (groupAttr == null)
            {
               continue;
            }

            groupDescriptions[groupAttr.Name] = groupAttr.Description;
            ScanGroupMethods(type, groupAttr);
         }
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// Scans a group class for [CLICommand] methods.
      /// Each method must be public static and accept a single CommandArgs.
      /// </summary>
      // ----------------------------------------------------------------------
      private static void ScanGroupMethods(Type type, CLIGroupAttribute groupAttr)
      {
         var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

         foreach (var method in methods)
         {
            var cmdAttr = method.GetCustomAttribute<CLICommandAttribute>();

            if (cmdAttr == null)
            {
               continue;
            }

            // Validate method signature
            var parameters = method.GetParameters();

            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(CommandArgs))
            {
               CLILog.Warning($"Skipping {groupAttr.Name}.{cmdAttr.Name}: invalid signature (must accept CommandArgs).");
               continue;
            }

            string key = BuildKey(groupAttr.Name, cmdAttr.Name);

            bool isAsync = typeof(Awaitable<object>).IsAssignableFrom(method.ReturnType);

            commands[key] = new CommandInfo
            {
               Group            = groupAttr.Name,
               Name             = cmdAttr.Name,
               FullName         = key,
               GroupDescription = groupAttr.Description,
               Description      = cmdAttr.Description,
               Method           = method,
               IsAsync          = isAsync
            };
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Builds a lookup key from group and command names.
      /// </summary>
      // ------------------------------------------------------------
      private static string BuildKey(string group, string command)
      {
         if (string.IsNullOrEmpty(command))
         {
            return group;
         }

         return $"{group}.{command}";
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// Standard error codes for CLI error responses.
   /// </summary>
   // ============================================================
   public static class ErrorCode
   {
      public const string PARSE_ERROR      = "PARSE_ERROR";
      public const string UNKNOWN_GROUP    = "UNKNOWN_GROUP";
      public const string UNKNOWN_COMMAND  = "UNKNOWN_COMMAND";
      public const string INVALID_ARGS     = "INVALID_ARGS";
      public const string COMPILE_ERROR    = "COMPILE_ERROR";
      public const string RUNTIME_ERROR    = "RUNTIME_ERROR";
      public const string INTERNAL_ERROR   = "INTERNAL_ERROR";
      public const string NOT_AVAILABLE    = "NOT_AVAILABLE";
   }

   // ============================================================
   /// <summary>
   /// Exception with a structured error code for CLI responses.
   /// </summary>
   // ============================================================
   public class CLIException : Exception
   {
      public string Code { get; }

      public CLIException(string code, string message) : base(message)
      {
         Code = code;
      }

      public CLIException(string code, string message, Exception inner) : base(message, inner)
      {
         Code = code;
      }
   }
}
