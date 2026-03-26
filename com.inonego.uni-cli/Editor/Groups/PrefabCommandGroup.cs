using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;

using InoCLI;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// Prefab management commands.
   /// </summary>
   // ============================================================
   public static class PrefabCommandGroup
   {

   #region Commands

      [CLICommand("prefab", "load", description = "Load prefab for editing")]
      public static object Load(CommandArgs args)
      {
         string path = args[0];

         if (string.IsNullOrEmpty(path))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Prefab path required.");
         }

         var root = PrefabUtility.LoadPrefabContents(path);

         return new JObject
         {
            ["path"]    = path,
            ["root_id"] = root.GetInstanceID()
         };
      }

      [CLICommand("prefab", "unload", description = "Unload prefab contents")]
      public static object Unload(CommandArgs args)
      {
         int id = args.GetInt(0, 0);

         if (id == 0)
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Root instance ID required (from prefab load).");
         }

         var go = EditorUtility.EntityIdToObject(id) as GameObject;

         if (go == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, $"GameObject {id} not found.");
         }

         PrefabUtility.UnloadPrefabContents(go);

         return null;
      }

      [CLICommand("prefab", "save", description = "Save GameObject as prefab")]
      public static object Save(CommandArgs args)
      {
         int id     = args.GetInt(0, 0);
         string path = args[1];

         if (id == 0 || string.IsNullOrEmpty(path))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Instance ID and path required.");
         }

         var go = EditorUtility.EntityIdToObject(id) as GameObject;

         if (go == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, $"GameObject {id} not found.");
         }

         PrefabUtility.SaveAsPrefabAsset(go, path);

         return new JObject { ["path"] = path };
      }

      [CLICommand("prefab", "apply", description = "Apply prefab overrides")]
      public static object Apply(CommandArgs args)
      {
         var go = GetPrefabInstance(args);

         string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
         PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);

         return null;
      }

      [CLICommand("prefab", "revert", description = "Revert prefab overrides")]
      public static object Revert(CommandArgs args)
      {
         var go = GetPrefabInstance(args);

         PrefabUtility.RevertPrefabInstance(go, InteractionMode.UserAction);

         return null;
      }

      [CLICommand("prefab", "unpack", description = "Unpack prefab instance")]
      public static object Unpack(CommandArgs args)
      {
         var go = GetPrefabInstance(args);

         PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.UserAction);

         return null;
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Gets a prefab instance root from args.
      /// </summary>
      // ------------------------------------------------------------
      private static GameObject GetPrefabInstance(CommandArgs args)
      {
         int id = args.GetInt(0, 0);

         if (id == 0)
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Instance ID required.");
         }

         var obj = EditorUtility.EntityIdToObject(id) as GameObject;

         if (obj == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, $"GameObject {id} not found.");
         }

         var root = PrefabUtility.GetNearestPrefabInstanceRoot(obj);

         if (root == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, $"GameObject {id} is not a prefab instance.");
         }

         return root;
      }

   #endregion

   }
}
