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
   /// GameObject manipulation commands.
   /// </summary>
   // ============================================================
   public static class GoCommandGroup
   {

   #region Commands

      // ------------------------------------------------------------
      /// <summary>
      /// Creates a new GameObject.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("go", "create", description = "Create a new GameObject")]
      public static object Create(CommandArgs args)
      {
         string name = args[0];

         if (string.IsNullOrEmpty(name))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Name required.");
         }

         GameObject go;
         string primitive = args["primitive"];

         if (primitive != null && Enum.TryParse<PrimitiveType>(primitive, true, out var primType))
         {
            go = GameObject.CreatePrimitive(primType);
            go.name = name;
         }
         else
         {
            go = new GameObject(name);
         }

         string parentId = args["parent"];

         if (parentId != null && int.TryParse(parentId, out int pid))
         {
            var parent = EditorUtility.EntityIdToObject(pid) as GameObject;

            if (parent != null)
            {
               go.transform.SetParent(parent.transform);
            }
         }

         Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

         return go;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets or sets the active state.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("go", "active", description = "Get or set active state")]
      public static object Active(CommandArgs args)
      {
         var go = GetTargetGO(args, 0);

         string value = args[1];

         if (value != null)
         {
            bool active = value.ToLower() == "on" || value.ToLower() == "true";

            Undo.RecordObject(go, "Set Active");
            go.SetActive(active);
         }

         if (args.Flag("hierarchy"))
         {
            return new JObject
            {
               ["instance_id"] = go.GetInstanceID(),
               ["active"]      = go.activeInHierarchy
            };
         }

         return new JObject
         {
            ["instance_id"] = go.GetInstanceID(),
            ["active"]      = go.activeSelf
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets or sets the parent.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("go", "parent", description = "Get or set parent")]
      public static object Parent(CommandArgs args)
      {
         var go = GetTargetGO(args, 0);

         if (args.Flag("null"))
         {
            Undo.RecordObject(go.transform, "Set Parent");
            go.transform.SetParent(null);
         }
         else
         {
            string parentId = args[1];

            if (parentId != null && int.TryParse(parentId, out int pid))
            {
               var parent = EditorUtility.EntityIdToObject(pid) as GameObject;

               if (parent == null)
               {
                  throw new CLIException(Constants.Error.InvalidArgs, $"Parent {pid} not found.");
               }

               Undo.RecordObject(go.transform, "Set Parent");
               go.transform.SetParent(parent.transform);
            }
         }

         var p = go.transform.parent;

         return new JObject
         {
            ["instance_id"] = go.GetInstanceID(),
            ["parent"]      = p != null ? p.gameObject.GetInstanceID() : (int?)null
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets or sets the tag.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("go", "tag", description = "Get or set tag")]
      public static object Tag(CommandArgs args)
      {
         var go = GetTargetGO(args, 0);
         string value = args[1];

         if (value != null)
         {
            Undo.RecordObject(go, "Set Tag");
            go.tag = value;
         }

         return new JObject
         {
            ["instance_id"] = go.GetInstanceID(),
            ["tag"]         = go.tag
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets or sets the layer.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("go", "layer", description = "Get or set layer")]
      public static object Layer(CommandArgs args)
      {
         var go = GetTargetGO(args, 0);
         string value = args[1];

         if (value != null && int.TryParse(value, out int layer))
         {
            Undo.RecordObject(go, "Set Layer");
            go.layer = layer;
         }

         return new JObject
         {
            ["instance_id"] = go.GetInstanceID(),
            ["layer"]       = go.layer
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets or sets the scene.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("go", "scene", description = "Get or set scene")]
      public static object Scene(CommandArgs args)
      {
         var go = GetTargetGO(args, 0);
         string value = args[1];

         if (value != null && int.TryParse(value, out int handle))
         {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
               var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);

               if ((int)s.handle == handle)
               {
                  UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(go, s);
                  break;
               }
            }
         }

         return new JObject
         {
            ["instance_id"] = go.GetInstanceID(),
            ["scene"]       = (int)go.scene.handle
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Lists children of a GameObject.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("go", "children", description = "List children")]
      public static object Children(CommandArgs args)
      {
         var go = GetTargetGO(args, 0);
         bool recursive = args.Flag("recursive");

         return CollectChildren(go.transform, recursive);
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Gets a GameObject by instance ID from arg at index.
      /// </summary>
      // ------------------------------------------------------------
      private static GameObject GetTargetGO(CommandArgs args, int argIndex)
      {
         int id = args.GetInt(argIndex, 0);

         if (id == 0)
         {
            throw new CLIException(Constants.Error.InvalidArgs, "GameObject instance ID required.");
         }

         var obj = EditorUtility.EntityIdToObject(id) as GameObject;

         if (obj == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, $"GameObject {id} not found.");
         }

         return obj;
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// Collects children as a list of GameObjects.
      /// Optionally recursive with nested children field.
      /// </summary>
      // ----------------------------------------------------------------------
      private static List<object> CollectChildren(Transform parent, bool recursive)
      {
         var list = new List<object>();

         for (int i = 0; i < parent.childCount; i++)
         {
            var child = parent.GetChild(i).gameObject;

            if (recursive)
            {
               var childList = CollectChildren(child.transform, true);

               var obj = new JObject
               {
                  ["instance_id"] = child.GetInstanceID(),
                  ["name"]        = child.name,
                  ["type"]        = "UnityEngine.GameObject",
                  ["active"]      = child.activeSelf,
                  ["tag"]         = child.tag,
                  ["layer"]       = child.layer,
                  ["scene"]       = (int)child.scene.handle
               };

               obj["children"] = JToken.FromObject(childList);
               list.Add(obj);
            }
            else
            {
               list.Add(child);
            }
         }

         return list;
      }

   #endregion

   }
}
