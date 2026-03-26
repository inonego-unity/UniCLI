using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

using InoIPC;

namespace UniCLI
{
   // ============================================================
   /// <summary>
   /// Client-side wait handler. Polls editor state until a
   /// condition is met. Survives domain reloads by reconnecting.
   /// </summary>
   // ============================================================
   static class WaitHandler
   {

   #region Methods

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Polls editor state until a condition is met.
      /// <br/> Runs client-side to survive domain reloads.
      /// </summary>
      // ----------------------------------------------------------------------
      public static int Handle(List<string> positionals, string pipeName, int timeoutSeconds, bool pretty, bool silent = false)
      {
         string condition = positionals.Count > 1 ? positionals[1] : null;

         if (string.IsNullOrEmpty(condition))
         {
            Console.Error.WriteLine("Error: Condition required (not_compiling, playing, not_playing, compiling)");
            return 1;
         }

         var    start    = DateTime.Now;
         string stateReq = "{\"positionals\":[\"editor\",\"state\"]}";

         while (true)
         {
            try
            {
               using var transport = new NamedPipeTransport(pipeName);
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
                  int elapsed = (int)(DateTime.Now - start).TotalMilliseconds;
                  var response = IpcResponse.Success
                  (
                     new Dictionary<string, object>
                     {
                        ["condition"] = condition,
                        ["elapsed"]   = elapsed
                     }
                  );

                  if (!silent)
                  {
                     Console.WriteLine(response.RawJson);
                  }

                  return 0;
               }
            }
            catch
            {
               // Server down (domain reload) — keep trying
            }

            if (timeoutSeconds >= 0 && (DateTime.Now - start).TotalSeconds >= timeoutSeconds)
            {
               var error = IpcResponse.Error("TIMEOUT", "wait timed out");
               Console.Error.WriteLine(error.RawJson);
               return 1;
            }

            Thread.Sleep(500);
         }
      }

   #endregion

   }
}
