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

      private const string KEY_PORT       = PREFIX + "Port";
      private const string KEY_AUTO_START = PREFIX + "AutoStart";
      private const string KEY_ENABLED    = PREFIX + "Enabled";

      private const int  DEFAULT_PORT              = 18960;
      private const int  DEFAULT_MAX_PORT_ATTEMPTS = 10;
      private const bool DEFAULT_AUTO_START        = true;
      private const bool DEFAULT_ENABLED           = true;

   #endregion

   #region Properties

      // ------------------------------------------------------------
      /// <summary>
      /// Gets or sets the TCP server port.
      /// </summary>
      // ------------------------------------------------------------
      public static int Port
      {
         get => EditorPrefs.GetInt(KEY_PORT, DEFAULT_PORT);
         set => EditorPrefs.SetInt(KEY_PORT, value);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Maximum number of ports to try when starting the server.
      /// </summary>
      // ------------------------------------------------------------
      public static int MaxPortAttempts => DEFAULT_MAX_PORT_ATTEMPTS;

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

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Resets all settings to their default values.
      /// </summary>
      // ------------------------------------------------------------
      public static void ResetToDefaults()
      {
         Port      = DEFAULT_PORT;
         AutoStart = DEFAULT_AUTO_START;
         Enabled   = DEFAULT_ENABLED;
      }

   #endregion

   }
}
