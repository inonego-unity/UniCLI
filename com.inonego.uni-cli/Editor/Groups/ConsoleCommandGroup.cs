using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Attribute;
   using Core;

   // ============================================================
   /// <summary>
   /// Console log capture and filtering.
   /// </summary>
   // ============================================================
   [CLIGroup("console", "Console log access")]
   public class ConsoleCommandGroup
   {

   #region Fields

      private static readonly List<LogEntry> logBuffer = new List<LogEntry>();
      private static readonly object bufferLock = new object();
      private const int MaxBufferSize = 500;
      private static bool registered = false;

      private struct LogEntry
      {
         public string    Message;
         public string    StackTrace;
         public string    Type;
         public DateTime  Timestamp;
      }

   #endregion

   #region Constructors

      // ============================================================
      /// <summary>
      /// Registers the log callback on first access.
      /// </summary>
      // ============================================================
      private static void EnsureRegistered()
      {
         if (registered)
         {
            return;
         }

         registered = true;
         Application.logMessageReceived += OnLogMessage;
      }

      private static void OnLogMessage(string message, string stackTrace, LogType type)
      {
         lock (bufferLock)
         {
            if (logBuffer.Count >= MaxBufferSize)
            {
               logBuffer.RemoveAt(0);
            }

            logBuffer.Add(new LogEntry
            {
               Message    = message,
               StackTrace = stackTrace,
               Type       = type.ToString(),
               Timestamp  = DateTime.Now
            });
         }
      }

   #endregion

   #region Commands

      // ------------------------------------------------------------
      /// <summary>
      /// Reads console log entries with optional filtering.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("", "Read console logs")]
      public static object Read(CommandArgs args)
      {
         EnsureRegistered();

         string typeFilter = args.Option("type");
         string sinceStr   = args.Option("since");

         DateTime? since = null;

         if (sinceStr != null && DateTime.TryParse(sinceStr, out DateTime parsed))
         {
            since = parsed;
         }

         lock (bufferLock)
         {
            IEnumerable<LogEntry> filtered = logBuffer;

            if (typeFilter != null)
            {
               filtered = filtered.Where(e => e.Type.Equals(typeFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (since.HasValue)
            {
               filtered = filtered.Where(e => e.Timestamp >= since.Value);
            }

            var result = new JArray();

            foreach (var entry in filtered)
            {
               result.Add(new JObject
               {
                  ["type"]       = entry.Type,
                  ["message"]    = entry.Message,
                  ["stacktrace"] = entry.StackTrace,
                  ["timestamp"]  = entry.Timestamp.ToString("o")
               });
            }

            return result;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Clears the log buffer.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("clear", "Clear console log buffer")]
      public static object Clear(CommandArgs args)
      {
         lock (bufferLock)
         {
            logBuffer.Clear();
         }

         return null;
      }

   #endregion

   }
}
