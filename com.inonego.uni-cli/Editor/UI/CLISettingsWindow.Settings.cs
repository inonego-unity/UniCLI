using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.UniCLI
{
   using Core;

   // ========================================================================================
   /// <summary>
   /// CLISettingsWindow — Settings tab logic.
   /// </summary>
   // ========================================================================================
   public partial class CLISettingsWindow
   {

   #region Settings Tab

      // ------------------------------------------------------------
      /// <summary>
      /// Builds the Settings tab.
      /// </summary>
      // ------------------------------------------------------------
      private Tab BuildSettingsTab()
      {
         var tab = new Tab("Settings");

         LoadUXML(SettingsUXML, tab);

         portField       = tab.Q<IntegerField>("field-port");
         autoStartToggle = tab.Q<Toggle>("field-auto-start");
         enabledToggle   = tab.Q<Toggle>("field-enabled");
         statusRow       = tab.Q("status-row");
         statusDot       = tab.Q("status-dot");
         statusLabel     = tab.Q<Label>("status-label");
         portLabel       = tab.Q<Label>("port-label");
         startStopButton = tab.Q<Button>("btn-start-stop");

         var restartButton = tab.Q<Button>("btn-restart");
         var resetButton   = tab.Q<Button>("btn-reset");

         if (portField != null)
         {
            portField.value = CLISettings.Port;
            portField.RegisterValueChangedCallback(evt => CLISettings.Port = evt.newValue);
         }

         if (autoStartToggle != null)
         {
            autoStartToggle.value = CLISettings.AutoStart;
            autoStartToggle.RegisterValueChangedCallback(evt => CLISettings.AutoStart = evt.newValue);
         }

         if (enabledToggle != null)
         {
            enabledToggle.value = CLISettings.Enabled;
            enabledToggle.RegisterValueChangedCallback(evt => CLISettings.Enabled = evt.newValue);
         }

         if (startStopButton != null)
         {
            startStopButton.tooltip = "Start or stop the CLI server";
            startStopButton.clicked += OnStartStopClicked;
         }

         if (restartButton != null)
         {
            restartButton.tooltip = "Restart the CLI server";
            restartButton.clicked += () => CLIServer.Restart();
         }

         if (resetButton != null)
         {
            resetButton.tooltip = "Reset all settings to default values";
            resetButton.clicked += OnResetToDefaults;
         }

         UpdateServerStatus();

         return tab;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handles the Start/Stop button click.
      /// </summary>
      // ------------------------------------------------------------
      private void OnStartStopClicked()
      {
         if (CLIServer.IsRunning)
         {
            CLIServer.Stop();
         }
         else
         {
            CLIServer.Start();
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Clears inline styles on the Start/Stop button so that
      /// USS :hover / :active pseudo-classes work correctly.
      /// </summary>
      // ------------------------------------------------------------
      private static void ClearButtonInlineStyles(Button button)
      {
         button.style.backgroundColor = StyleKeyword.Null;
         button.style.borderTopColor    = StyleKeyword.Null;
         button.style.borderBottomColor = StyleKeyword.Null;
         button.style.borderLeftColor   = StyleKeyword.Null;
         button.style.borderRightColor  = StyleKeyword.Null;
         button.style.color             = StyleKeyword.Null;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handles the Reset to Defaults button click.
      /// </summary>
      // ------------------------------------------------------------
      private void OnResetToDefaults()
      {
         bool confirmed = EditorUtility.DisplayDialog
         (
            "Reset to Defaults",
            "All settings will be restored to their default values.\nAre you sure?",
            "Reset", "Cancel"
         );

         if (!confirmed)
         {
            return;
         }

         CLISettings.ResetToDefaults();

         portField?.SetValueWithoutNotify(CLISettings.Port);
         autoStartToggle?.SetValueWithoutNotify(CLISettings.AutoStart);
         enabledToggle?.SetValueWithoutNotify(CLISettings.Enabled);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Updates the server status UI elements.
      /// </summary>
      // ------------------------------------------------------------
      private void UpdateServerStatus()
      {
         if (statusRow == null)
         {
            return;
         }

         bool running = CLIServer.IsRunning;

         statusRow.EnableInClassList("cli-status-row--running", running);
         statusRow.EnableInClassList("cli-status-row--stopped", !running);

         statusDot?.EnableInClassList("cli-status-dot--running", running);
         statusDot?.EnableInClassList("cli-status-dot--stopped", !running);

         if (statusLabel != null)
         {
            statusLabel.text = running ? "Running" : "Stopped";
            statusLabel.EnableInClassList("cli-status-label--running", running);
            statusLabel.EnableInClassList("cli-status-label--stopped", !running);
         }

         if (portLabel != null)
         {
            portLabel.text = running ? $"Port {CLIServer.Port}" : "—";
            portLabel.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
         }

         if (startStopButton != null)
         {
            startStopButton.text = running ? "Stop" : "Start";
            startStopButton.EnableInClassList("cli-btn-primary", !running);
            startStopButton.EnableInClassList("cli-btn-danger", running);

            // Clear inline styles so USS :hover / :active work correctly
            ClearButtonInlineStyles(startStopButton);
         }
      }

   #endregion

   }
}
