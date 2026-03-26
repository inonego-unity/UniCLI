using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using InoCLI;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Parses incoming JSON requests, routes them to commands,
   /// and formats unified JSON responses.
   /// </summary>
   // ============================================================
   public static class CLIRouter
   {

   #region Methods

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Handles a raw JSON request string.
      /// <br/> Parses positionals/optionals, resolves via CommandRegistry,
      /// <br/> and returns a JSON response string.
      /// </summary>
      // ----------------------------------------------------------------------
      public static async Awaitable<string> HandleRequest(string json)
      {
         try
         {
            var parsed = ParseRequest(json);

            // Handle --help
            if (parsed.Has("help"))
            {
               return CreateHelpResponse(parsed);
            }

            // Resolve + invoke via CLIRegistry (handles async/sync)
            object result = await CLIRegistry.Invoke(parsed);

            // Serialize on main thread (Unity properties like go.scene require it)
            await Awaitable.MainThreadAsync();
            JToken serialized = ResultSerializer.Serialize(result);

            return CreateSuccessResponse(serialized);
         }
         catch (CLIException ex)
         {
            return CreateErrorResponse(ex.Code, ex.Message);
         }
         catch (Exception ex)
         {
            var inner = ex.InnerException ?? ex;

            if (inner is CLIException cliEx)
            {
               return CreateErrorResponse(cliEx.Code, cliEx.Message);
            }

            return CreateErrorResponse(Constants.Error.InternalError, inner.Message);
         }
      }

   #endregion

   #region Request Parsing

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Parses a JSON request into CommandArgs.
      /// <br/> Format: {"positionals":[...],"optionals":{...}}
      /// </summary>
      // ----------------------------------------------------------------------
      private static CommandArgs ParseRequest(string json)
      {
         JObject root;

         try
         {
            root = JObject.Parse(json);
         }
         catch (JsonReaderException ex)
         {
            throw new CLIException(Constants.Error.ParseError, $"Invalid JSON: {ex.Message}");
         }

         var positionals = new List<string>();
         var optionals   = new Dictionary<string, List<string>>();

         // Positionals
         var posArray = root.Value<JArray>("positionals");

         if (posArray != null)
         {
            foreach (var item in posArray)
            {
               positionals.Add(item.ToString());
            }
         }

         if (positionals.Count == 0)
         {
            throw new CLIException(Constants.Error.ParseError, "Missing positionals.");
         }

         // Optionals
         var optObj = root.Value<JObject>("optionals");

         if (optObj != null)
         {
            foreach (var prop in optObj.Properties())
            {
               var values = new List<string>();

               if (prop.Value.Type == JTokenType.Array)
               {
                  foreach (var v in (JArray)prop.Value)
                  {
                     values.Add(v.ToString());
                  }
               }
               else if (prop.Value.Type != JTokenType.Null)
               {
                  values.Add(prop.Value.ToString());
               }

               optionals[prop.Name] = values;
            }
         }

         return new CommandArgs
         {
            Positionals = positionals,
            Optionals   = optionals
         };
      }

   #endregion

   #region Response Formatting

      // ------------------------------------------------------------
      /// <summary>
      /// Creates a success JSON response.
      /// </summary>
      // ------------------------------------------------------------
      private static string CreateSuccessResponse(JToken result)
      {
         var response = new JObject
         {
            ["success"] = true,
            ["result"]  = result
         };

         return response.ToString(Formatting.None);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Creates an error JSON response.
      /// </summary>
      // ------------------------------------------------------------
      public static string CreateErrorResponse(string code, string message)
      {
         return CreateErrorResponse(code, message, null);
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// Creates an error JSON response with optional stacktrace.
      /// </summary>
      // ----------------------------------------------------------------------
      public static string CreateErrorResponse(string code, string message, string stacktrace)
      {
         var error = new JObject
         {
            ["code"]    = code,
            ["message"] = message
         };

         if (!string.IsNullOrEmpty(stacktrace))
         {
            error["stacktrace"] = stacktrace;
         }

         var response = new JObject
         {
            ["success"] = false,
            ["error"]   = error
         };

         return response.ToString(Formatting.None);
      }

   #endregion

   #region Help

      // ------------------------------------------------------------
      /// <summary>
      /// Creates a help response from the command registry.
      /// </summary>
      // ------------------------------------------------------------
      private static string CreateHelpResponse(CommandArgs parsed)
      {
         JToken result;

         string root = parsed[0];

         if (string.IsNullOrEmpty(root) || root == "help")
         {
            // List all roots
            var roots = new JArray();

            foreach (string r in CLIRegistry.GetRoots())
            {
               roots.Add(new JObject { ["name"] = r });
            }

            result = roots;
         }
         else
         {
            // List commands under this root
            var cmds = CLIRegistry.GetCommands(root);
            var list = new JArray();

            foreach (var cmd in cmds)
            {
               list.Add(new JObject
               {
                  ["name"]        = cmd.Key,
                  ["description"] = cmd.Description
               });
            }

            result = list;
         }

         return CreateSuccessResponse(result);
      }

   #endregion

   }
}
