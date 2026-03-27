using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;

using InoCLI;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// UI Toolkit visual tree inspection and debugging commands.
   /// </summary>
   // ============================================================
   public static class UITKCommandGroup
   {

   #region Inspect

      // ------------------------------------------------------------
      /// <summary>
      /// Inspect visual tree hierarchy with optional style/layout/sheet info.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("uitk", "inspect", description = "Inspect visual tree")]
      public static object Inspect(CommandArgs args)
      {
         var window = GetTargetWindow(args);
         var root   = window.rootVisualElement;

         int depth = args.GetInt("depth", 1);

         string pathStr = args["path"];

         if (pathStr != null)
         {
            root = WalkPath(root, pathStr);
         }

         // --style: flag만이면 전체, 값이 있으면 선택 속성
         List<string> styleProps = null;
         bool includeStyle       = args.Has("style");

         if (includeStyle)
         {
            styleProps = args.All("style", null);

            // flag만 쓴 경우 빈 리스트가 올 수 있음 → null로 통일 (전체)
            if (styleProps != null && styleProps.Count == 0)
            {
               styleProps = null;
            }
         }

         bool includeLayout = args.Flag("layout");
         bool includeSheet  = args.Flag("sheet");

         string basePath = pathStr ?? "";

         return SerializeTree(root, basePath, 0, depth, includeStyle, styleProps, includeLayout, includeSheet);
      }

   #endregion

   #region Query

      // ------------------------------------------------------------
      /// <summary>
      /// Find elements by CSS-like selector (#name, .class, Type).
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("uitk", "query", description = "Find elements by CSS selector")]
      public static object Query(CommandArgs args)
      {
         string selector = args[0];

         if (string.IsNullOrEmpty(selector))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Selector required.");
         }

         var window  = GetTargetWindow(args);
         var root    = window.rootVisualElement;
         var results = new JArray();

         var elements = QueryElements(root, selector);

         foreach (var el in elements)
         {
            string indexPath = BuildIndexPath(el, root);

            var item = new JObject
            {
               ["element"] = SerializeElement(el, indexPath)
            };

            results.Add(item);
         }

         return results;
      }

   #endregion

   #region Class

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> USS class operations (add, remove, toggle).
      /// <br/> --window and --path are required.
      /// </summary>
      // ----------------------------------------------------------------------
      [CLICommand("uitk", "class", description = "USS class operations (add, remove, toggle)")]
      public static object Class(CommandArgs args)
      {
         string sub = args[0];

         if (sub != "add" && sub != "remove" && sub != "toggle")
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Use: uitk class add|remove|toggle <class> --window <id> --path <path>");
         }

         string className = args[1];

         if (string.IsNullOrEmpty(className))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Class name required.");
         }

         string windowId = args["window"];

         if (windowId == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, "--window is required.");
         }

         string pathStr = args["path"];

         if (pathStr == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, "--path is required.");
         }

         var window = GetTargetWindow(args);
         var root   = window.rootVisualElement;
         var el     = WalkPath(root, pathStr);

         if (sub == "add")
         {
            el.AddToClassList(className);
         }
         else if (sub == "remove")
         {
            el.RemoveFromClassList(className);
         }
         else if (sub == "toggle")
         {
            el.ToggleInClassList(className);
         }

         string indexPath = BuildIndexPath(el, root);

         return new JObject
         {
            ["element"] = SerializeElement(el, indexPath)
         };
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Get target EditorWindow from --window (required).
      /// </summary>
      // ------------------------------------------------------------
      private static EditorWindow GetTargetWindow(CommandArgs args)
      {
         string windowArg = args["window"];

         if (windowArg == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, "--window is required. Use: editor window list");
         }

         if (!int.TryParse(windowArg, out int id))
         {
            throw new CLIException(Constants.Error.InvalidArgs, $"Invalid window id: {windowArg}");
         }

         var win = EditorUtility.EntityIdToObject(id) as EditorWindow;

         if (win == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, $"Window {id} not found.");
         }

         return win;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Walk index path (e.g. "0/2/1") to reach a child element.
      /// </summary>
      // ------------------------------------------------------------
      private static VisualElement WalkPath(VisualElement root, string path)
      {
         if (string.IsNullOrEmpty(path))
         {
            return root;
         }

         var current = root;
         var parts   = path.Split('/');

         foreach (var part in parts)
         {
            if (!int.TryParse(part, out int index))
            {
               throw new CLIException(Constants.Error.InvalidArgs, $"Invalid path segment: {part}");
            }

            if (index < 0 || index >= current.hierarchy.childCount)
            {
               throw new CLIException
               (
                  Constants.Error.InvalidArgs,
                  $"Path index {index} out of range (child count: {current.hierarchy.childCount})."
               );
            }

            current = current.hierarchy[index];
         }

         return current;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Build index path string from element up to root.
      /// </summary>
      // ------------------------------------------------------------
      private static string BuildIndexPath(VisualElement element, VisualElement root)
      {
         if (element == root)
         {
            return "";
         }

         var segments = new List<int>();
         var current  = element;

         while (current != null && current != root)
         {
            var parent = current.hierarchy.parent;

            if (parent == null)
            {
               break;
            }

            int index = parent.hierarchy.IndexOf(current);
            segments.Add(index);
            current = parent;
         }

         segments.Reverse();

         return string.Join("/", segments);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Serialize element info as unified element block.
      /// </summary>
      // ------------------------------------------------------------
      private static JObject SerializeElement(VisualElement el, string indexPath)
      {
         var classes = new JArray();

         foreach (var cls in el.GetClasses())
         {
            classes.Add(cls);
         }

         return new JObject
         {
            ["type"]        = el.GetType().Name,
            ["name"]        = string.IsNullOrEmpty(el.name) ? null : el.name,
            ["classes"]     = classes,
            ["index_path"]  = indexPath,
            ["child_count"] = el.hierarchy.childCount
         };
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Recursively serialize visual tree with optional
      /// <br/> style, layout, and stylesheet info.
      /// </summary>
      // ----------------------------------------------------------------------
      private static JObject SerializeTree
      (
         VisualElement el, string parentPath, int currentDepth, int maxDepth,
         bool includeStyle, List<string> styleProps,
         bool includeLayout, bool includeSheet
      )
      {
         string indexPath = parentPath;

         var node = new JObject
         {
            ["element"] = SerializeElement(el, indexPath)
         };

         if (includeStyle)
         {
            node["resolved"] = SerializeResolvedStyle(el.resolvedStyle, styleProps);
         }

         if (includeLayout)
         {
            node["layout"]      = SerializeLayout(el);
            node["world_bound"] = SerializeWorldBound(el);
         }

         if (includeSheet)
         {
            node["stylesheets"] = SerializeStyleSheets(el);
         }

         var children = new JArray();

         // maxDepth -1 = unlimited
         if (maxDepth == -1 || currentDepth < maxDepth)
         {
            for (int i = 0; i < el.hierarchy.childCount; i++)
            {
               var child     = el.hierarchy[i];
               string childPath = string.IsNullOrEmpty(indexPath) ? i.ToString() : $"{indexPath}/{i}";

               children.Add
               (
                  SerializeTree
                  (
                     child, childPath, currentDepth + 1, maxDepth,
                     includeStyle, styleProps,
                     includeLayout, includeSheet
                  )
               );
            }
         }

         node["children"] = children;

         return node;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Serialize resolved style properties.
      /// </summary>
      // ------------------------------------------------------------
      private static JObject SerializeResolvedStyle(IResolvedStyle style, List<string> props)
      {
         var all = new Dictionary<string, Func<IResolvedStyle, object>>
         {
            ["color"]                     = s => FormatColor(s.color),
            ["background_color"]          = s => FormatColor(s.backgroundColor),
            ["border_top_color"]          = s => FormatColor(s.borderTopColor),
            ["border_right_color"]        = s => FormatColor(s.borderRightColor),
            ["border_bottom_color"]       = s => FormatColor(s.borderBottomColor),
            ["border_left_color"]         = s => FormatColor(s.borderLeftColor),
            ["font_size"]                 = s => s.fontSize,
            ["opacity"]                   = s => s.opacity,
            ["display"]                   = s => s.display.ToString(),
            ["visibility"]                = s => s.visibility.ToString(),
            ["position"]                  = s => s.position.ToString(),
            ["flex_direction"]            = s => s.flexDirection.ToString(),
            ["flex_grow"]                 = s => s.flexGrow,
            ["flex_shrink"]               = s => s.flexShrink,
            ["flex_wrap"]                 = s => s.flexWrap.ToString(),
            ["align_items"]               = s => s.alignItems.ToString(),
            ["align_self"]                = s => s.alignSelf.ToString(),
            ["align_content"]             = s => s.alignContent.ToString(),
            ["justify_content"]           = s => s.justifyContent.ToString(),
            ["width"]                     = s => s.width,
            ["height"]                    = s => s.height,
            ["min_width"]                 = s => s.minWidth,
            ["max_width"]                 = s => s.maxWidth,
            ["min_height"]                = s => s.minHeight,
            ["max_height"]                = s => s.maxHeight,
            ["margin_top"]                = s => s.marginTop,
            ["margin_right"]              = s => s.marginRight,
            ["margin_bottom"]             = s => s.marginBottom,
            ["margin_left"]               = s => s.marginLeft,
            ["padding_top"]               = s => s.paddingTop,
            ["padding_right"]             = s => s.paddingRight,
            ["padding_bottom"]            = s => s.paddingBottom,
            ["padding_left"]              = s => s.paddingLeft,
            ["border_top_width"]          = s => s.borderTopWidth,
            ["border_right_width"]        = s => s.borderRightWidth,
            ["border_bottom_width"]       = s => s.borderBottomWidth,
            ["border_left_width"]         = s => s.borderLeftWidth,
            ["border_top_left_radius"]    = s => s.borderTopLeftRadius,
            ["border_top_right_radius"]   = s => s.borderTopRightRadius,
            ["border_bottom_left_radius"] = s => s.borderBottomLeftRadius,
            ["border_bottom_right_radius"]= s => s.borderBottomRightRadius,
            ["top"]                       = s => s.top,
            ["right"]                     = s => s.right,
            ["bottom"]                    = s => s.bottom,
            ["left"]                      = s => s.left
         };

         var result  = new JObject();
         var entries = (props != null) ? props : all.Keys.ToList();

         foreach (var key in entries)
         {
            if (all.TryGetValue(key, out var getter))
            {
               try
               {
                  var value = getter(style);

                  if (value is float f)
                  {
                     result[key] = f;
                  }
                  else if (value is string s)
                  {
                     result[key] = s;
                  }
                  else if (value is int i)
                  {
                     result[key] = i;
                  }
                  else
                  {
                     result[key] = value?.ToString();
                  }
               }
               catch
               {
                  result[key] = null;
               }
            }
         }

         return result;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Serialize layout Rect (parent-relative).
      /// </summary>
      // ------------------------------------------------------------
      private static JObject SerializeLayout(VisualElement el)
      {
         var rect = el.layout;

         return new JObject
         {
            ["x"]      = rect.x,
            ["y"]      = rect.y,
            ["width"]  = rect.width,
            ["height"] = rect.height
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Serialize worldBound (window-relative).
      /// </summary>
      // ------------------------------------------------------------
      private static JObject SerializeWorldBound(VisualElement el)
      {
         var rect = el.worldBound;

         return new JObject
         {
            ["x"]      = rect.x,
            ["y"]      = rect.y,
            ["width"]  = rect.width,
            ["height"] = rect.height
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Serialize stylesheets directly attached to element.
      /// </summary>
      // ------------------------------------------------------------
      private static JArray SerializeStyleSheets(VisualElement el)
      {
         var result = new JArray();

         for (int i = 0; i < el.styleSheets.count; i++)
         {
            var sheet = el.styleSheets[i];

            if (sheet == null)
            {
               continue;
            }

            string path = AssetDatabase.GetAssetPath(sheet);

            result.Add(new JObject
            {
               ["name"] = sheet.name,
               ["path"] = string.IsNullOrEmpty(path) ? null : path
            });
         }

         return result;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Query elements by CSS-like selector.
      /// </summary>
      // ------------------------------------------------------------
      private static List<VisualElement> QueryElements(VisualElement root, string selector)
      {
         var results = new List<VisualElement>();

         if (selector.StartsWith("#"))
         {
            string name = selector.Substring(1);
            root.Query<VisualElement>(name).ForEach(e => results.Add(e));
         }
         else if (selector.StartsWith("."))
         {
            string className = selector.Substring(1);
            root.Query<VisualElement>(className: className).ForEach(e => results.Add(e));
         }
         else
         {
            // Type name match
            string typeName = selector;

            root.Query<VisualElement>().ForEach(e =>
            {
               if (string.Equals(e.GetType().Name, typeName, StringComparison.OrdinalIgnoreCase))
               {
                  results.Add(e);
               }
            });
         }

         return results;
      }

      private static string FormatColor(Color c)
      {
         return $"rgba({(int)(c.r * 255)},{(int)(c.g * 255)},{(int)(c.b * 255)},{c.a:F2})";
      }


   #endregion

   }
}
