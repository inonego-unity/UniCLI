using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using InoCLI;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// Scene management commands.
   /// </summary>
   // ============================================================
   public static class SceneCommandGroup
   {

   #region Commands

      // ------------------------------------------------------------
      /// <summary>
      /// Lists all loaded scenes.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("scene", "list", description = "List open scenes")]
      public static object List(CommandArgs args)
      {
         var scenes = new List<object>();

         for (int i = 0; i < SceneManager.sceneCount; i++)
         {
            scenes.Add(SceneManager.GetSceneAt(i));
         }

         return scenes;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Creates a new scene.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("scene", "new", description = "Create a new scene")]
      public static object New(CommandArgs args)
      {
         var setup = args.Get("setup", "default") == "empty"
            ? NewSceneSetup.EmptyScene
            : NewSceneSetup.DefaultGameObjects;

         var mode = args.Get("mode", "single") == "additive"
            ? NewSceneMode.Additive
            : NewSceneMode.Single;

         return EditorSceneManager.NewScene(setup, mode);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Opens a scene by path.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("scene", "open", description = "Open a scene")]
      public static object Open(CommandArgs args)
      {
         string path = args[0];

         if (string.IsNullOrEmpty(path))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Scene path required.");
         }

         var mode = args.Flag("additive")
            ? OpenSceneMode.Additive
            : OpenSceneMode.Single;

         return EditorSceneManager.OpenScene(path, mode);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Saves a scene.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("scene", "save", description = "Save scene")]
      public static object Save(CommandArgs args)
      {
         if (args.Flag("all"))
         {
            EditorSceneManager.SaveOpenScenes();

            var scenes = new List<object>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
               scenes.Add(SceneManager.GetSceneAt(i));
            }

            return scenes;
         }

         var scene = GetTargetScene(args);
         string path = args["path"];

         if (path != null)
         {
            EditorSceneManager.SaveScene(scene, path);
         }
         else
         {
            if (string.IsNullOrEmpty(scene.path))
            {
               throw new CLIException(Constants.Error.InvalidArgs, "Scene has no path. Use --path to specify.");
            }

            EditorSceneManager.SaveScene(scene);
         }

         return scene;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Closes a scene.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("scene", "close", description = "Close a scene")]
      public static object Close(CommandArgs args)
      {
         var scene = GetTargetScene(args);
         bool save = args.Flag("save");

         if (save)
         {
            EditorSceneManager.SaveScene(scene);
         }

         EditorSceneManager.CloseScene(scene, true);

         return scene;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets root GameObjects of a scene.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("scene", "root", description = "List root GameObjects")]
      public static object Root(CommandArgs args)
      {
         var scene = GetTargetScene(args);
         return scene.GetRootGameObjects();
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets or sets the active scene.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("scene", "active", description = "Get or set active scene")]
      public static object Active(CommandArgs args)
      {
         string idStr = args["id"];

         if (idStr != null && int.TryParse(idStr, out int handle))
         {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
               var s = SceneManager.GetSceneAt(i);

               if ((int)s.handle == handle)
               {
                  SceneManager.SetActiveScene(s);
                  return s;
               }
            }

            throw new CLIException(Constants.Error.InvalidArgs, $"Scene handle {handle} not found.");
         }

         return SceneManager.GetActiveScene();
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Gets the target scene from --id or active scene.
      /// </summary>
      // ------------------------------------------------------------
      private static Scene GetTargetScene(CommandArgs args)
      {
         string idStr = args["id"];

         if (idStr != null && int.TryParse(idStr, out int handle))
         {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
               var s = SceneManager.GetSceneAt(i);

               if ((int)s.handle == handle)
               {
                  return s;
               }
            }

            throw new CLIException(Constants.Error.InvalidArgs, $"Scene handle {handle} not found.");
         }

         return SceneManager.GetActiveScene();
      }

   #endregion

   }
}
