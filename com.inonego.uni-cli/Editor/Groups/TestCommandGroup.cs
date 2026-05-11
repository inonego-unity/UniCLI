using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
         var filter    = BuildFilter(args);
         var jobId     = JobTracker.Create();
         var callbacks = new TestRunCallbacks(jobId, Api);

         Api.RegisterCallbacks(callbacks);

         var settings = new ExecutionSettings
         (
            filter
         );

         Api.Execute(settings);

         return new JObject { ["job_id"] = jobId };
      }

      [CLICommand("test", "list", description = "List available tests")]
      public static object List(CommandArgs args)
      {
         var filter = BuildFilter(args, defaultBoth: false);
         var jobId  = JobTracker.Create();

         Api.RetrieveTestList(filter.testMode, root =>
         {
            var tests = new List<object>();
            CollectTests(root, tests, filter);

            JobTracker.Complete(jobId, new
            {
               mode  = filter.testMode.ToString(),
               count = tests.Count,
               tests = tests
            });
         });

         return new JObject { ["job_id"] = jobId };
      }

   #endregion

   #region Helpers

      private static Filter BuildFilter(CommandArgs args, bool defaultBoth = true)
      {
         return new Filter
         {
            testMode      = ParseTestMode(args["mode"], defaultBoth),
            assemblyNames = GetOptionalValues(args, "assembly"),
            testNames     = GetOptionalValues(args, "test"),
            groupNames    = GetOptionalValues(args, "group"),
            categoryNames = GetOptionalValues(args, "category")
         };
      }

      private static TestMode ParseTestMode(string mode, bool defaultBoth = true)
      {
         if (string.IsNullOrEmpty(mode))
         {
            return defaultBoth
               ? (TestMode.EditMode | TestMode.PlayMode)
               : TestMode.EditMode;
         }

         switch (mode.ToLower())
         {
            case "all":
               return TestMode.EditMode | TestMode.PlayMode;

            case "play":
               return TestMode.PlayMode;

            case "edit":
               return TestMode.EditMode;

            default:
               throw new CLIException(Constants.Error.InvalidArgs, $"Invalid test mode: {mode}");
         }
      }

      private static string[] GetOptionalValues(CommandArgs args, string key)
      {
         var values = args.All(key, new List<string>())
            .Where(v => !string.IsNullOrEmpty(v))
            .ToArray();

         return values.Length > 0 ? values : null;
      }

      private static void CollectTests(ITestAdaptor node, List<object> results, Filter filter)
      {
         if (!node.IsSuite && !node.IsTestAssembly)
         {
            if (MatchesFilter(node, filter))
            {
               results.Add(new
               {
                  full_name          = node.FullName,
                  name               = node.Name,
                  mode               = node.TestMode.ToString(),
                  unique_name        = node.UniqueName,
                  parent_unique_name = node.ParentUniqueName,
                  parent_full_name   = node.ParentFullName
               });
            }
         }

         if (node.HasChildren)
         {
            foreach (var child in node.Children)
            {
               CollectTests(child, results, filter);
            }
         }
      }

      private static bool MatchesFilter(ITestAdaptor node, Filter filter)
      {
         if (!MatchesAssembly(node, filter.assemblyNames))
         {
            return false;
         }

         if (!MatchesExactName(node.FullName, filter.testNames))
         {
            return false;
         }

         if (!MatchesRegexName(node.FullName, filter.groupNames))
         {
            return false;
         }

         if (!MatchesCategories(node, filter.categoryNames))
         {
            return false;
         }

         return true;
      }

      private static bool MatchesAssembly(ITestAdaptor node, string[] assemblyNames)
      {
         if (assemblyNames == null || assemblyNames.Length == 0)
         {
            return true;
         }

         string assemblyName;
         if (!TryGetAssemblyName(node, out assemblyName))
         {
            return false;
         }

         return MatchesFilterSet(assemblyNames, filter => string.Equals(filter, assemblyName, StringComparison.OrdinalIgnoreCase));
      }

      private static bool TryGetAssemblyName(ITestAdaptor node, out string assemblyName)
      {
         assemblyName = null;

         if (TryGetAssemblyNameFromUniqueName(node.UniqueName, out assemblyName))
         {
            return true;
         }

         if (TryGetAssemblyNameFromUniqueName(node.ParentUniqueName, out assemblyName))
         {
            return true;
         }

         var typeInfo = node.TypeInfo ?? node.Parent?.TypeInfo;

         if (typeInfo == null || typeInfo.Assembly == null)
         {
            return false;
         }

         assemblyName = typeInfo.Assembly.GetName().Name;
         return true;
      }

      private static bool TryGetAssemblyNameFromUniqueName(string uniqueName, out string assemblyName)
      {
         assemblyName = null;

         if (string.IsNullOrEmpty(uniqueName))
         {
            return false;
         }

         var dllIndex = uniqueName.IndexOf(".dll", StringComparison.OrdinalIgnoreCase);
         if (dllIndex > 0)
         {
            var candidate = uniqueName.Substring(0, dllIndex);
            var slash     = Math.Max(candidate.LastIndexOf('/'), candidate.LastIndexOf('\\'));

            assemblyName = slash >= 0
               ? candidate.Substring(slash + 1)
               : candidate;

            return !string.IsNullOrEmpty(assemblyName);
         }

         var openingBracket = uniqueName.IndexOf('[');
         var closingBracket = openingBracket >= 0
            ? uniqueName.IndexOf(']', openingBracket + 1)
            : -1;

         if (openingBracket >= 0 && closingBracket > openingBracket)
         {
            assemblyName = uniqueName.Substring(openingBracket + 1, closingBracket - openingBracket - 1);
            return !string.IsNullOrEmpty(assemblyName);
         }

         return false;
      }

      private static bool MatchesExactName(string value, string[] filters)
      {
         if (string.IsNullOrEmpty(value))
         {
            return filters == null || filters.Length == 0;
         }

         return MatchesFilterSet(filters, filter => string.Equals(value, filter, StringComparison.Ordinal));
      }

      private static bool MatchesRegexName(string value, string[] filters)
      {
         if (string.IsNullOrEmpty(value))
         {
            return filters == null || filters.Length == 0;
         }

         return MatchesFilterSet(filters, filter => Regex.IsMatch(value, filter));
      }

      private static bool MatchesCategories(ITestAdaptor node, string[] categoryNames)
      {
         var categories = CollectCategories(node);

         return MatchesFilterSet(categoryNames, filter => categories.Any(category => Regex.IsMatch(category, filter)));
      }

      private static bool MatchesFilterSet(string[] filters, Func<string, bool> matches)
      {
         if (filters == null || filters.Length == 0)
         {
            return true;
         }

         var includes = filters.Where(filter => !filter.StartsWith("!")).ToArray();
         var excludes = filters.Where(filter => filter.StartsWith("!")).Select(filter => filter.Substring(1)).ToArray();

         if (excludes.Any(matches))
         {
            return false;
         }

         return includes.Length == 0 || includes.Any(matches);
      }

      private static List<string> CollectCategories(ITestAdaptor node)
      {
         var categories = new List<string>();
         var current    = node;

         while (current != null)
         {
            if (current.Categories != null)
            {
               categories.AddRange(current.Categories);
            }

            current = current.Parent;
         }

         if (categories.Count == 0)
         {
            categories.Add("Uncategorized");
         }

         return categories;
      }

   #endregion

   }
}
