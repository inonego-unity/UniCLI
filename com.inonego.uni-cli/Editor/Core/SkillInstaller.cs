using System;
using System.IO;

using UnityEngine;

using UnityEditor;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Copies Claude skill files from the package to the project.
   /// </summary>
   // ============================================================
   [InitializeOnLoad]
   public static class SkillInstaller
   {

   #region Fields

      private const string PackageSkillsDir = "Packages/com.inonego.uni-cli/.claude/skills";
      private const string ProjectSkillsDir = ".claude/skills";

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

            foreach (var dir in Directory.GetDirectories(PackageSkillsDir))
            {
               var name = Path.GetFileName(dir);
               var dest = Path.Combine(ProjectSkillsDir, name);

               if (Directory.Exists(dest))
               {
                  return true;
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

         foreach (var dir in Directory.GetDirectories(PackageSkillsDir))
         {
            var name = Path.GetFileName(dir);
            var dest = Path.Combine(ProjectSkillsDir, name);

            CopyDirectory(dir, dest);
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

         foreach (var dir in Directory.GetDirectories(PackageSkillsDir))
         {
            var name = Path.GetFileName(dir);
            var dest = Path.Combine(ProjectSkillsDir, name);

            if (!Directory.Exists(dest))
            {
               continue;
            }

            Directory.Delete(dest, true);
         }

         // Remove empty parent directories
         if (Directory.Exists(ProjectSkillsDir) && Directory.GetFileSystemEntries(ProjectSkillsDir).Length == 0)
         {
            Directory.Delete(ProjectSkillsDir);

            var parent = Path.GetDirectoryName(ProjectSkillsDir);

            while (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
            {
               if (Directory.GetFileSystemEntries(parent).Length > 0)
               {
                  break;
               }

               Directory.Delete(parent);
               parent = Path.GetDirectoryName(parent);
            }
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
            if (fileName.EndsWith(".meta"))
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
