using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using UnityEngine;

using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Attribute;
   using Core;

   // ============================================================
   /// <summary>
   /// Test, build, poll, and wait commands.
   /// </summary>
   // ============================================================
   [CLIGroup("test", "Test management")]
   public class TestCommandGroup
   {

   #region Internal data

      private class TestRunCallbacks : ICallbacks
      {
         private readonly string        jobId;
         private readonly TestRunnerApi api;

         public TestRunCallbacks(string jobId, TestRunnerApi api)
         {
            this.jobId = jobId;
            this.api   = api;
         }

         public void RunStarted(ITestAdaptor testsToRun) {}
         public void TestStarted(ITestAdaptor test) {}
         public void TestFinished(ITestResultAdaptor result) {}

         public void RunFinished(ITestResultAdaptor result)
         {
            var results = new List<object>();
            CollectResults(result, results);

            JobTracker.Complete(jobId, new
            {
               passed   = result.PassCount,
               failed   = result.FailCount,
               skipped  = result.SkipCount,
               duration = result.Duration,
               results  = results
            });

            api.UnregisterCallbacks(this);
         }

         private void CollectResults(ITestResultAdaptor result, List<object> list)
         {
            if (!result.HasChildren)
            {
               list.Add(new
               {
                  full_name  = result.FullName,
                  status     = result.TestStatus.ToString(),
                  duration   = result.Duration,
                  message    = result.Message
               });

               return;
            }

            foreach (var child in result.Children)
            {
               CollectResults(child, list);
            }
         }
      }

   #endregion

   #region Fields

      private static TestRunnerApi testRunnerApi = null;

      private static TestRunnerApi Api
      {
         get
         {
            if (testRunnerApi == null)
            {
               testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            }

            return testRunnerApi;
         }
      }

   #endregion

   #region Commands

      [CLICommand("run", "Run tests asynchronously")]
      public static object Run(CommandArgs args)
      {
         var testMode = ParseTestMode(args.Option("mode"));
         var jobId    = JobTracker.Create();
         var callbacks = new TestRunCallbacks(jobId, Api);

         Api.RegisterCallbacks(callbacks);

         var settings = new ExecutionSettings
         (
            new Filter { testMode = testMode }
         );

         Api.Execute(settings);

         return new JObject { ["job_id"] = jobId };
      }

      [CLICommand("list", "List available tests")]
      public static object List(CommandArgs args)
      {
         var testMode = ParseTestMode(args.Option("mode"), defaultBoth: false);
         var jobId    = JobTracker.Create();

         Api.RetrieveTestList(testMode, root =>
         {
            var tests = new List<object>();
            CollectTests(root, tests);

            JobTracker.Complete(jobId, new
            {
               mode  = testMode.ToString(),
               count = tests.Count,
               tests = tests
            });
         });

         return new JObject { ["job_id"] = jobId };
      }

   #endregion

   #region Helpers

      private static TestMode ParseTestMode(string mode, bool defaultBoth = true)
      {
         if (string.IsNullOrEmpty(mode))
         {
            return defaultBoth
               ? (TestMode.EditMode | TestMode.PlayMode)
               : TestMode.EditMode;
         }

         return mode.ToLower() == "play"
            ? TestMode.PlayMode
            : TestMode.EditMode;
      }

      private static void CollectTests(ITestAdaptor node, List<object> results)
      {
         if (!node.IsSuite && !node.IsTestAssembly)
         {
            results.Add(new
            {
               full_name = node.FullName,
               name      = node.Name,
               mode      = node.TestMode.ToString()
            });
         }

         if (node.HasChildren)
         {
            foreach (var child in node.Children)
            {
               CollectTests(child, results);
            }
         }
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// Build command.
   /// </summary>
   // ============================================================
   [CLIGroup("build", "Project build")]
   public class BuildCommandGroup
   {
      [CLICommand("", "Build the project")]
      public static object Build(CommandArgs args)
      {
         string target = args.Option("target");
         string path   = args.Option("path", "Builds/Build");
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

         // Focus editor window so delayCall fires even when in background
         FocusEditorWindow();

         return new JObject { ["job_id"] = jobId };
      }

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
   }

   // ============================================================
   /// <summary>
   /// Poll async job status.
   /// </summary>
   // ============================================================
   [CLIGroup("poll", "Poll async job status")]
   public class PollCommandGroup
   {
      [CLICommand("", "Poll job status")]
      public static object Poll(CommandArgs args)
      {
         string jobId = args.Arg(0);

         if (string.IsNullOrEmpty(jobId))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Job ID required.");
         }

         var job = JobTracker.Get(jobId);

         if (job == null)
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, $"Job not found: {jobId}");
         }

         var result = new JObject
         {
            ["status"] = job.Status.ToString().ToLower()
         };

         if (job.Result != null)
         {
            result["result"] = JToken.FromObject(job.Result);
         }

         return result;
      }
   }
}
