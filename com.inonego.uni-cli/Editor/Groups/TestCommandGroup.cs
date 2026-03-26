using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

using InoCLI;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// Test runner commands.
   /// </summary>
   // ============================================================
   public static class TestCommandGroup
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

      [CLICommand("test", "run", description = "Run tests asynchronously")]
      public static object Run(CommandArgs args)
      {
         var testMode = ParseTestMode(args["mode"]);
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

      [CLICommand("test", "list", description = "List available tests")]
      public static object List(CommandArgs args)
      {
         var testMode = ParseTestMode(args["mode"], defaultBoth: false);
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
}
