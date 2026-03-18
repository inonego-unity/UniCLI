using System;
using System.Collections.Generic;

using UnityEngine;

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
      /// <br/> Parses group/command/args/options, invokes on the main thread,
      /// <br/> and returns a JSON response string.
      /// </summary>
      // ----------------------------------------------------------------------
      public static async Awaitable<string> HandleRequest(string json)
      {
         try
         {
            var request = ParseRequest(json);

            string group   = request.Group;
            string command = request.Command;

            // Handle --help
            if (request.Help)
            {
               return CreateHelpResponse(group, command);
            }

            // Build CommandArgs
            var args = new CommandArgs(request.Args, request.Options);

            // Invoke (async commands handle their own threading,
            // sync commands get Awaitable.MainThreadAsync via CLIRegistry)
            object result = await CLIRegistry.InvokeCommand(group, command, args);

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
            // Unwrap TargetInvocationException from reflection
            var inner = ex.InnerException ?? ex;

            if (inner is CLIException cliEx)
            {
               return CreateErrorResponse(cliEx.Code, cliEx.Message);
            }

            return CreateErrorResponse(ErrorCode.INTERNAL_ERROR, inner.Message);
         }
      }

   #endregion

   #region Request Parsing

      // ------------------------------------------------------------
      /// <summary>
      /// Holds parsed request data.
      /// </summary>
      // ------------------------------------------------------------
      private class ParsedRequest
      {
         public string   Group;
         public string   Command;
         public string[] Args;
         public Dictionary<string, object> Options;
         public bool     Help;
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// Parses a JSON string into a ParsedRequest.
      /// Expected: {"group":"...","command":"...","args":[...],"options":{...}}
      /// </summary>
      // ----------------------------------------------------------------------
      private static ParsedRequest ParseRequest(string json)
      {
         JObject root;

         try
         {
            root = JObject.Parse(json);
         }
         catch (JsonReaderException ex)
         {
            throw new CLIException(ErrorCode.PARSE_ERROR, $"Invalid JSON: {ex.Message}");
         }

         // Group (required)
         string group = root.Value<string>("group");

         if (string.IsNullOrEmpty(group))
         {
            throw new CLIException(ErrorCode.PARSE_ERROR, "Missing 'group' field.");
         }

         // Command (optional)
         string command = root.Value<string>("command") ?? "";

         // Args (optional string array)
         string[] args = Array.Empty<string>();
         JArray argsArray = root.Value<JArray>("args");

         if (argsArray != null)
         {
            var argList = new List<string>();

            foreach (var item in argsArray)
            {
               argList.Add(item.ToString());
            }

            args = argList.ToArray();
         }

         // Options (optional object)
         Dictionary<string, object> options = null;
         JObject optsObj = root.Value<JObject>("options");

         if (optsObj != null)
         {
            options = ParseOptions(optsObj);
         }

         // Help flag
         bool help = options != null && options.ContainsKey("help");

         return new ParsedRequest
         {
            Group   = group,
            Command = command,
            Args    = args,
            Options = options,
            Help    = help
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Parses a JObject into a Dictionary.
      /// </summary>
      // ------------------------------------------------------------
      private static Dictionary<string, object> ParseOptions(JObject obj)
      {
         var dict = new Dictionary<string, object>();

         foreach (var prop in obj.Properties())
         {
            dict[prop.Name] = ParseJToken(prop.Value);
         }

         return dict;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Converts a JToken to a C# object.
      /// </summary>
      // ------------------------------------------------------------
      private static object ParseJToken(JToken token)
      {
         switch (token.Type)
         {
            case JTokenType.String:
               return token.Value<string>();

            case JTokenType.Integer:
               return token.Value<int>();

            case JTokenType.Float:
               return token.Value<double>();

            case JTokenType.Boolean:
               return token.Value<bool>();

            case JTokenType.Null:
               return null;

            case JTokenType.Array:
               var list = new List<object>();
               foreach (var item in (JArray)token)
               {
                  list.Add(ParseJToken(item));
               }
               return list;

            case JTokenType.Object:
               var dict = new Dictionary<string, object>();
               foreach (var prop in ((JObject)token).Properties())
               {
                  dict[prop.Name] = ParseJToken(prop.Value);
               }
               return dict;

            default:
               return token.ToString();
         }
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
      /// Creates a help response for --help requests.
      /// </summary>
      // ------------------------------------------------------------
      private static string CreateHelpResponse(string group, string command)
      {
         JToken result;

         if (string.IsNullOrEmpty(group) || group == "help")
         {
            var groups = new JArray();

            foreach (var kvp in CLIRegistry.GetGroups())
            {
               groups.Add(new JObject
               {
                  ["name"]        = kvp.Key,
                  ["description"] = kvp.Value
               });
            }

            result = groups;
         }
         else if (string.IsNullOrEmpty(command))
         {
            var cmds = CLIRegistry.GetCommands(group);
            var list = new JArray();

            foreach (var cmd in cmds)
            {
               list.Add(new JObject
               {
                  ["name"]        = cmd.Name,
                  ["description"] = cmd.Description
               });
            }

            result = list;
         }
         else
         {
            var cmds = CLIRegistry.GetCommands(group);
            var cmd  = cmds.Find(c => c.Name == command);

            if (cmd != null)
            {
               result = new JObject
               {
                  ["name"]        = cmd.Name,
                  ["description"] = cmd.Description
               };
            }
            else
            {
               result = JValue.CreateNull();
            }
         }

         return CreateSuccessResponse(result);
      }

   #endregion

   }
}
