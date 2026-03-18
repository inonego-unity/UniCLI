using System;

using UnityEditor;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Manages CLI server lifecycle during Unity domain reloads.
   /// Stops the server before reload and restarts it after.
   /// </summary>
   // ============================================================
   [InitializeOnLoad]
   public static class DomainReloadHandler
   {

   #region Fields

      private const string SESSION_KEY_WAS_RUNNING = "UniCLI_WasRunning";

   #endregion

   #region Constructors

      // ============================================================
      /// <summary>
      /// Subscribes to assembly reload events on editor load.
      /// </summary>
      // ============================================================
      static DomainReloadHandler()
      {
         AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
         AssemblyReloadEvents.afterAssemblyReload  += OnAfterAssemblyReload;
      }

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Saves server state and stops it before assembly reload.
      /// </summary>
      // ------------------------------------------------------------
      private static void OnBeforeAssemblyReload()
      {
         SessionState.SetBool(SESSION_KEY_WAS_RUNNING, CLIServer.IsRunning);

         if (CLIServer.IsRunning)
         {
            CLILog.Info("Stopping server before assembly reload.");
            CLIServer.Stop();
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Restarts the server after assembly reload if it was running.
      /// </summary>
      // ------------------------------------------------------------
      private static void OnAfterAssemblyReload()
      {
         bool wasRunning = SessionState.GetBool(SESSION_KEY_WAS_RUNNING, false);

         if (wasRunning)
         {
            CLILog.Info("Restarting server after assembly reload.");
            CLIServer.Start();
         }
      }

   #endregion

   }
}
