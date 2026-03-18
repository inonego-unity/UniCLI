using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

using UnityEditor;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// TCP server for UniCLI.
   /// Accepts CLI client connections and routes requests.
   /// </summary>
   // ============================================================
   [InitializeOnLoad]
   public class CLIServer
   {

   #region Fields

      private static TcpListener listener;
      private static TcpClient currentClient;
      private static CancellationTokenSource cancellationTokenSource;
      private static readonly object lockObj = new object();
      private static bool isRunning = false;
      private static int activePort = 0;

   #endregion

   #region Constructors

      // ============================================================
      /// <summary>
      /// Auto-starts the server on editor load based on settings.
      /// </summary>
      // ============================================================
      static CLIServer()
      {
         if (CLISettings.AutoStart && CLISettings.Enabled)
         {
            EditorApplication.delayCall += () =>
            {
               Start();
            };
         }
      }

   #endregion

   #region Properties

      // ------------------------------------------------------------
      /// <summary>
      /// Whether the server is currently running.
      /// </summary>
      // ------------------------------------------------------------
      public static bool IsRunning => isRunning;

      // ------------------------------------------------------------
      /// <summary>
      /// The port the server is currently listening on.
      /// </summary>
      // ------------------------------------------------------------
      public static int Port => activePort;

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Starts the TCP server and initializes the command registry.
      /// </summary>
      // ------------------------------------------------------------
      public static void Start()
      {
         if (isRunning)
         {
            CLILog.Warning("Server is already running.");
            return;
         }

         try
         {
            CLIRegistry.Initialize();
            InstanceRegistry.CleanStale();

            cancellationTokenSource = new CancellationTokenSource();

            int basePort = CLISettings.Port;

            for (int attempt = 0; attempt < CLISettings.MaxPortAttempts; attempt++)
            {
               int port = basePort + attempt;

               try
               {
                  listener = new TcpListener(IPAddress.Loopback, port);
                  listener.Start();
                  activePort = port;
                  isRunning  = true;

                  InstanceRegistry.Register(port);

                  CLILog.Info($"CLI server started on port {port}.");

                  AcceptClientsAsync(cancellationTokenSource.Token);
                  return;
               }
               catch (SocketException)
               {
                  listener = null;
               }
            }

            CLILog.Error($"Failed to start server — no available port (tried {basePort}~{basePort + CLISettings.MaxPortAttempts - 1}).");
         }
         catch (Exception ex)
         {
            CLILog.Error("Failed to start server.", ex);
            isRunning = false;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Stops the TCP server and disconnects the client.
      /// </summary>
      // ------------------------------------------------------------
      public static void Stop()
      {
         if (!isRunning)
         {
            return;
         }

         try
         {
            isRunning = false;

            if (cancellationTokenSource != null)
            {
               cancellationTokenSource.Cancel();
               cancellationTokenSource.Dispose();
               cancellationTokenSource = null;
            }

            DisconnectClient();

            if (listener != null)
            {
               listener.Stop();
               listener = null;
            }

            InstanceRegistry.Unregister(activePort);
            CLILog.Info($"CLI server stopped (port {activePort}).");
            activePort = 0;
         }
         catch (Exception ex)
         {
            CLILog.Error("Error while stopping server.", ex);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Restarts the server.
      /// </summary>
      // ------------------------------------------------------------
      public static void Restart()
      {
         Stop();
         Start();
      }

   #endregion

   #region Client Handling

      // ------------------------------------------------------------
      /// <summary>
      /// Accepts incoming client connections.
      /// </summary>
      // ------------------------------------------------------------
      private static async void AcceptClientsAsync(CancellationToken token)
      {
         await Awaitable.BackgroundThreadAsync();

         while (!token.IsCancellationRequested)
         {
            try
            {
               TcpClient client = await listener.AcceptTcpClientAsync();

               lock (lockObj)
               {
                  DisconnectClient();
                  currentClient = client;
               }

               CLILog.Info("Client connected.");

               HandleClientAsync(client, token);
            }
            catch (ObjectDisposedException)
            {
               break;
            }
            catch (SocketException) when (token.IsCancellationRequested)
            {
               break;
            }
            catch (Exception ex)
            {
               if (!token.IsCancellationRequested)
               {
                  CLILog.Error("Error while accepting client.", ex);
               }
            }
         }
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Handles a connected client.
      /// <br/> Reads frames, routes to CLIRouter, writes responses.
      /// </summary>
      // ----------------------------------------------------------------------
      private static async void HandleClientAsync(TcpClient client, CancellationToken token)
      {
         await Awaitable.BackgroundThreadAsync();

         NetworkStream stream = null;

         try
         {
            stream = client.GetStream();

            while (!token.IsCancellationRequested && client.Connected)
            {
               string requestJson = await ReadFrameAsync(stream, token);

               if (requestJson == null)
               {
                  break;
               }

               CLILog.Info($"Request: {requestJson}");

               // Handle modal commands directly (no main thread needed)
               string modalResponse = TryHandleModalCommand(requestJson);

               if (modalResponse != null)
               {
                  await WriteFrameAsync(stream, modalResponse, token);
                  CLILog.Info($"Response: {modalResponse}");
                  continue;
               }

               // Start modal watcher alongside command execution
               var modalCTS     = new CancellationTokenSource();
               var modalTask    = ModalWatcher.WatchAsync(modalCTS.Token, 300);
               bool modalSent   = false;

               // Monitor for modal in background while command executes
               _ = Task.Run(async () =>
               {
                  var modal = await modalTask;

                  if (modal != null && !modalCTS.IsCancellationRequested)
                  {
                     try
                     {
                        string response = CreateModalResponse(modal);
                        await WriteFrameAsync(stream, response, token);
                        CLILog.Info($"Response (modal): {response}");
                        modalSent = true;
                     }
                     catch {}
                  }
               });

               string responseJson = await CLIRouter.HandleRequest(requestJson);
               modalCTS.Cancel();

               if (!modalSent)
               {
                  await WriteFrameAsync(stream, responseJson, token);
                  CLILog.Info($"Response: {responseJson}");
               }
            }
         }
         catch (OperationCanceledException) {}
         catch (ObjectDisposedException) {}
         catch (IOException) {}
         catch (Exception ex)
         {
            if (!token.IsCancellationRequested)
            {
               CLILog.Error("Error while handling client.", ex);
            }
         }
         finally
         {
            lock (lockObj)
            {
               if (currentClient == client)
               {
                  currentClient = null;
               }
            }

            try
            {
               stream?.Close();
               client?.Close();
            }
            catch {}

            CLILog.Info("Client disconnected.");
         }
      }

   #endregion

   #region Frame Protocol

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Reads a length-prefixed frame from the stream.
      /// <br/> Format: [4-byte big-endian uint32 length][UTF-8 JSON body]
      /// </summary>
      // ----------------------------------------------------------------------
      private static async Task<string> ReadFrameAsync(NetworkStream stream, CancellationToken token)
      {
         byte[] lengthBuffer = new byte[4];
         int bytesRead = await ReadExactAsync(stream, lengthBuffer, 0, 4, token);

         if (bytesRead < 4)
         {
            return null;
         }

         if (BitConverter.IsLittleEndian)
         {
            Array.Reverse(lengthBuffer);
         }

         uint length = BitConverter.ToUInt32(lengthBuffer, 0);

         if (length == 0)
         {
            return string.Empty;
         }

         byte[] bodyBuffer = new byte[length];
         bytesRead = await ReadExactAsync(stream, bodyBuffer, 0, (int)length, token);

         if (bytesRead < (int)length)
         {
            return null;
         }

         return Encoding.UTF8.GetString(bodyBuffer);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Reads an exact number of bytes from the stream.
      /// </summary>
      // ------------------------------------------------------------
      private static async Task<int> ReadExactAsync
      (
         NetworkStream stream,
         byte[] buffer, int offset, int count,
         CancellationToken token
      )
      {
         int totalRead = 0;

         while (totalRead < count)
         {
            token.ThrowIfCancellationRequested();

            int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, token);

            if (read == 0)
            {
               break;
            }

            totalRead += read;
         }

         return totalRead;
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Writes a length-prefixed frame to the stream.
      /// <br/> Format: [4-byte big-endian uint32 length][UTF-8 JSON body]
      /// </summary>
      // ----------------------------------------------------------------------
      private static async Task WriteFrameAsync(NetworkStream stream, string json, CancellationToken token)
      {
         byte[] bodyBytes   = Encoding.UTF8.GetBytes(json);
         byte[] lengthBytes = BitConverter.GetBytes((uint)bodyBytes.Length);

         if (BitConverter.IsLittleEndian)
         {
            Array.Reverse(lengthBytes);
         }

         await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, token);
         await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, token);
         await stream.FlushAsync(token);
      }

   #endregion

   #region Modal

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Handles modal commands without main thread.
      /// <br/> Returns JSON response, or null if not a modal command.
      /// </summary>
      // ----------------------------------------------------------------------
      private static string TryHandleModalCommand(string json)
      {
         try
         {
            var root = JObject.Parse(json);

            string group   = root.Value<string>("group");
            string command = root.Value<string>("command") ?? "";

            if (group != "editor" || (command != "modal" && command != ""))
            {
               return null;
            }

            // Check if it's "editor modal" by looking at args
            var argsArray = root.Value<JArray>("args");
            string subCommand = null;

            if (command == "modal")
            {
               subCommand = argsArray != null && argsArray.Count > 0
                  ? argsArray[0]?.ToString()
                  : null;
            }
            else if (command == "" && argsArray != null && argsArray.Count > 0)
            {
               if (argsArray[0]?.ToString() == "modal")
               {
                  subCommand = argsArray.Count > 1 ? argsArray[1]?.ToString() : null;
               }
               else
               {
                  return null;
               }
            }
            else
            {
               return null;
            }

            // editor modal — detect
            if (subCommand == null || subCommand == "")
            {
               var modal = ModalWatcher.Detect();

               if (modal == null)
               {
                  return "{\"success\":true,\"result\":null}";
               }

               return CreateModalDetectResponse(modal);
            }

            // editor modal click <button>
            if (subCommand == "click")
            {
               string buttonText = null;

               if (command == "modal" && argsArray != null && argsArray.Count > 1)
               {
                  buttonText = argsArray[1]?.ToString();
               }
               else if (command == "" && argsArray != null && argsArray.Count > 2)
               {
                  buttonText = argsArray[2]?.ToString();
               }

               if (string.IsNullOrEmpty(buttonText))
               {
                  return CLIRouter.CreateErrorResponse("INVALID_ARGS", "Button text required.");
               }

               bool clicked = ModalWatcher.ClickButton(buttonText);

               if (!clicked)
               {
                  return CLIRouter.CreateErrorResponse("INVALID_ARGS", $"Button '{buttonText}' not found.");
               }

               return "{\"success\":true,\"result\":null}";
            }

            return null;
         }
         catch
         {
            return null;
         }
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Creates a success response for explicit modal detection.
      /// <br/> Used by "editor modal" command.
      /// </summary>
      // ----------------------------------------------------------------------
      private static string CreateModalDetectResponse(ModalWatcher.ModalInfo modal)
      {
         var buttons = new JArray();

         foreach (string button in modal.Buttons)
         {
            buttons.Add(button);
         }

         var result = new JObject
         {
            ["title"]   = modal.Title,
            ["buttons"] = buttons
         };

         var response = new JObject
         {
            ["success"] = true,
            ["result"]  = result
         };

         return response.ToString(Formatting.None);
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Creates an error response for auto-detected modal.
      /// <br/> Used when a modal blocks command execution.
      /// </summary>
      // ----------------------------------------------------------------------
      private static string CreateModalResponse(ModalWatcher.ModalInfo modal)
      {
         var buttons = new JArray();

         foreach (string button in modal.Buttons)
         {
            buttons.Add(button);
         }

         var error = new JObject
         {
            ["code"]    = "MODAL",
            ["message"] = modal.Title,
            ["buttons"] = buttons
         };

         var response = new JObject
         {
            ["success"] = false,
            ["error"]   = error
         };

         return response.ToString(Formatting.None);
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Disconnects the current client.
      /// </summary>
      // ------------------------------------------------------------
      private static void DisconnectClient()
      {
         if (currentClient != null)
         {
            try
            {
               currentClient.GetStream().Close();
            }
            catch {}

            try
            {
               currentClient.Close();
            }
            catch {}

            currentClient = null;
         }
      }

   #endregion

   }
}
