using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

using UnityEditor;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using InoIPC;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Named Pipe server for UniCLI.
   /// Accepts CLI client connections and routes requests.
   /// </summary>
   // ============================================================
   [InitializeOnLoad]
   public class CLIServer
   {

   #region Fields

      private static NamedPipeServer server;
      private static Thread serverThread;
      private static string pipeName;
      private static bool isRunning = false;

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
      /// The pipe name the server is listening on.
      /// </summary>
      // ------------------------------------------------------------
      public static string PipeName => pipeName;

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Starts the Named Pipe server and initializes the command
      /// registry.
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

            int pid = Process.GetCurrentProcess().Id;
            pipeName = $"unicli-{pid}";

            server = new NamedPipeServer(pipeName);

            serverThread = new Thread(() => server.Start(async conn =>
            {
               await Awaitable.BackgroundThreadAsync();

               try
               {
                  string requestJson = conn.Receive();

                  if (requestJson == null)
                  {
                     return;
                  }

                  CLILog.Info($"Request: {requestJson}");

                  // Handle modal commands directly (no main thread needed)
                  string modalResponse = TryHandleModalCommand(requestJson);

                  if (modalResponse != null)
                  {
                     conn.Send(modalResponse);
                     CLILog.Info($"Response: {modalResponse}");
                     return;
                  }

                  // Start modal watcher alongside command execution
                  var modalCTS   = new CancellationTokenSource();
                  var modalTask  = ModalWatcher.WatchAsync(modalCTS.Token, 300);
                  bool modalSent = false;

                  _ = Task.Run(async () =>
                  {
                     var modal = await modalTask;

                     if (modal != null && !modalCTS.IsCancellationRequested)
                     {
                        try
                        {
                           string response = CreateModalResponse(modal);
                           conn.Send(response);
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
                     conn.Send(responseJson);
                     CLILog.Info($"Response: {responseJson}");
                  }
               }
               catch (OperationCanceledException) {}
               catch (System.IO.IOException) {}
               catch (Exception ex)
               {
                  CLILog.Error("Error while handling client.", ex);
               }
            }));

            serverThread.IsBackground = true;
            serverThread.Start();

            isRunning = true;

            InstanceRegistry.Register(pipeName);

            CLILog.Info($"CLI server started on pipe: {pipeName}");
         }
         catch (Exception ex)
         {
            CLILog.Error("Failed to start server.", ex);
            isRunning = false;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Stops the Named Pipe server.
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

            server?.Stop();
            serverThread = null;
            server = null;

            InstanceRegistry.Unregister(pipeName);
            CLILog.Info($"CLI server stopped (pipe: {pipeName}).");
            pipeName = null;
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

   #region Modal

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Handles modal commands without main thread.
      /// <br/> Returns JSON response, or null if not a modal command.
      /// <br/> Expects positionals/optionals format.
      /// </summary>
      // ----------------------------------------------------------------------
      private static string TryHandleModalCommand(string json)
      {
         try
         {
            var root = JObject.Parse(json);

            var positionals = root.Value<JArray>("positionals");

            if (positionals == null || positionals.Count < 2)
            {
               return null;
            }

            string first  = positionals[0]?.ToString();
            string second = positionals[1]?.ToString();

            if (first != "editor" || second != "modal")
            {
               return null;
            }

            string subCommand = positionals.Count > 2 ? positionals[2]?.ToString() : null;

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
               string buttonText = positionals.Count > 3 ? positionals[3]?.ToString() : null;

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

   }
}
