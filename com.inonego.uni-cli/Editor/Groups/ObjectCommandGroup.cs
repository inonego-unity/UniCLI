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

   // =====================================================================
   /// <summary>
   /// Universal Object commands (works on any UnityEngine.Object).
   /// </summary>
   // =====================================================================
   public static class ObjectCommandGroup
   {

   #region Commands

      // ------------------------------------------------------------
      /// <summary>
      /// Instantiates (clones) an object.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("object", "instantiate", description = "Clone an object")]
      public static object Instantiate(CommandArgs args)
      {
         var obj = GetTarget(args, 0);

         var clone = UnityEngine.Object.Instantiate(obj);
         Undo.RegisterCreatedObjectUndo(clone, $"Instantiate {obj.name}");

         string name = args["name"];

         if (name != null)
         {
            clone.name = name;
         }

         string parentId = args["parent"];

         if (parentId != null && int.TryParse(parentId, out int pid))
         {
            var parent = EditorUtility.EntityIdToObject(pid) as GameObject;

            if (parent != null && clone is GameObject goClone)
            {
               goClone.transform.SetParent(parent.transform);
            }
         }

         return clone;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Destroys an object with undo support.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("object", "destroy", description = "Destroy an object")]
      public static object Destroy(CommandArgs args)
      {
         var obj = GetTarget(args, 0);

         Undo.DestroyObjectImmediate(obj);

         return null;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Pings (highlights) an object in the editor.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("object", "ping", description = "Highlight an object in editor")]
      public static object Ping(CommandArgs args)
      {
         int id = args.GetInt(0, 0);

         EditorGUIUtility.PingObject(id);

         return null;
      }

      // --------------------------------------------------------------------
      /// <summary>
      /// Selects objects in the editor.
      /// Returns {selected:[...], not_found:[...]} so callers can detect
      /// partial / total resolution failures.
      /// </summary>
      // --------------------------------------------------------------------
      [CLICommand("object", "select", description = "Select objects in editor")]
      public static object Select(CommandArgs args)
      {
         var objects  = new List<UnityEngine.Object>();
         var selected = new JArray();
         var notFound = new JArray();

         for (int i = 0; i < args.Count; i++)
         {
            int id = args.GetInt(i, 0);

            if (id == 0)
            {
               notFound.Add(id);
               continue;
            }

            var obj = EditorUtility.EntityIdToObject(id);

            if (obj != null)
            {
               objects.Add(obj);
               selected.Add(id);
            }
            else
            {
               notFound.Add(id);
            }
         }

         Selection.objects = objects.ToArray();

         return new JObject
         {
            ["selected"]  = selected,
            ["not_found"] = notFound
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets or sets the name of an object.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("object", "name", description = "Get or set object name")]
      public static object Name(CommandArgs args)
      {
         var obj = GetTarget(args, 0);
         string value = args[1];

         if (value != null)
         {
            Undo.RecordObject(obj, "Rename");
            obj.name = value;
         }

         return new JObject
         {
            ["instance_id"] = obj.GetInstanceID(),
            ["name"]        = obj.name
         };
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Gets an Object by instance ID from arg at index.
      /// </summary>
      // ------------------------------------------------------------
      private static UnityEngine.Object GetTarget(CommandArgs args, int argIndex)
      {
         int id = args.GetInt(argIndex, 0);

         if (id == 0)
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Instance ID required.");
         }

         var obj = EditorUtility.EntityIdToObject(id);

         if (obj == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, $"Object {id} not found.");
         }

         return obj;
      }

   #endregion

   }
}
