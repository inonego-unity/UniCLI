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

      private const string SkillName  = "inonego-uni-cli";
      private const string PackageDir = "Packages/com.inonego.uni-cli/.claude/skills/" + SkillName;
      private const string ProjectDir = ".claude/skills/" + SkillName;

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
      /// Checks if the skill is installed in the project.
      /// </summary>
      // ------------------------------------------------------------
      public static bool IsInstalled => Directory.Exists(ProjectDir);

      // ------------------------------------------------------------
      /// <summary>
      /// Copies skill files from the package to the project.
      /// </summary>
      // ------------------------------------------------------------
      public static void Sync()
      {
         if (!Directory.Exists(PackageDir))
         {
            return;
         }

         CopyDirectory(PackageDir, ProjectDir);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Removes the skill files from the project.
      /// </summary>
      // ------------------------------------------------------------
      public static void Remove()
      {
         if (!Directory.Exists(ProjectDir))
         {
            return;
         }

         Directory.Delete(ProjectDir, true);

         // Remove empty parent directories
         var parent = Path.GetDirectoryName(ProjectDir);

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
