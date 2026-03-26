using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

using InoIPC;

namespace UniCLI
{
   // ============================================================
   /// <summary>
   /// Discovers Unity instances from the registry and resolves
   /// the Named Pipe name for communication.
   /// </summary>
   // ============================================================
   static class UnityDiscovery
   {

   #region Methods

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Resolves the pipe name from multiple sources.
      /// <br/> Priority: --pipe > UNICLI_PIPE env > registry > pipe discovery.
      /// </summary>
      // ----------------------------------------------------------------------
      public static string GetPipe(string project = null)
      {
         string envPipe = Environment.GetEnvironmentVariable("UNICLI_PIPE");

         if (!string.IsNullOrEmpty(envPipe))
         {
            return envPipe;
         }

         string discovered = DiscoverInstance(project);

         if (discovered != null)
         {
            return discovered;
         }

         // Fallback: find any active unicli pipe
         return NamedPipeTransport.Find("unicli-");
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Discovers a Unity instance from the registry.
      /// <br/> Reads ~/.unicli/instances/*.json and matches by project.
      /// </summary>
      // ----------------------------------------------------------------------
      static string DiscoverInstance(string project)
      {
         string dir = Path.Combine
         (
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".unicli", "instances"
         );

         if (!Directory.Exists(dir))
         {
            return null;
         }

         var candidates = new List<(string pipe, string name, long timestamp)>();

         foreach (string file in Directory.GetFiles(dir, "*.json"))
         {
            try
            {
               string json = File.ReadAllText(file);
               var    doc  = JsonDocument.Parse(json);
               var    root = doc.RootElement;

               string pipe      = root.GetProperty("pipe").GetString();
               int    pid       = root.GetProperty("pid").GetInt32();
               string name      = root.GetProperty("project_name").GetString();
               string path      = root.GetProperty("project_path").GetString();
               long   timestamp = root.GetProperty("timestamp").GetInt64();

               try
               {
                  Process.GetProcessById(pid);
               }
               catch
               {
                  File.Delete(file);
                  continue;
               }

               if (project != null)
               {
                  if (name != null && name.Contains(project, StringComparison.OrdinalIgnoreCase))
                  {
                     candidates.Add((pipe, name, timestamp));
                  }
                  else if (path != null && path.Contains(project, StringComparison.OrdinalIgnoreCase))
                  {
                     candidates.Add((pipe, name, timestamp));
                  }
               }
               else
               {
                  candidates.Add((pipe, name, timestamp));
               }
            }
            catch
            {
               try { File.Delete(file); } catch {}
            }
         }

         if (candidates.Count == 0)
         {
            return null;
         }

         candidates.Sort((a, b) => b.timestamp.CompareTo(a.timestamp));
         return candidates[0].pipe;
      }

   #endregion

   }
}
