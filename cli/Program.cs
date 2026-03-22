using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;

using InoCLI;

namespace UniCLI
{
   // ============================================================
   /// <summary>
   /// UniCLI client. Sends JSON commands to Unity via TCP.
   /// </summary>
   // ============================================================
   class Program
   {

   #region Entry

      static int Main(string[] args)
      {
         if (args.Length == 0)
         {
            PrintUsage();
            return 1;
         }

         // Parse arguments (no schema — server validates)
         var parser = new ArgParser(new[] { "port", "project", "timeout", "pretty" });
         var parsed = parser.Parse(args);

         if (parsed.HelpRequested && parsed.Group == null)
         {
            PrintUsage();
            return 0;
         }

         // Resolve global options
         int    port    = -1;
         string project = null;
         bool   pretty  = false;
         int    timeout = -1;

         if (parsed.GlobalOptions.ContainsKey("port"))
         {
            int.TryParse(parsed.GlobalOptions["port"].ToString(), out port);
         }

         if (parsed.GlobalOptions.ContainsKey("project"))
         {
            project = parsed.GlobalOptions["project"].ToString();
         }

         if (parsed.GlobalOptions.ContainsKey("pretty"))
         {
            pretty = true;
         }

         if (parsed.GlobalOptions.ContainsKey("timeout"))
         {
            int.TryParse(parsed.GlobalOptions["timeout"].ToString(), out timeout);
         }

         if (port < 0)
         {
            port = GetPort(project);
         }

         if (parsed.Group == null)
         {
            PrintUsage();
            return 0;
         }

         // wait is handled client-side (survives domain reload)
         if (parsed.Group == "wait")
         {
            return HandleWait(parsed.Positional, port, timeout, pretty);
         }

         // Replace "-" with stdin content
         string stdinError = StdinReader.ReadAll(parsed.Positional);

         if (stdinError != null)
         {
            Console.Error.WriteLine($"Error: {stdinError}");
            return 1;
         }

         // Forward --help to server as an option
         if (parsed.HelpRequested)
         {
            parsed.Options["help"] = true;
         }

         // Build and send request
         var request = CliRequest.FromParsedArgs(parsed);

         try
         {
            using var transport = new TcpTransport(port);
            var client   = new CliClient(transport);
            var response = client.SendWithRetry(request, timeout);

            JsonOutput.Write(response.RawJson, pretty);
            return response.ExitCode;
         }
         catch (SocketException)
         {
            var error = CliResponse.Error("CONNECT_FAILED", "Cannot connect to Unity. Is the editor running with UniCLI?");
            JsonOutput.WriteError(error.RawJson);
            return 1;
         }
         catch (Exception ex)
         {
            var error = CliResponse.Error("ERROR", ex.Message);
            JsonOutput.WriteError(error.RawJson);
            return 1;
         }
      }

   #endregion

   #region Wait

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Polls editor state until a condition is met.
      /// <br/> Runs client-side to survive domain reloads.
      /// </summary>
      // ----------------------------------------------------------------------
      static int HandleWait(List<string> positional, int port, int timeoutSeconds, bool pretty)
      {
         string condition = positional.Count > 0 ? positional[0] : null;

         if (string.IsNullOrEmpty(condition))
         {
            Console.Error.WriteLine("Error: Condition required (not_compiling, playing, not_playing, compiling)");
            return 1;
         }

         var    start    = DateTime.Now;
         string stateReq = "{\"group\":\"editor\",\"command\":\"state\"}";

         while (true)
         {
            try
            {
               using var transport = new TcpTransport(port);
               transport.Connect();

               FrameProtocol.Send(transport, stateReq);

               string resp   = FrameProtocol.Receive(transport);
               var    doc    = JsonDocument.Parse(resp);
               var    result = doc.RootElement.GetProperty("result");

               bool met = condition switch
               {
                  "not_compiling" => !result.GetProperty("compiling").GetBoolean(),
                  "not_playing"   => !result.GetProperty("playing").GetBoolean(),
                  "compiling"     => result.GetProperty("compiling").GetBoolean(),
                  "playing"       => result.GetProperty("playing").GetBoolean(),
                  _ => throw new Exception($"Unknown condition: {condition}")
               };

               if (met)
               {
                  int    elapsed = (int)(DateTime.Now - start).TotalMilliseconds;
                  var    response = CliResponse.From(new Dictionary<string, object>
                  {
                     ["condition"] = condition,
                     ["elapsed"]   = elapsed
                  });

                  JsonOutput.Write(response.RawJson, pretty);
                  return 0;
               }
            }
            catch
            {
               // Server down (domain reload) — keep trying
            }

            if (timeoutSeconds >= 0 && (DateTime.Now - start).TotalSeconds >= timeoutSeconds)
            {
               var error = CliResponse.Error("TIMEOUT", "wait timed out");
               JsonOutput.WriteError(error.RawJson);
               return 1;
            }

            Thread.Sleep(500);
         }
      }

