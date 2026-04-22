using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

using InoCLI;
using InoIPC;

namespace UniCLI
{
   // ============================================================
   /// <summary>
   /// UniCLI client. Sends JSON commands to Unity via Named Pipe.
   /// </summary>
   // ============================================================
   class Program
   {

   #region Entry

      static int Main(string[] args)
      {
         Console.OutputEncoding = Encoding.UTF8;

         if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
         {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
         }

         var parsed = new ArgParser().Parse(args);

         if (parsed.Positionals.Count == 0)
         {
            PrintHelp();
            return parsed.Has("help") ? 0 : 1;
         }

         // Resolve options
         string command = parsed[0];
         string pipe    = parsed["pipe"];
         string project = parsed["project"];
         bool   pretty  = parsed.Has("pretty");
         int    timeout = parsed.Has("timeout") ? parsed.GetInt("timeout") : -1;

         if (string.IsNullOrEmpty(pipe))
         {
            try
            {
               pipe = UnityDiscovery.GetPipe(project);
            }
            catch (UnityDiscovery.AmbiguousInstanceException ex)
            {
               string detail = UnityDiscovery.FormatAmbiguityMessage(ex.Candidates);
               var    error  = IpcResponse.Error("AMBIGUOUS_INSTANCE", detail);
               Console.Error.WriteLine(error.RawJson);
               return 1;
            }
         }

         if (string.IsNullOrEmpty(pipe))
         {
            var error = IpcResponse.Error("CONNECT_FAILED", "No Unity instance found. Is the editor running with UniCLI?");
            Console.Error.WriteLine(error.RawJson);
            return 1;
         }

         // wait is handled client-side (survives domain reload)
         if (command == "wait")
         {
            return WaitHandler.Handle(parsed.Positionals, pipe, timeout, pretty);
         }

         // Forward --help to server
         if (parsed.Has("help"))
         {
            parsed.Optionals["help"] = new List<string>();
         }

         // Build and send request
         string requestJson = JsonSerializer.Serialize
         (
            new Dictionary<string, object>
            {
               ["positionals"] = parsed.Positionals,
               ["optionals"]   = parsed.Optionals
            }
         );

         try
         {
            using var transport = new NamedPipeTransport(pipe);
            var conn     = new IpcConnection(transport);
            var response = conn.Request(requestJson);

            if (pretty)
            {
               JsonHelper.Write(response.RawJson, true);
            }
            else
            {
               Console.WriteLine(response.RawJson);
            }

            // Auto-wait for domain-reload-triggering commands
            if (response.IsSuccess && !parsed.Has("no-wait"))
            {
               string waitCondition = GetAutoWaitCondition(parsed.Positionals);

               if (waitCondition != null)
               {
                  WaitHandler.Handle
                  (
                     new List<string> { "wait", waitCondition },
                     pipe, timeout, pretty, silent: true
                  );
               }
            }

            return response.IsSuccess ? 0 : 1;
         }
         catch (TimeoutException)
         {
            var error = IpcResponse.Error("CONNECT_FAILED", "Cannot connect to Unity. Is the editor running with UniCLI?");
            Console.Error.WriteLine(error.RawJson);
            return 1;
         }
         catch (Exception ex)
         {
            var error = IpcResponse.Error("ERROR", ex.Message);
            Console.Error.WriteLine(error.RawJson);
            return 1;
         }
      }

   #endregion

   #region Auto-Wait

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Returns the wait condition for commands that trigger
      /// <br/> domain reload, or null if no auto-wait is needed.
      /// </summary>
      // ----------------------------------------------------------------------
      static string GetAutoWaitCondition(List<string> positionals)
      {
         if (positionals.Count < 2)
         {
            return null;
         }

         string a = positionals[0];
         string b = positionals[1];

         if (a == "editor" && b == "play")    return "playing";
         if (a == "editor" && b == "stop")    return "not_playing";
         if (a == "asset"  && b == "refresh") return "not_compiling";

         return null;
      }

   #endregion

   #region Help

      // ------------------------------------------------------------
      /// <summary>
      /// Prints usage information referencing the skill file.
      /// </summary>
      // ------------------------------------------------------------
      static void PrintHelp()
      {
         string exeDir  = AppContext.BaseDirectory;
         string skillMd = Path.GetFullPath
         (
            Path.Combine(exeDir, ".claude", "skills", "inonego-uni-cli", "SKILL.md")
         );

         Console.WriteLine($"UniCLI — Unity Editor CLI for AI Agents\n\nReference: {skillMd}");
      }

   #endregion

   }
}
