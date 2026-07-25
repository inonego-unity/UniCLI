/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ObjectCommandGroup.cs
수정일 : 2026-07-25

# 설명
Unity 객체의 복제, 선택, 강조 및 삭제 명령을 제공한다.
========================================================================= BLOCK_HEADER_END */

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

         if (parentId != null)
         {
            var parent = EntityIdUtility.Resolve(parentId) as GameObject;

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
         var obj = EntityIdUtility.Resolve(args[0]);

         EditorGUIUtility.PingObject(obj);

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
            string id = args[i];

            if (string.IsNullOrEmpty(id))
            {
               notFound.Add(id);
               continue;
            }

            var obj = EntityIdUtility.Resolve(id);

            if (obj != null)
            {
               objects.Add(obj);
               selected.Add(EntityIdUtility.Serialize(obj));
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
            ["instance_id"] = EntityIdUtility.Serialize(obj),
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
         string id = args[argIndex];

         if (string.IsNullOrEmpty(id))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Instance ID required.");
         }

         var obj = EntityIdUtility.Resolve(id);

         if (obj == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, $"Object {id} not found.");
         }

         return obj;
      }

   #endregion

   }
}
