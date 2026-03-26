using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.UniCLI
{
   using Core;

   // ========================================================================================
   /// <summary>
   /// UniCLI settings editor window.
   /// Left TabView (Settings, Commands) + right Log panel layout.
   /// </summary>
   // ========================================================================================
   public partial class CLISettingsWindow : EditorWindow
   {

   #region Constants

      private const string USSPath      = "Packages/com.inonego.uni-cli/Editor/UI/UXML/CLISettingsWindow.uss";
      private const string SettingsUXML = "Packages/com.inonego.uni-cli/Editor/UI/UXML/CLISettingsWindow.Settings.uxml";
      private const string CommandsUXML = "Packages/com.inonego.uni-cli/Editor/UI/UXML/CLISettingsWindow.Commands.uxml";
      private const string LogUXML      = "Packages/com.inonego.uni-cli/Editor/UI/UXML/CLISettingsWindow.Log.uxml";

   #endregion

   #region Fields — Settings

      private VisualElement  statusRow       = null;
      private VisualElement  statusDot       = null;
      private Label          statusLabel     = null;
      private Label          pipeLabel       = null;
      private Button         startStopButton = null;
      private Toggle         autoStartToggle = null;
      private Toggle         enabledToggle   = null;

   #endregion

   #region Fields — Commands

      private VisualElement      commandsContainer   = null;
      private VisualElement      commandOverlay       = null;
      private ToolbarSearchField commandsSearchField  = null;
      private ToolbarToggle      filterCommandsToggle = null;

   #endregion

   #region Fields — Log

      private ListView      logListView        = null;
      private TextField     logDetailTextField  = null;
      private ToolbarToggle logInfoToggle       = null;
      private ToolbarToggle logWarningToggle    = null;
      private ToolbarToggle logErrorToggle      = null;
      private ToolbarToggle logAutoScrollToggle = null;

      private readonly List<CLILogEntry>            filteredLogs = new List<CLILogEntry>();
      private readonly ConcurrentQueue<CLILogEntry> pendingLogs  = new ConcurrentQueue<CLILogEntry>();

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Opens the UniCLI settings window from the menu.
      /// </summary>
      // ------------------------------------------------------------
      [MenuItem("Window/UniCLI Settings")]
      public static void ShowWindow()
      {
         CLISettingsWindow window = GetWindow<CLISettingsWindow>("UniCLI Settings");

         window.minSize = new Vector2(700, 420);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Builds the UIToolkit UI.
      /// </summary>
      // ------------------------------------------------------------
      private void CreateGUI()
      {
         var root = rootVisualElement;

         var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(USSPath);

         if (styleSheet != null)
         {
            root.styleSheets.Add(styleSheet);
         }

         root.AddToClassList("cli-root");

         // Left: Settings / Commands tabs
         var tabView = new TabView();

         tabView.style.flexGrow = 1;

         tabView.Add(BuildSettingsTab());
         tabView.Add(BuildCommandsTab());

         // Apply tab header background + underline after layout is ready
         tabView.RegisterCallback<AttachToPanelEvent>(_ =>
         {
            tabView.schedule.Execute(() =>
            {
               PatchTabHeaderBackground(tabView);
               UpdateTabIndicators(tabView);
            });
         });

         tabView.activeTabChanged += (_, _) => UpdateTabIndicators(tabView);

         // Right: Log panel (always visible)
         var logPanel = BuildLogPanel();

         // Horizontal split
         var splitView = new TwoPaneSplitView(0, 500, TwoPaneSplitViewOrientation.Horizontal);

         splitView.AddToClassList("cli-main-split");

         splitView.Add(tabView);
         splitView.Add(logPanel);

         root.Add(splitView);

         root.schedule.Execute(UpdateServerStatus).Every(500);
         root.schedule.Execute(DrainPendingLogs).Every(100);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Subscribes to log events when the window is enabled.
      /// </summary>
      // ------------------------------------------------------------
      private void OnEnable()
      {
         CLILog.LogReceived += OnLogReceived;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Unsubscribes from log events when the window is disabled.
      /// </summary>
      // ------------------------------------------------------------
      private void OnDisable()
      {
         CLILog.LogReceived -= OnLogReceived;
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Sets tab header element backgrounds via inline styles.
      /// </summary>
      // ------------------------------------------------------------
      private static void PatchTabHeaderBackground(TabView tabView)
      {
         var bg = new StyleColor(new Color32(0x32, 0x32, 0x32, 0xFF));

         foreach (string cls in new[]
         {
            "unity-tab-view__header-container",
            "unity-tab-view__header-tabs-strip",
            "unity-tab-view__header-filler"
         })
         {
            var el = tabView.Q(className: cls);

            if (el != null)
            {
               el.style.backgroundColor = bg;
            }
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Loads a UXML template and clones it into the target element.
      /// </summary>
      // ------------------------------------------------------------
      private static void LoadUXML(string path, VisualElement target)
      {
         var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);

         asset?.CloneTree(target);
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// Updates tab selection indicators via inline styles.
      /// Inline styles are required because Unity's built-in USS overrides
      /// custom USS for tab header elements.
      /// </summary>
      // ----------------------------------------------------------------------
      private static readonly Color TabActiveColor   = new Color32(0x4D, 0x88, 0xFF, 0xFF);
      private static readonly Color TabInactiveColor = new Color32(0x80, 0x80, 0x80, 0xFF);

      private static void UpdateTabIndicators(TabView tabView)
      {
         Tab currentTab = tabView.activeTab;

         // TabView reparents tab headers outside of Tab elements,
         // so we must query from the tabView root to find them.
         var underlines = new List<VisualElement>();
         var labels     = new List<VisualElement>();

         tabView.Query(className: "unity-tab__header-underline").ForEach(e => underlines.Add(e));
         tabView.Query(className: "unity-tab__header-label").ForEach(e => labels.Add(e));

         var tabs = new List<Tab>();

         tabView.Query<Tab>().ForEach(t => tabs.Add(t));

         for (int i = 0; i < tabs.Count; i++)
         {
            bool isActive = tabs[i] == currentTab;

            if (i < underlines.Count)
            {
               underlines[i].style.backgroundColor = isActive
                  ? new StyleColor(TabActiveColor)
                  : new StyleColor(Color.clear);

               underlines[i].style.height = 2;
            }

            if (i < labels.Count)
            {
               labels[i].style.color = isActive
                  ? new StyleColor(TabActiveColor)
                  : new StyleColor(TabInactiveColor);

               labels[i].style.unityFontStyleAndWeight = isActive
                  ? FontStyle.Bold
                  : FontStyle.Normal;
            }

            tabs[i].EnableInClassList("cli-tab--active", isActive);
         }
      }

   #endregion

   }
}