   #endregion

   #region Instance Discovery

      // ------------------------------------------------------------
      /// <summary>
      /// Resolves the server port from multiple sources.
      /// </summary>
      // ------------------------------------------------------------
      static int GetPort(string project = null)
      {
         string envPort = Environment.GetEnvironmentVariable("UNICLI_PORT");

         if (envPort != null && int.TryParse(envPort, out int port))
         {
            return port;
         }

         int discovered = DiscoverInstance(project);

         if (discovered > 0)
         {
            return discovered;
         }

         return 18960;
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Discovers a Unity instance from the registry.
      /// <br/> Reads ~/.unicli/instances/*.json and matches by project.
      /// </summary>
      // ----------------------------------------------------------------------
      static int DiscoverInstance(string project)
      {
         string dir = Path.Combine
         (
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".unicli", "instances"
         );

         if (!Directory.Exists(dir))
         {
            return 0;
         }

         var candidates = new List<(int port, string name, long timestamp)>();

         foreach (string file in Directory.GetFiles(dir, "*.json"))
         {
            try
            {
               string json = File.ReadAllText(file);
               var    doc  = JsonDocument.Parse(json);
               var    root = doc.RootElement;

               int    port      = root.GetProperty("port").GetInt32();
               int    pid       = root.GetProperty("pid").GetInt32();
               string name      = root.GetProperty("project_name").GetString();
               string path      = root.GetProperty("project_path").GetString();
               long   timestamp = root.GetProperty("timestamp").GetInt64();

               try
               {
                  Process.GetProcessById(pid);
               }
               catch
               {
                  File.Delete(file);
                  continue;
               }

               if (project != null)
               {
                  if (name != null && name.Contains(project, StringComparison.OrdinalIgnoreCase))
                  {
                     candidates.Add((port, name, timestamp));
                  }
                  else if (path != null && path.Contains(project, StringComparison.OrdinalIgnoreCase))
                  {
                     candidates.Add((port, name, timestamp));
                  }
               }
               else
               {
                  candidates.Add((port, name, timestamp));
               }
            }
            catch
            {
               try { File.Delete(file); } catch {}
            }
         }

         if (candidates.Count == 0)
         {
            return 0;
         }

         candidates.Sort((a, b) => b.timestamp.CompareTo(a.timestamp));
         return candidates[0].port;
      }

   #endregion

   #region Usage

      // ------------------------------------------------------------
      /// <summary>
      /// Prints CLI usage information.
      /// </summary>
      // ------------------------------------------------------------
      static void PrintUsage()
      {
         Console.WriteLine("Usage: unicli <group> [command] [args...] [--options]");
         Console.WriteLine();
         Console.WriteLine("Groups:");
         Console.WriteLine("  eval cs|lua '<code>'   Evaluate C#/Lua code");
         Console.WriteLine("  scene                  Scene management");
         Console.WriteLine("  go                     GameObject manipulation");
         Console.WriteLine("  object                 Object operations");
         Console.WriteLine("  asset                  Asset management");
         Console.WriteLine("  editor                 Editor control");
         Console.WriteLine("  console                Console log access");
         Console.WriteLine("  search                 Unity Search");
         Console.WriteLine("  capture                Screen capture");
         Console.WriteLine("  prefab                 Prefab operations");
         Console.WriteLine("  package                Package management");
         Console.WriteLine("  test                   Test management");
         Console.WriteLine("  build                  Project build");
         Console.WriteLine("  poll                   Poll async job");
         Console.WriteLine("  wait                   Wait for condition");
         Console.WriteLine("  ping                   Server connectivity");
         Console.WriteLine();
         Console.WriteLine("Global options:");
         Console.WriteLine("  --port <n>             Server port (default: auto-discover, env: UNICLI_PORT)");
         Console.WriteLine("  --project <name>       Select Unity project by name");
         Console.WriteLine("  --pretty               Pretty-print JSON output");
         Console.WriteLine("  --timeout <s>          Connection/wait timeout in seconds");
         Console.WriteLine("  --help                 Show help");
         Console.WriteLine();
         Console.WriteLine("Examples:");
         Console.WriteLine("  unicli ping");
         Console.WriteLine("  unicli eval cs 'return 1+1;'");
         Console.WriteLine("  unicli eval lua 'return CS.UnityEngine.Application.dataPath'");
         Console.WriteLine("  unicli scene list");
         Console.WriteLine("  unicli go create Player --primitive cube");
         Console.WriteLine("  cat script.cs | unicli eval cs -");
      }

   #endregion

   }
}
