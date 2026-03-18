using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;
using UnityEditor.Search;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Attribute;
   using Core;

   // ============================================================
   /// <summary>
   /// Unity Search API commands.
   /// </summary>
   // ============================================================
   [CLIGroup("search", "Unity Search")]
   public class SearchCommandGroup
   {

   #region Commands

      // ------------------------------------------------------------
      /// <summary>
      /// Searches using Unity Search API.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("", "Search using Unity Search")]
      public static object Query(CommandArgs args)
      {
         string query = args.Arg(0);

         if (string.IsNullOrEmpty(query))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Search query required.");
         }

         string provider = args.Option("provider");

         SearchContext context;

         if (provider != null)
         {
            context = SearchService.CreateContext(provider, query);
         }
         else
         {
            context = SearchService.CreateContext(query);
         }

         var results = SearchService.Request(context, SearchFlags.Synchronous);

         var output = new JArray();

         foreach (var item in results)
         {
            output.Add(new JObject
            {
               ["id"]          = item.id,
               ["label"]       = item.GetLabel(context),
               ["description"] = item.GetDescription(context)
            });
         }

         context.Dispose();
         results.Dispose();

         return output;
      }

   #endregion

   }
}
