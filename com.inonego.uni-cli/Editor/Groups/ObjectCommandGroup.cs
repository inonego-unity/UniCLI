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
   /// Universal Object commands (works on any UnityEngine.Object).
   /// </summary>
   // ============================================================
   [CLIGroup("object", "Object operations")]
   public class ObjectCommandGroup
   {

   #region Commands

      // ------------------------------------------------------------
      /// <summary>
      /// Instantiates (clones) an object.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("instantiate", "Clone an object")]
      public static object Instantiate(CommandArgs args)
      {
         var obj = GetTarget(args, 0);

         var clone = UnityEngine.Object.Instantiate(obj);
         Undo.RegisterCreatedObjectUndo(clone, $"Instantiate {obj.name}");

         string name = args.Option("name");

         if (name != null)
         {
            clone.name = name;
         }

         string parentId = args.Option("parent");

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
      [CLICommand("destroy", "Destroy an object")]
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
      [CLICommand("ping", "Highlight an object in editor")]
      public static object Ping(CommandArgs args)
      {
         int id = args.ArgInt(0);

         EditorGUIUtility.PingObject(id);

         return null;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Selects objects in the editor.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("select", "Select objects in editor")]
      public static object Select(CommandArgs args)
      {
         var objects = new List<UnityEngine.Object>();

         for (int i = 0; i < args.ArgCount; i++)
         {
            int id = args.ArgInt(i);

            if (id != 0)
            {
               var obj = EditorUtility.EntityIdToObject(id);

               if (obj != null)
               {
                  objects.Add(obj);
               }
            }
         }

         Selection.objects = objects.ToArray();

         return null;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets or sets the name of an object.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("name", "Get or set object name")]
      public static object Name(CommandArgs args)
      {
         var obj = GetTarget(args, 0);
         string value = args.Arg(1);

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
         int id = args.ArgInt(argIndex);

         if (id == 0)
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Instance ID required.");
         }

         var obj = EditorUtility.EntityIdToObject(id);

         if (obj == null)
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, $"Object {id} not found.");
         }

         return obj;
      }

   #endregion

   }
}
