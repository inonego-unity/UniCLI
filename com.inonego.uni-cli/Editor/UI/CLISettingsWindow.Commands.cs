using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

using InoCLI;

namespace inonego.UniCLI
{
   using Core;

   // ========================================================================================
   /// <summary>
   /// CLISettingsWindow — Commands tab logic.
   /// </summary>
   // ========================================================================================
   public partial class CLISettingsWindow
   {

   #region Commands Tab

      // ------------------------------------------------------------
      /// <summary>
      /// Builds the Commands tab.
      /// </summary>
      // ------------------------------------------------------------
      private Tab BuildCommandsTab()
      {
         var tab = new Tab("Commands");

         LoadUXML(CommandsUXML, tab);

         commandsSearchField  = tab.Q<ToolbarSearchField>("commands-search");
         filterCommandsToggle = tab.Q<ToolbarToggle>("filter-commands");
         commandsContainer    = tab.Q("commands-container");

         var refreshButton = tab.Q<Button>("btn-refresh");

         commandsSearchField?.RegisterValueChangedCallback(_ => FilterCommands());
         filterCommandsToggle?.RegisterValueChangedCallback(_ => FilterCommands());

         if (refreshButton != null)
         {
            refreshButton.clicked += RebuildCommandsTree;
         }

         // Overlay floats over the right log panel area
         commandOverlay = new VisualElement();

         commandOverlay.AddToClassList("cli-command-overlay");
         commandOverlay.style.display = DisplayStyle.None;
         commandOverlay.pickingMode   = PickingMode.Ignore;

         tab.RegisterCallback<AttachToPanelEvent>(_ =>
         {
            if (commandOverlay.parent == null)
            {
               rootVisualElement.Add(commandOverlay);
            }
         });

         RebuildCommandsTree();

         return tab;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Shows the command detail overlay near the hovered item.
      /// </summary>
      // ------------------------------------------------------------
      private void ShowCommandOverlay(CommandInfo command, VisualElement anchor)
      {
         if (commandOverlay == null || commandOverlay.parent == null)
         {
            return;
         }

         commandOverlay.Clear();

         // Header row: badge + name
         var headerRow = new VisualElement();

         headerRow.AddToClassList("cli-overlay-header");

         var badge = new Label("CMD");

         badge.AddToClassList("cli-badge");
         badge.AddToClassList("cli-badge-cmd");

         headerRow.Add(badge);

         var nameLabel = new Label(command.Key);

         nameLabel.AddToClassList("cli-overlay-name");

         headerRow.Add(nameLabel);

         commandOverlay.Add(headerRow);

         // Description
         if (!string.IsNullOrEmpty(command.Description))
         {
            var descLabel = new Label(command.Description);

            descLabel.AddToClassList("cli-overlay-desc");

            commandOverlay.Add(descLabel);
         }

         // Position to the right of the anchor item
         var anchorRect = anchor.worldBound;
         var rootRect   = rootVisualElement.worldBound;

         float left = anchorRect.xMax - rootRect.xMin + 4;
         float top  = anchorRect.yMin - rootRect.yMin;

         // Clamp to stay within window
         float maxLeft = rootRect.width - 300;
         float maxTop  = rootRect.height - 200;

         if (left > maxLeft) left = anchorRect.xMin - rootRect.xMin - 290;
         if (top > maxTop)   top  = maxTop;
         if (top < 0)        top  = 0;

         commandOverlay.style.left    = left;
         commandOverlay.style.top     = top;
         commandOverlay.style.display = DisplayStyle.Flex;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Hides the command detail overlay.
      /// </summary>
      // ------------------------------------------------------------
      private void HideCommandOverlay()
      {
         if (commandOverlay != null)
         {
            commandOverlay.style.display = DisplayStyle.None;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Rebuilds the commands list with group headers.
      /// </summary>
      // ------------------------------------------------------------
      private void RebuildCommandsTree()
      {
         if (commandsContainer == null)
         {
            return;
         }

         commandsContainer.Clear();

         List<CommandInfo> commands = CLIRegistry.GetAllCommands();

         if (commands == null || commands.Count == 0)
         {
            var placeholder = new Label("Start the server to see registered commands.");

            placeholder.AddToClassList("cli-placeholder");

            commandsContainer.Add(placeholder);

            if (filterCommandsToggle != null)
            {
               filterCommandsToggle.text = "Commands 0";
            }

            return;
         }

         int commandCount = 0;

         // Group by group name
         var groups = commands
            .GroupBy(c => c.Path.Length > 0 ? c.Path[0] : "")
            .OrderBy(g => g.Key);

         foreach (var group in groups)
         {
            var groupHeader = new Label(group.Key);

            groupHeader.AddToClassList("cli-group-header");
            groupHeader.name = $"group-{group.Key}";

            commandsContainer.Add(groupHeader);

            foreach (CommandInfo command in group)
            {
               commandCount++;

               var commandItem = new VisualElement();

               commandItem.AddToClassList("cli-action");
               commandItem.name = $"command-{command.Key}";

               var badge = new Label("CMD");

               badge.AddToClassList("cli-badge");
               badge.AddToClassList("cli-badge-cmd");

               commandItem.Add(badge);

               var nameLabel = new Label(command.Key);

               nameLabel.AddToClassList("cli-action-name");

               commandItem.Add(nameLabel);

               // Hover overlay
               CommandInfo captured = command;

               commandItem.RegisterCallback<MouseEnterEvent>(_ => ShowCommandOverlay(captured, commandItem));
               commandItem.RegisterCallback<MouseLeaveEvent>(_ => HideCommandOverlay());

               commandsContainer.Add(commandItem);
            }
         }

         if (filterCommandsToggle != null)
         {
            filterCommandsToggle.text = $"Commands {commandCount}";
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Applies search and type filters to the commands list.
      /// </summary>
      // ------------------------------------------------------------
      private void FilterCommands()
      {
         if (commandsContainer == null)
         {
            return;
         }

         string search      = commandsSearchField?.value?.ToLower() ?? "";
         bool   showCommands = filterCommandsToggle?.value ?? true;

         string currentGroup = "";

         foreach (var child in commandsContainer.Children())
         {
            if (child.ClassListContains("cli-group-header"))
            {
               currentGroup = child.name?.Replace("group-", "") ?? "";

               // Group header visibility is determined after checking its commands
               child.style.display = DisplayStyle.None;

               continue;
            }

            if (child.name == null || !child.name.StartsWith("command-"))
            {
               continue;
            }

            bool typeMatch   = showCommands;
            string cmdName   = child.name.Replace("command-", "");

            bool searchMatch = string.IsNullOrEmpty(search)
               || currentGroup.ToLower().Contains(search)
               || cmdName.ToLower().Contains(search);

            bool visible = typeMatch && searchMatch;

            child.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
         }

         // Show headers if any child command in that section is visible
         VisualElement lastGroupHeader = null;

         foreach (var child in commandsContainer.Children())
         {
            if (child.ClassListContains("cli-group-header"))
            {
               lastGroupHeader = child;

               continue;
            }

            if (child.style.display == DisplayStyle.Flex)
            {
               if (lastGroupHeader != null)
               {
                  lastGroupHeader.style.display = DisplayStyle.Flex;
               }
            }
         }
      }

   #endregion

   }
}
