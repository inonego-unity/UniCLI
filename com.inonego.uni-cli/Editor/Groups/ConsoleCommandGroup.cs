using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using UnityEditor;
using UnityEditor.Compilation;

using InoCLI;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// Console log capture and filtering.
   /// </summary>
   // ============================================================
   [InitializeOnLoad]
   public static class ConsoleCommandGroup
   {

   #region Fields

      private static readonly List<LogEntry> logBuffer = new List<LogEntry>();
      private static readonly object bufferLock = new object();
      private const int MaxBufferSize = 500;

      private struct LogEntry
      {
         public string    Message;
         public string    StackTrace;
         public string    Type;
         public DateTime  Timestamp;
      }

   #endregion

   #region Constructors

      // ======================================================================
      /// <summary>
      /// Registers log and compilation error callbacks on domain reload.
      /// </summary>
      // ======================================================================
      static ConsoleCommandGroup()
      {
         Application.logMessageReceived += OnLogMessage;
         CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
      }

      private static void OnLogMessage(string message, string stackTrace, LogType type)
      {
         AddEntry(message, stackTrace, type.ToString());
      }

      private static void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages)
      {
         foreach (var msg in messages)
         {
            if (msg.type == CompilerMessageType.Error)
            {
               AddEntry(msg.message, $"{msg.file}({msg.line},{msg.column})", "CompileError");
            }
         }
      }

      private static void AddEntry(string message, string stackTrace, string type)
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
               Type       = type,
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
      [CLICommand("console", description = "Read console logs")]
      public static object Read(CommandArgs args)
      {
         string typeFilter = args["type"];
         string sinceStr   = args["since"];

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
      [CLICommand("console", "clear", description = "Clear console log buffer")]
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
