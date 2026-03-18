using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using UnityEditor;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Attribute;
   using Core;

   // ============================================================
   /// <summary>
   /// Editor control, menu, and window commands.
   /// </summary>
   // ============================================================
   [CLIGroup("editor", "Editor control")]
   public class EditorCommandGroup
   {

   #region Playmode

      [CLICommand("play", "Enter play mode")]
      public static object Play(CommandArgs args)
      {
         EditorApplication.isPlaying = true;
         return null;
      }

      [CLICommand("stop", "Exit play mode")]
      public static object Stop(CommandArgs args)
      {
         EditorApplication.isPlaying = false;
         return null;
      }

      [CLICommand("pause", "Toggle pause")]
      public static object Pause(CommandArgs args)
      {
         EditorApplication.isPaused = !EditorApplication.isPaused;
         return null;
      }

      [CLICommand("step", "Step one frame")]
      public static object Step(CommandArgs args)
      {
         EditorApplication.Step();
         return null;
      }

   #endregion

   #region Undo

      [CLICommand("undo", "Perform undo")]
      public static object Undo(CommandArgs args)
      {
         UnityEditor.Undo.PerformUndo();
         return null;
      }

      [CLICommand("redo", "Perform redo")]
      public static object Redo(CommandArgs args)
      {
         UnityEditor.Undo.PerformRedo();
         return null;
      }

   #endregion

   #region State

      [CLICommand("state", "Get editor state")]
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

   #region Menu

      // ------------------------------------------------------------
      /// <summary>
      /// Executes a menu item.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("menu", "Menu operations (exec, list)")]
      public static object Menu(CommandArgs args)
      {
         string sub = args.Arg(0);

         if (sub == "exec")
         {
            string path = args.Arg(1);

            if (string.IsNullOrEmpty(path))
            {
               throw new CLIException(ErrorCode.INVALID_ARGS, "Menu path required.");
            }

            bool result = EditorApplication.ExecuteMenuItem(path);

            if (!result)
            {
               throw new CLIException(ErrorCode.INVALID_ARGS, $"Menu item not found: {path}");
            }

            return null;
         }
         else if (sub == "list")
         {
            return Unsupported.GetSubmenus("").ToList();
         }

         throw new CLIException(ErrorCode.INVALID_ARGS, "Use: editor menu exec <path> | editor menu list");
      }

   #endregion

   #region Window

      // ------------------------------------------------------------
      /// <summary>
      /// Window operations (list, focus, close).
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("window", "Window operations (list, focus, close)")]
      public static object Window(CommandArgs args)
      {
         string sub = args.Arg(0);

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
            int id = args.ArgInt(1);
            var win = EditorUtility.EntityIdToObject(id) as EditorWindow;

            if (win == null)
            {
               throw new CLIException(ErrorCode.INVALID_ARGS, $"Window {id} not found.");
            }

            win.Focus();
            return null;
         }
         else if (sub == "close")
         {
            int id = args.ArgInt(1);
            var win = EditorUtility.EntityIdToObject(id) as EditorWindow;

            if (win == null)
            {
               throw new CLIException(ErrorCode.INVALID_ARGS, $"Window {id} not found.");
            }

            win.Close();
            return null;
         }

         throw new CLIException(ErrorCode.INVALID_ARGS, "Use: editor window list | focus <id> | close <id>");
      }

   #endregion

   }
}
