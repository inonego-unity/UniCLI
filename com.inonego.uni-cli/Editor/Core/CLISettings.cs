using System;

using UnityEditor;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Manages UniCLI settings via EditorPrefs.
   /// </summary>
   // ============================================================
   public static class CLISettings
   {

   #region Fields

      private const string PREFIX = "UniCLI_";

      private const string KEY_AUTO_START      = PREFIX + "AutoStart";
      private const string KEY_ENABLED         = PREFIX + "Enabled";
      private const string KEY_SKILL_AUTO_SYNC = PREFIX + "SkillAutoSync";

      private const bool DEFAULT_AUTO_START      = true;
      private const bool DEFAULT_ENABLED         = true;
      private const bool DEFAULT_SKILL_AUTO_SYNC = true;

   #endregion

   #region Properties

      // ------------------------------------------------------------------
      /// <summary>
      /// Gets or sets whether to auto-start the server on editor load.
      /// </summary>
      // ------------------------------------------------------------------
      public static bool AutoStart
      {
         get => EditorPrefs.GetBool(KEY_AUTO_START, DEFAULT_AUTO_START);
         set => EditorPrefs.SetBool(KEY_AUTO_START, value);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets or sets the master enabled/disabled state.
      /// </summary>
      // ------------------------------------------------------------
      public static bool Enabled
      {
         get => EditorPrefs.GetBool(KEY_ENABLED, DEFAULT_ENABLED);
         set => EditorPrefs.SetBool(KEY_ENABLED, value);
      }

      // ------------------------------------------------------------------
      /// <summary>
      /// Gets or sets whether to auto-sync Claude skills on domain reload.
      /// </summary>
      // ------------------------------------------------------------------
      public static bool SkillAutoSync
      {
         get => EditorPrefs.GetBool(KEY_SKILL_AUTO_SYNC, DEFAULT_SKILL_AUTO_SYNC);
         set => EditorPrefs.SetBool(KEY_SKILL_AUTO_SYNC, value);
      }

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Resets all settings to their default values.
      /// </summary>
      // ------------------------------------------------------------
      public static void ResetToDefaults()
      {
         AutoStart     = DEFAULT_AUTO_START;
         Enabled       = DEFAULT_ENABLED;
         SkillAutoSync = DEFAULT_SKILL_AUTO_SYNC;
      }

   #endregion

   }
}
