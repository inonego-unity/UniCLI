using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Attribute;
   using Core;

   // ============================================================
   /// <summary>
   /// Prefab management commands.
   /// </summary>
   // ============================================================
   [CLIGroup("prefab", "Prefab operations")]
   public class PrefabCommandGroup
   {

   #region Commands

      [CLICommand("load", "Load prefab for editing")]
      public static object Load(CommandArgs args)
      {
         string path = args.Arg(0);

         if (string.IsNullOrEmpty(path))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Prefab path required.");
         }

         var root = PrefabUtility.LoadPrefabContents(path);

         return new JObject
         {
            ["path"]    = path,
            ["root_id"] = root.GetInstanceID()
         };
      }

      [CLICommand("unload", "Unload prefab contents")]
      public static object Unload(CommandArgs args)
      {
         int id = args.ArgInt(0);

         if (id == 0)
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Root instance ID required (from prefab load).");
         }

         var go = EditorUtility.EntityIdToObject(id) as GameObject;

         if (go == null)
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, $"GameObject {id} not found.");
         }

         PrefabUtility.UnloadPrefabContents(go);

         return null;
      }

      [CLICommand("save", "Save GameObject as prefab")]
      public static object Save(CommandArgs args)
      {
         int id     = args.ArgInt(0);
         string path = args.Arg(1);

         if (id == 0 || string.IsNullOrEmpty(path))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Instance ID and path required.");
         }

         var go = EditorUtility.EntityIdToObject(id) as GameObject;

         if (go == null)
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, $"GameObject {id} not found.");
         }

         PrefabUtility.SaveAsPrefabAsset(go, path);

         return new JObject { ["path"] = path };
      }

      [CLICommand("apply", "Apply prefab overrides")]
      public static object Apply(CommandArgs args)
      {
         var go = GetPrefabInstance(args);

         string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
         PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);

         return null;
      }

      [CLICommand("revert", "Revert prefab overrides")]
      public static object Revert(CommandArgs args)
      {
         var go = GetPrefabInstance(args);

         PrefabUtility.RevertPrefabInstance(go, InteractionMode.UserAction);

         return null;
      }

      [CLICommand("unpack", "Unpack prefab instance")]
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
         int id = args.ArgInt(0);

         if (id == 0)
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Instance ID required.");
         }

         var obj = EditorUtility.EntityIdToObject(id) as GameObject;

         if (obj == null)
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, $"GameObject {id} not found.");
         }

         var root = PrefabUtility.GetNearestPrefabInstanceRoot(obj);

         if (root == null)
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, $"GameObject {id} is not a prefab instance.");
         }

         return root;
      }

   #endregion

   }
}
