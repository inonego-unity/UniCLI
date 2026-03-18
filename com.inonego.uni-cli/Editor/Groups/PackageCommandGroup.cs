using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Attribute;
   using Core;

   // ============================================================
   /// <summary>
   /// UPM package management commands.
   /// </summary>
   // ============================================================
   [CLIGroup("package", "Package management")]
   public class PackageCommandGroup
   {

   #region Commands

      [CLICommand("list", "List installed packages")]
      public static object List(CommandArgs args)
      {
         var request = Client.List();
         WaitForRequest(request);

         if (request.Status == StatusCode.Failure)
         {
            throw new CLIException(ErrorCode.INTERNAL_ERROR, request.Error.message);
         }

         var result = new JArray();

         foreach (var pkg in request.Result)
         {
            var entry = new JObject
            {
               ["name"]         = pkg.name,
               ["version"]      = pkg.version,
               ["display_name"] = pkg.displayName,
               ["source"]       = pkg.source.ToString()
            };

            if (pkg.source == PackageSource.Local || pkg.source == PackageSource.LocalTarball)
            {
               entry["path"] = pkg.resolvedPath;
            }

            if (pkg.source == PackageSource.Git)
            {
               entry["git"] = pkg.packageId;
            }

            result.Add(entry);
         }

         return result;
      }

      [CLICommand("install", "Install a package")]
      public static object Install(CommandArgs args)
      {
         string id = args.Arg(0);

         if (string.IsNullOrEmpty(id))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Package ID or git URL required.");
         }

         var request = Client.Add(id);
         WaitForRequest(request);

         if (request.Status == StatusCode.Failure)
         {
            throw new CLIException(ErrorCode.INTERNAL_ERROR, request.Error.message);
         }

         return new JObject
         {
            ["name"]    = request.Result.name,
            ["version"] = request.Result.version
         };
      }

      [CLICommand("rm", "Remove a package")]
      public static object Rm(CommandArgs args)
      {
         string id = args.Arg(0);

         if (string.IsNullOrEmpty(id))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Package name required.");
         }

         var request = Client.Remove(id);
         WaitForRequest(request);

         if (request.Status == StatusCode.Failure)
         {
            throw new CLIException(ErrorCode.INTERNAL_ERROR, request.Error.message);
         }

         return new JObject { ["name"] = id };
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Synchronously waits for a UPM request to complete.
      /// </summary>
      // ------------------------------------------------------------
      private static void WaitForRequest(Request request)
      {
         while (!request.IsCompleted)
         {
            System.Threading.Thread.Sleep(10);
         }
      }

   #endregion

   }
}
