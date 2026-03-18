using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

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

         // Parse global options
         int    port      = -1;
         string project   = null;
         bool   pretty    = false;
         int    timeout   = -1;
         var    remaining = new List<string>();

         for (int i = 0; i < args.Length; i++)
         {
            if (args[i] == "--port" && i + 1 < args.Length)
            {
               port = int.Parse(args[++i]);
            }
            else if (args[i] == "--project" && i + 1 < args.Length)
            {
               project = args[++i];
            }
            else if (args[i] == "--timeout" && i + 1 < args.Length)
            {
               int.TryParse(args[++i], out timeout);
            }
            else if (args[i] == "--pretty")
            {
               pretty = true;
            }
            else if (args[i] == "--help" && remaining.Count == 0)
            {
               PrintUsage();
               return 0;
            }
            else
            {
               remaining.Add(args[i]);
            }
         }

         // Resolve port: --port > UNICLI_PORT env > instance registry > default
         if (port < 0)
         {
            port = GetPort(project);
         }

         if (remaining.Count == 0)
         {
            PrintUsage();
            return 0;
         }

         string group = remaining[0];

         // wait is handled client-side (survives domain reload)
         if (group == "wait")
         {
            var waitArgs = remaining.GetRange(1, remaining.Count - 1);
            return HandleWait(waitArgs, port, timeout, pretty);
         }

         // Parse group, command, positional args, options
         string command    = "";
         var    positional = new List<string>();
         var    options    = new Dictionary<string, object>();

         if (remaining.Contains("--help"))
         {
            options["help"] = true;
         }

         int argStart = 1;

         // Second arg is command if it doesn't start with --
         if (remaining.Count > 1 && !remaining[1].StartsWith("--"))
         {
            command  = remaining[1];
            argStart = 2;
         }

         for (int i = argStart; i < remaining.Count; i++)
         {
            string arg = remaining[i];

            if (arg.StartsWith("--"))
            {
               string key = arg.Substring(2);

               if (key == "help" || key == "pretty")
               {
                  continue;
               }

               if (i + 1 < remaining.Count && !remaining[i + 1].StartsWith("--"))
               {
                  string value = remaining[++i];

                  // Handle repeated options (e.g. --using X --using Y)
                  if (options.ContainsKey(key))
                  {
                     var existing = options[key];

                     if (existing is List<object> list)
                     {
                        list.Add(value);
                     }
                     else
                     {
                        options[key] = new List<object> { existing, value };
                     }
                  }
                  else
                  {
                     options[key] = value;
                  }
               }
               else
               {
                  options[key] = true;
               }
            }
            else
            {
               positional.Add(arg);
            }
         }

         // Replace "-" with stdin content (POSIX convention)
         string stdinError = ReadStdin(positional);

         if (stdinError != null)
         {
            Console.Error.WriteLine($"Error: {stdinError}");
            return 1;
         }

         // Build JSON request
         var request = new Dictionary<string, object>
         {
            ["group"] = group
         };

         if (!string.IsNullOrEmpty(command))
         {
            request["command"] = command;
         }

         if (positional.Count > 0)
         {
            request["args"] = positional;
         }

         if (options.Count > 0)
         {
            request["options"] = options;
         }

         string requestJson = JsonSerializer.Serialize(request);

         // Send TCP request
         try
         {
            string responseJson = SendTcpWithRetry(requestJson, port, timeout);

            if (pretty)
            {
               var doc = JsonDocument.Parse(responseJson);
               responseJson = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            }

            Console.WriteLine(responseJson);

            var doc2 = JsonDocument.Parse(responseJson);

            if (doc2.RootElement.TryGetProperty("success", out var success) && !success.GetBoolean())
            {
               return 1;
            }

            return 0;
         }
         catch (SocketException)
         {
            Console.Error.WriteLine("Error: Cannot connect to Unity. Is the editor running with UniCLI?");
            return 1;
         }
         catch (Exception ex)
         {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
         }
      }

   #endregion

   #region TCP

      // ------------------------------------------------------------
      /// <summary>
      /// Sends a TCP request with retry on connection failure.
      /// </summary>
      // ------------------------------------------------------------
      static string SendTcpWithRetry(string json, int port, int timeoutSeconds = -1)
      {
         var  start   = DateTime.Now;
         bool hasSent = false;

         while (true)
         {
            try
            {
               return SendTcp(json, port, out hasSent);
            }
            catch (Exception)
            {
               // Request sent but response lost (domain reload) — treat as success
               if (hasSent)
               {
                  return "{\"success\":true,\"result\":null}";
               }

               if (timeoutSeconds >= 0 && (DateTime.Now - start).TotalSeconds >= timeoutSeconds)
               {
                  throw new IOException($"Failed to connect after {timeoutSeconds}s. Is Unity running?");
               }

               Thread.Sleep(500);
            }
         }
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Sends a length-prefixed JSON frame over TCP.
      /// <br/> Frame format: [4-byte BE uint32 length][UTF-8 body]
      /// </summary>
      // ----------------------------------------------------------------------
      static string SendTcp(string json, int port, out bool hasSent)
      {
         hasSent = false;

         using var client = new TcpClient();
         client.Connect("127.0.0.1", port);

         using var stream = client.GetStream();

         byte[] body   = Encoding.UTF8.GetBytes(json);
         byte[] length = BitConverter.GetBytes((uint)body.Length);

         if (BitConverter.IsLittleEndian)
         {
            Array.Reverse(length);
         }

         stream.Write(length, 0, 4);
         stream.Write(body, 0, body.Length);

         hasSent = true;
         stream.Flush();

         // Read response frame
         byte[] respLengthBuf = new byte[4];
         ReadExact(stream, respLengthBuf, 4);

         if (BitConverter.IsLittleEndian)
         {
            Array.Reverse(respLengthBuf);
         }

         uint respLength = BitConverter.ToUInt32(respLengthBuf, 0);

         byte[] respBody = new byte[respLength];
         ReadExact(stream, respBody, (int)respLength);

         return Encoding.UTF8.GetString(respBody);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Reads an exact number of bytes from the stream.
      /// </summary>
      // ------------------------------------------------------------
      static void ReadExact(NetworkStream stream, byte[] buffer, int count)
      {
         int totalRead = 0;

         while (totalRead < count)
         {
            int read = stream.Read(buffer, totalRead, count - totalRead);

            if (read == 0)
            {
               throw new IOException("Connection closed.");
            }

            totalRead += read;
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
               string resp = SendTcp(stateReq, port, out _);
               var    doc  = JsonDocument.Parse(resp);
               var result  = doc.RootElement.GetProperty("result");

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
                  string json    = $"{{\"success\":true,\"result\":{{\"condition\":\"{condition}\",\"elapsed\":{elapsed}}}}}";

                  if (pretty)
                  {
                     var d = JsonDocument.Parse(json);
                     json = JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true });
                  }

                  Console.WriteLine(json);
                  return 0;
               }
            }
            catch
            {
               // Server down (domain reload) — keep trying
            }

            if (timeoutSeconds >= 0 && (DateTime.Now - start).TotalSeconds >= timeoutSeconds)
            {
               Console.WriteLine("{\"success\":false,\"error\":{\"code\":\"timeout\",\"message\":\"wait timed out\"}}");
               return 1;
            }

            Thread.Sleep(500);
         }
      }

   #endregion

   #region Stdin

      // ------------------------------------------------------------
      /// <summary>
      /// Replaces "-" in positional args with stdin content.
      /// </summary>
      // ------------------------------------------------------------
      static string ReadStdin(List<string> positional)
      {
         int dashCount = positional.Count(a => a == "-");

         if (dashCount > 1)
         {
            return "Only one '-' (stdin) argument allowed.";
         }

         int dashIndex = positional.IndexOf("-");

         if (dashIndex < 0)
         {
            return null;
         }

         if (!Console.IsInputRedirected)
         {
            return "'-' requires piped input.";
         }

         string stdin = Console.In.ReadToEnd().Trim();

         if (string.IsNullOrEmpty(stdin))
         {
            return "No input from stdin.";
         }

         positional[dashIndex] = stdin;
         return null;
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
