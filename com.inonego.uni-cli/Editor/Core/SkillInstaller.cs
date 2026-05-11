using System;
using System.IO;

using UnityEngine;

using UnityEditor;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Copies agent skill files from the package to the project.
   /// </summary>
   // ============================================================
   [InitializeOnLoad]
   public static class SkillInstaller
   {

   #region Fields

      private const string PackageSkillsDir      = "Packages/com.inonego.uni-cli/skills";
      private const string ProjectClaudeSkillsDir = ".claude/skills";
      private const string ProjectCodexSkillsDir  = ".agents/skills";

      private static readonly string[] ProjectSkillDirs =
      {
         ProjectClaudeSkillsDir,
         ProjectCodexSkillsDir
      };

   #endregion

   #region Constructors

      static SkillInstaller()
      {
         if (CLISettings.SkillAutoSync)
         {
            Sync();
         }
      }

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Checks if any skill is installed in the project.
      /// </summary>
      // ------------------------------------------------------------
      public static bool IsInstalled
      {
         get
         {
            if (!Directory.Exists(PackageSkillsDir))
            {
               return false;
            }

            foreach (var sourceDir in Directory.GetDirectories(PackageSkillsDir))
            {
               var name = Path.GetFileName(sourceDir);

               foreach (var projectSkillDir in ProjectSkillDirs)
               {
                  var dest = Path.Combine(projectSkillDir, name);

                  if (Directory.Exists(dest))
                  {
                     return true;
                  }
               }
            }

            return false;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Copies all skill folders from the package to the project.
      /// </summary>
      // ------------------------------------------------------------
      public static void Sync()
      {
         if (!Directory.Exists(PackageSkillsDir))
         {
            return;
         }

         foreach (var sourceDir in Directory.GetDirectories(PackageSkillsDir))
         {
            var name = Path.GetFileName(sourceDir);

            foreach (var projectSkillDir in ProjectSkillDirs)
            {
               var dest = Path.Combine(projectSkillDir, name);

               CopyDirectory(sourceDir, dest);
            }
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Removes all package skill folders from the project.
      /// </summary>
      // ------------------------------------------------------------
      public static void Remove()
      {
         if (!Directory.Exists(PackageSkillsDir))
         {
            return;
         }

         foreach (var sourceDir in Directory.GetDirectories(PackageSkillsDir))
         {
            RemoveSkill(Path.GetFileName(sourceDir));
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Removes a skill folder from every supported agent skills directory.
      /// </summary>
      // ------------------------------------------------------------
      private static void RemoveSkill(string skillName)
      {
         foreach (var projectSkillDir in ProjectSkillDirs)
         {
            var dest = Path.Combine(projectSkillDir, skillName);

            if (Directory.Exists(dest))
            {
               Directory.Delete(dest, true);
            }

            RemoveEmptySkillsDir(projectSkillDir);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Removes an empty agent skills directory and its empty parent.
      /// </summary>
      // ------------------------------------------------------------
      private static void RemoveEmptySkillsDir(string projectSkillDir)
      {
         if (!Directory.Exists(projectSkillDir) || Directory.GetFileSystemEntries(projectSkillDir).Length > 0)
         {
            return;
         }

         Directory.Delete(projectSkillDir);

         var parent = Path.GetDirectoryName(projectSkillDir);

         if (!string.IsNullOrEmpty(parent) &&
             Directory.Exists(parent) &&
             Directory.GetFileSystemEntries(parent).Length == 0)
         {
            Directory.Delete(parent);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Recursively copies a directory.
      /// </summary>
      // ------------------------------------------------------------
      private static void CopyDirectory(string source, string destination)
      {
         Directory.CreateDirectory(destination);

         foreach (var file in Directory.GetFiles(source))
         {
            var fileName = Path.GetFileName(file);

            // Skip .meta files
            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
               continue;
            }

            File.Copy(file, Path.Combine(destination, fileName), true);
         }

         foreach (var dir in Directory.GetDirectories(source))
         {
            var dirName = Path.GetFileName(dir);

            CopyDirectory(dir, Path.Combine(destination, dirName));
         }
      }

   #endregion

   }
}
