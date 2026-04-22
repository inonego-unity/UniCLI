using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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

   #region Types

      public readonly struct InstanceInfo
      {
         public readonly string Pipe;
         public readonly string ProjectName;
         public readonly string ProjectPath;
         public readonly long   Timestamp;

         public InstanceInfo(string pipe, string name, string path, long ts)
         {
            Pipe        = pipe;
            ProjectName = name;
            ProjectPath = path;
            Timestamp   = ts;
         }
      }

      public class AmbiguousInstanceException : Exception
      {
         public readonly List<InstanceInfo> Candidates;

         public AmbiguousInstanceException(List<InstanceInfo> candidates)
            : base("Multiple Unity instances are registered.")
         {
            Candidates = candidates;
         }
      }

   #endregion

   #region Methods

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Resolves the pipe name from multiple sources.
      /// <br/> Priority: --pipe > UNICLI_PIPE env > registry > pipe discovery.
      /// <br/> Throws AmbiguousInstanceException when the registry has more
      /// <br/> than one match and UNICLI_AUTO_PICK is not set.
      /// </summary>
      // ----------------------------------------------------------------------
      public static string GetPipe(string project = null)
      {
         string envPipe = Environment.GetEnvironmentVariable("UNICLI_PIPE");

         if (!string.IsNullOrEmpty(envPipe))
         {
            return envPipe;
         }

         var candidates = DiscoverInstances(project);

         if (candidates.Count == 0)
         {
            // Fallback: find any active unicli pipe
            return NamedPipeTransport.Find("unicli-");
         }

         if (candidates.Count == 1)
         {
            return candidates[0].Pipe;
         }

         // 2+ candidates. Prefer an exact project_name match if one exists —
         // substring matching on both name and path is useful but can
         // accidentally pull in unrelated projects whose paths happen to
         // contain the query.
         if (project != null)
         {
            var exact = candidates.FindAll(c =>
               string.Equals(c.ProjectName, project, StringComparison.OrdinalIgnoreCase));

            if (exact.Count == 1)
            {
               return exact[0].Pipe;
            }
         }

         // Allow opt-in auto-pick for CI/scripts.
         if (IsAutoPick())
         {
            candidates.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
            return candidates[0].Pipe;
         }

         throw new AmbiguousInstanceException(candidates);
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Enumerates all Unity instances from the registry.
      /// <br/> Dead instances (pid gone) are cleaned up as a side-effect.
      /// <br/> If project is non-null, candidates are filtered by substring
      /// <br/> match on project_name or project_path.
      /// </summary>
      // ----------------------------------------------------------------------
      public static List<InstanceInfo> DiscoverInstances(string project)
      {
         var result = new List<InstanceInfo>();

         string dir = Path.Combine
         (
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".unicli", "instances"
         );

         if (!Directory.Exists(dir))
         {
            return result;
         }

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
                  bool nameMatch = name != null && name.Contains(project, StringComparison.OrdinalIgnoreCase);
                  bool pathMatch = path != null && path.Contains(project, StringComparison.OrdinalIgnoreCase);

                  if (!nameMatch && !pathMatch)
                  {
                     continue;
                  }
               }

               result.Add(new InstanceInfo(pipe, name, path, timestamp));
            }
            catch
            {
               try { File.Delete(file); } catch {}
            }
         }

         return result;
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// Formats an ambiguous-instance error message for user display.
      /// </summary>
      // ----------------------------------------------------------------------
      public static string FormatAmbiguityMessage(List<InstanceInfo> candidates)
      {
         var sb = new StringBuilder();
         sb.AppendLine("Multiple Unity instances found. Specify one with --project <name> or --pipe <id>:");

         foreach (var c in candidates)
         {
            sb.Append("  --pipe ").Append(c.Pipe);

            if (!string.IsNullOrEmpty(c.ProjectName))
            {
               sb.Append("   # ").Append(c.ProjectName);
            }

            if (!string.IsNullOrEmpty(c.ProjectPath))
            {
               sb.Append(" (").Append(c.ProjectPath).Append(")");
            }

            sb.AppendLine();
         }

         sb.Append("Set UNICLI_AUTO_PICK=1 to keep the legacy 'pick most recent' behavior.");
         return sb.ToString();
      }

      static bool IsAutoPick()
      {
         string v = Environment.GetEnvironmentVariable("UNICLI_AUTO_PICK");
         return !string.IsNullOrEmpty(v) && v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase);
      }

   #endregion

   }
}
