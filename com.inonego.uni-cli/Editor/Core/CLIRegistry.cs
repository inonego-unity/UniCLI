using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

using InoCLI;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// CLI command registry. Wraps InoCLI CommandRegistry with
   /// Unity-specific async invoke support.
   /// </summary>
   // ============================================================
   public static class CLIRegistry
   {

   #region Fields

      private static CommandRegistry registry = new CommandRegistry();
      private static bool initialized = false;

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Scans all loaded assemblies for [CLICommand] methods.
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

         registry.Initialize(AppDomain.CurrentDomain.GetAssemblies());

         CLILog.Info($"Registered {registry.GetAll().Count} commands.");
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Resolves and invokes a command from parsed args.
      /// <br/> Async commands (Awaitable) are awaited automatically.
      /// <br/> Sync commands run on main thread via Awaitable.
      /// </summary>
      // ----------------------------------------------------------------------
      public static async Awaitable<object> Invoke(CommandArgs parsed)
      {
         var (info, args) = registry.Resolve(parsed);

         bool isAsync = typeof(Awaitable<object>).IsAssignableFrom(info.Method.ReturnType);

         if (isAsync)
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
      /// Returns help text for all commands or a specific path.
      /// </summary>
      // ------------------------------------------------------------
      public static string GetHelp(params string[] path)
      {
         if (path == null || path.Length == 0)
         {
            return registry.GetHelp();
         }

         return registry.GetHelp(path);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Returns distinct root command names.
      /// </summary>
      // ------------------------------------------------------------
      public static List<string> GetRoots()
      {
         return registry.GetRoots();
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Returns all registered commands.
      /// </summary>
      // ------------------------------------------------------------
      public static List<CommandInfo> GetAllCommands()
      {
         return registry.GetAll();
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Returns commands under a specific root path.
      /// </summary>
      // ------------------------------------------------------------
      public static List<CommandInfo> GetCommands(string root)
      {
         return registry.GetAll()
            .Where(c => c.Path.Length > 0 && c.Path[0] == root)
            .ToList();
      }

   #endregion

   }
}
