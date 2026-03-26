using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

using UnityEditor;

using InoCLI;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// Build command.
   /// </summary>
   // ============================================================
   public static class BuildCommandGroup
   {

   #region Commands

      [CLICommand("build", description = "Build the project")]
      public static object Build(CommandArgs args)
      {
         string target = args["target"];
         string path   = args.Get("path", "Builds/Build");
         bool   run    = args.Flag("run");
         var    jobId  = JobTracker.Create();

         BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;

         if (target != null && Enum.TryParse<BuildTarget>(target, true, out var parsed))
         {
            buildTarget = parsed;
         }

         var options = BuildOptions.None;

         if (run)
         {
            options |= BuildOptions.AutoRunPlayer;
         }

         var scenes = new List<string>();

         foreach (var scene in EditorBuildSettings.scenes)
         {
            if (scene.enabled)
            {
               scenes.Add(scene.path);
            }
         }

         EditorApplication.delayCall += () =>
         {
            try
            {
               // Save open scenes to prevent modal dialog
               UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

               var report = BuildPipeline.BuildPlayer(scenes.ToArray(), path, buildTarget, options);
               JobTracker.Complete(jobId, new { result = report.summary.result.ToString() });
            }
            catch (Exception ex)
            {
               JobTracker.Fail(jobId, ex.Message);
            }
         };

         FocusEditorWindow();

         return new JObject { ["job_id"] = jobId };
      }

   #endregion

   #region Helpers

      [DllImport("user32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      private static extern bool SetForegroundWindow(IntPtr hWnd);

      [DllImport("user32.dll")]
      private static extern IntPtr GetActiveWindow();

      private static void FocusEditorWindow()
      {
         try
         {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            SetForegroundWindow(process.MainWindowHandle);
         }
         catch {}
      }

   #endregion

   }
}
