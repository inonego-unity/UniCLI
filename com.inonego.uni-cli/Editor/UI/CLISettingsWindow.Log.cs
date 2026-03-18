using System.Collections;
using System.Collections.Generic;

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace inonego.UniCLI
{
   using Core;

   // ========================================================================================
   /// <summary>
   /// CLISettingsWindow — Log panel logic.
   /// </summary>
   // ========================================================================================
   public partial class CLISettingsWindow
   {

   #region Log Panel

      // ------------------------------------------------------------
      /// <summary>
      /// Builds the log panel (always visible on the right side).
      /// </summary>
      // ------------------------------------------------------------
      private VisualElement BuildLogPanel()
      {
         var panel = new VisualElement();

         panel.AddToClassList("cli-log-panel");

         LoadUXML(LogUXML, panel);

         logInfoToggle       = panel.Q<ToolbarToggle>("filter-info");
         logWarningToggle    = panel.Q<ToolbarToggle>("filter-warning");
         logErrorToggle      = panel.Q<ToolbarToggle>("filter-error");
         logAutoScrollToggle = panel.Q<ToolbarToggle>("filter-autoscroll");

         var clearButton = panel.Q<Button>("btn-clear");

         if (clearButton != null)
         {
            clearButton.clicked += OnClearLog;
         }

         logInfoToggle?.RegisterValueChangedCallback(_    => RebuildFilteredLogs());
         logWarningToggle?.RegisterValueChangedCallback(_ => RebuildFilteredLogs());
         logErrorToggle?.RegisterValueChangedCallback(_   => RebuildFilteredLogs());

         // ListView
         logListView = new ListView(filteredLogs, 22, MakeLogItem, BindLogItem);

         logListView.AddToClassList("cli-log-list");
         logListView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
         logListView.selectionType        = SelectionType.Single;

         logListView.RegisterCallback<AttachToPanelEvent>(_ =>
         {
            var sv = logListView.Q<ScrollView>();

            if (sv != null)
            {
               sv.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            }
         });

         // Update detail panel on selection change
         logListView.selectionChanged += OnLogSelectionChanged;

         // Detail panel
         var detailPane = new ScrollView(ScrollViewMode.Vertical);

         detailPane.AddToClassList("cli-log-detail");
         detailPane.verticalScrollerVisibility  = ScrollerVisibility.Auto;
         detailPane.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

         logDetailTextField = new TextField();

         logDetailTextField.isReadOnly = true;
         logDetailTextField.multiline  = true;
         logDetailTextField.AddToClassList("cli-log-detail-text");

         detailPane.Add(logDetailTextField);

         // Vertical split: list (top) / detail (bottom, fixed height)
         var splitView = new TwoPaneSplitView(1, 100, TwoPaneSplitViewOrientation.Vertical);

         splitView.AddToClassList("cli-log-split");

         splitView.Add(logListView);
         splitView.Add(detailPane);

         // Container for the split view (provides relative positioning)
         var splitContainer = new VisualElement();

         splitContainer.AddToClassList("cli-log-split-container");

         splitContainer.Add(splitView);

         var logContent = panel.Q("log-content");

         if (logContent != null)
         {
            logContent.Add(splitContainer);
         }
         else
         {
            panel.Add(splitContainer);
         }

         RebuildFilteredLogs();

         return panel;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handles the Clear button click.
      /// </summary>
      // ------------------------------------------------------------
      private void OnClearLog()
      {
         CLILog.ClearHistory();

         filteredLogs.Clear();

         logListView?.RefreshItems();

         UpdateLogCount();

         if (logDetailTextField != null)
         {
            logDetailTextField.value = string.Empty;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Updates the detail panel when the log list selection changes.
      /// </summary>
      // ------------------------------------------------------------
      private void OnLogSelectionChanged(IEnumerable<object> selection)
      {
         if (logDetailTextField == null)
         {
            return;
         }

         foreach (object item in selection)
         {
            if (item is CLILogEntry entry)
            {
               logDetailTextField.value = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}]\n\n{entry.Message}";
            }

            return;
         }

         logDetailTextField.value = string.Empty;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Creates a VisualElement for a log item.
      /// </summary>
      // ------------------------------------------------------------
      private VisualElement MakeLogItem()
      {
         var label = new Label();

         label.AddToClassList("cli-log-item");

         return label;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Binds a log entry to a VisualElement.
      /// </summary>
      // ------------------------------------------------------------
      private void BindLogItem(VisualElement element, int index)
      {
         if (element is not Label label || index < 0 || index >= filteredLogs.Count)
         {
            return;
         }

         CLILogEntry entry = filteredLogs[index];

         label.text = entry.ToString();

         label.RemoveFromClassList("cli-log-item--info");
         label.RemoveFromClassList("cli-log-item--warning");
         label.RemoveFromClassList("cli-log-item--error");

         switch (entry.Level)
         {
            case CLILogLevel.Info:
               label.AddToClassList("cli-log-item--info");
               break;

            case CLILogLevel.Warning:
               label.AddToClassList("cli-log-item--warning");
               break;

            case CLILogLevel.Error:
               label.AddToClassList("cli-log-item--error");
               break;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Log event callback. May be called from a background thread.
      /// </summary>
      // ------------------------------------------------------------
      private void OnLogReceived(CLILogEntry entry)
      {
         pendingLogs.Enqueue(entry);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Drains pending logs on the main thread.
      /// </summary>
      // ------------------------------------------------------------
      private void DrainPendingLogs()
      {
         if (pendingLogs.IsEmpty)
         {
            return;
         }

         bool added = false;

         while (pendingLogs.TryDequeue(out CLILogEntry entry))
         {
            if (!ShouldShowLog(entry))
            {
               continue;
            }

            filteredLogs.Add(entry);

            added = true;
         }

         if (!added)
         {
            return;
         }

         logListView?.RefreshItems();

         UpdateLogCount();

         if (logAutoScrollToggle != null && logAutoScrollToggle.value && filteredLogs.Count > 0)
         {
            logListView?.ScrollToItem(filteredLogs.Count - 1);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Rebuilds the filtered log list from history based on filter settings.
      /// </summary>
      // ------------------------------------------------------------
      private void RebuildFilteredLogs()
      {
         filteredLogs.Clear();

         foreach (CLILogEntry entry in CLILog.GetHistory())
         {
            if (ShouldShowLog(entry))
            {
               filteredLogs.Add(entry);
            }
         }

         logListView?.RefreshItems();

         UpdateLogCount();
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Determines whether a log entry should be shown based on current filter settings.
      /// </summary>
      // ------------------------------------------------------------
      private bool ShouldShowLog(CLILogEntry entry)
      {
         switch (entry.Level)
         {
            case CLILogLevel.Info:
               return logInfoToggle == null || logInfoToggle.value;

            case CLILogLevel.Warning:
               return logWarningToggle == null || logWarningToggle.value;

            case CLILogLevel.Error:
               return logErrorToggle == null || logErrorToggle.value;

            default:
               return true;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Updates the log count label.
      /// </summary>
      // ------------------------------------------------------------
      private void UpdateLogCount()
      {
         var history = CLILog.GetHistory();

         int infoCount    = 0;
         int warningCount = 0;
         int errorCount   = 0;

         foreach (CLILogEntry entry in history)
         {
            switch (entry.Level)
            {
               case CLILogLevel.Info:    infoCount++;    break;
               case CLILogLevel.Warning: warningCount++; break;
               case CLILogLevel.Error:   errorCount++;   break;
            }
         }

         if (logInfoToggle != null)    logInfoToggle.text    = $"Info {infoCount}";
         if (logWarningToggle != null) logWarningToggle.text = $"Warning {warningCount}";
         if (logErrorToggle != null)   logErrorToggle.text   = $"Error {errorCount}";
      }

   #endregion

   }
}
