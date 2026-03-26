using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Manages instance registry for multi-editor discovery.
   /// Writes instance info to ~/.unicli/instances/ as JSON files.
   /// </summary>
   // ============================================================
   public static class InstanceRegistry
   {

   #region Fields

      private static readonly string registryDir = Path.Combine
      (
         Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
         ".unicli", "instances"
      );

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Registers the current Unity instance with its pipe name.
      /// </summary>
      // ------------------------------------------------------------
      public static void Register(string pipeName)
      {
         try
         {
            Directory.CreateDirectory(registryDir);

            int pid = Process.GetCurrentProcess().Id;

            var entry = new JObject
            {
               ["pipe"]          = pipeName,
               ["pid"]           = pid,
               ["project_path"]  = Application.dataPath.Replace("/Assets", ""),
               ["project_name"]  = Path.GetFileName(Application.dataPath.Replace("/Assets", "")),
               ["unity_version"] = Application.unityVersion,
               ["timestamp"]     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            string path = Path.Combine(registryDir, $"{pid}.json");
            File.WriteAllText(path, entry.ToString(Formatting.Indented));
         }
         catch (Exception ex)
         {
            CLILog.Error("Failed to register instance.", ex);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Unregisters the instance by pipe name.
      /// </summary>
      // ------------------------------------------------------------
      public static void Unregister(string pipeName)
      {
         try
         {
            int pid = Process.GetCurrentProcess().Id;
            string path = Path.Combine(registryDir, $"{pid}.json");

            if (File.Exists(path))
            {
               File.Delete(path);
            }
         }
         catch {}
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Removes stale entries where the process is no longer alive.
      /// </summary>
      // ------------------------------------------------------------
      public static void CleanStale()
      {
         try
         {
            if (!Directory.Exists(registryDir))
            {
               return;
            }

            foreach (string file in Directory.GetFiles(registryDir, "*.json"))
            {
               try
               {
                  string json = File.ReadAllText(file);
                  var entry   = JObject.Parse(json);
                  int pid     = entry["pid"]?.Value<int>() ?? 0;

                  if (pid > 0)
                  {
                     try
                     {
                        Process.GetProcessById(pid);
                     }
                     catch (ArgumentException)
                     {
                        File.Delete(file);
                     }
                  }
               }
               catch
               {
                  File.Delete(file);
               }
            }
         }
         catch {}
      }

   #endregion

   }
}
