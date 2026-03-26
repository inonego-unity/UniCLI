using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using UnityEditor;

using InoCLI;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// Editor control, menu, and window commands.
   /// </summary>
   // ============================================================
   public static class EditorCommandGroup
   {

   #region Playmode

      [CLICommand("editor", "play", description = "Enter play mode")]
      public static object Play(CommandArgs args)
      {
         EditorApplication.isPlaying = true;
         return null;
      }

      [CLICommand("editor", "stop", description = "Exit play mode")]
      public static object Stop(CommandArgs args)
      {
         EditorApplication.isPlaying = false;
         return null;
      }

      [CLICommand("editor", "pause", description = "Toggle pause")]
      public static object Pause(CommandArgs args)
      {
         EditorApplication.isPaused = !EditorApplication.isPaused;
         return null;
      }

      [CLICommand("editor", "step", description = "Step one frame")]
      public static object Step(CommandArgs args)
      {
         EditorApplication.Step();
         return null;
      }

   #endregion

   #region Undo

      [CLICommand("editor", "undo", description = "Perform undo")]
      public static object Undo(CommandArgs args)
      {
         UnityEditor.Undo.PerformUndo();
         return null;
      }

      [CLICommand("editor", "redo", description = "Perform redo")]
      public static object Redo(CommandArgs args)
      {
         UnityEditor.Undo.PerformRedo();
         return null;
      }

   #endregion

   #region State

      [CLICommand("editor", "state", description = "Get editor state")]
      public static object State(CommandArgs args)
      {
         return new JObject
         {
            ["playing"]   = EditorApplication.isPlaying,
            ["paused"]    = EditorApplication.isPaused,
            ["compiling"] = EditorApplication.isCompiling
         };
      }

   #endregion

   #region SDB

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Returns the SDB debugger port for MonoDebug attachment.
      /// <br/> Scans the current process for listening ports in the
      /// <br/> SDB range (56000+).
      /// </summary>
      // ----------------------------------------------------------------------
      [CLICommand("editor", "sdb", description = "Get SDB debugger port")]
      public static object Sdb(CommandArgs args)
      {
         bool enabled = EditorPrefs.GetBool("AllowAttachedDebuggingOfEditor", false);

         if (!enabled)
         {
            throw new CLIException
            (
               Constants.Error.NotAvailable,
               "Editor debugging is disabled. Enable: Edit > Preferences > External Tools > Editor Attaching, then restart Unity."
            );
         }

         int pid = System.Diagnostics.Process.GetCurrentProcess().Id;

         // Unity SDB port = 56000 + (pid % 1000)
         int port = 56000 + (pid % 1000);

         return new JObject
         {
            ["port"]    = port,
            ["pid"]     = pid,
            ["enabled"] = enabled
         };
      }

   #endregion

   #region Menu

      // ------------------------------------------------------------
      /// <summary>
      /// Executes a menu item.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("editor", "menu", description = "Menu operations (exec, list)")]
      public static object Menu(CommandArgs args)
      {
         string sub = args[0];

         if (sub == "exec")
         {
            string path = args[1];

            if (string.IsNullOrEmpty(path))
            {
               throw new CLIException(Constants.Error.InvalidArgs, "Menu path required.");
            }

            bool result = EditorApplication.ExecuteMenuItem(path);

            if (!result)
            {
               throw new CLIException(Constants.Error.InvalidArgs, $"Menu item not found: {path}");
            }

            return null;
         }
         else if (sub == "list")
         {
            return Unsupported.GetSubmenus("").ToList();
         }

         throw new CLIException(Constants.Error.InvalidArgs, "Use: editor menu exec <path> | editor menu list");
      }

   #endregion

   #region Window

      // ------------------------------------------------------------
      /// <summary>
      /// Window operations (list, focus, close).
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("editor", "window", description = "Window operations (list, focus, close)")]
      public static object Window(CommandArgs args)
      {
         string sub = args[0];

         if (sub == "list")
         {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var result  = new JArray();

            foreach (var w in windows)
            {
               result.Add(new JObject
               {
                  ["instance_id"] = w.GetInstanceID(),
                  ["type"]        = w.GetType().FullName,
                  ["title"]       = w.titleContent.text
               });
            }

            return result;
         }
         else if (sub == "focus")
         {
            int id = args.GetInt(1, 0);
            var win = EditorUtility.EntityIdToObject(id) as EditorWindow;

            if (win == null)
            {
               throw new CLIException(Constants.Error.InvalidArgs, $"Window {id} not found.");
            }

            win.Focus();
            return null;
         }
         else if (sub == "close")
         {
            int id = args.GetInt(1, 0);
            var win = EditorUtility.EntityIdToObject(id) as EditorWindow;

            if (win == null)
            {
               throw new CLIException(Constants.Error.InvalidArgs, $"Window {id} not found.");
            }

            win.Close();
            return null;
         }

         throw new CLIException(Constants.Error.InvalidArgs, "Use: editor window list | focus <id> | close <id>");
      }

   #endregion

   }
}
