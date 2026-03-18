using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Log level for UniCLI log entries.
   /// </summary>
   // ============================================================
   public enum CLILogLevel
   {
      Info,
      Warning,
      Error
   }

   // ============================================================
   /// <summary>
   /// A single log entry.
   /// </summary>
   // ============================================================
   public struct CLILogEntry
   {
      public string Message;
      public CLILogLevel Level;
      public DateTime Timestamp;

      // ------------------------------------------------------------
      /// <summary>
      /// Formats the log entry as a timestamped string.
      /// </summary>
      // ------------------------------------------------------------
      public override string ToString()
      {
         return $"[{Timestamp:HH:mm:ss}] {Message}";
      }
   }

   // ============================================================
   /// <summary>
   /// UniCLI log utility.
   /// Stores log history and raises events on new entries.
   /// </summary>
   // ============================================================
   public static class CLILog
   {

   #region Fields

      private const int MaxHistorySize = 1000;

      private static readonly List<CLILogEntry> logHistory = new List<CLILogEntry>();
      private static readonly object historyLock = new object();

   #endregion

   #region Events

      // ------------------------------------------------------------
      /// <summary>
      /// Raised when a new log entry is recorded.
      /// </summary>
      // ------------------------------------------------------------
      public static event Action<CLILogEntry> LogReceived = null;

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Logs an informational message.
      /// </summary>
      // ------------------------------------------------------------
      public static void Info(string message)
      {
         Log(message, CLILogLevel.Info);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Logs a warning message.
      /// </summary>
      // ------------------------------------------------------------
      public static void Warning(string message)
      {
         Log(message, CLILogLevel.Warning);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Logs an error message.
      /// </summary>
      // ------------------------------------------------------------
      public static void Error(string message)
      {
         Log(message, CLILogLevel.Error);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Logs an error message with an exception.
      /// </summary>
      // ------------------------------------------------------------
      public static void Error(string message, Exception ex)
      {
         Log($"{message}\n{ex}", CLILogLevel.Error);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Returns a snapshot copy of the log history.
      /// </summary>
      // ------------------------------------------------------------
      public static IReadOnlyList<CLILogEntry> GetHistory()
      {
         lock (historyLock)
         {
            return new List<CLILogEntry>(logHistory);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Clears the log history.
      /// </summary>
      // ------------------------------------------------------------
      public static void ClearHistory()
      {
         lock (historyLock)
         {
            logHistory.Clear();
         }
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Creates a log entry, adds it to history, and raises the event.
      /// </summary>
      // ------------------------------------------------------------
      private static void Log(string message, CLILogLevel level)
      {
         CLILogEntry entry = new CLILogEntry
         {
            Message   = message,
            Level     = level,
            Timestamp = DateTime.Now
         };

         lock (historyLock)
         {
            if (logHistory.Count >= MaxHistorySize)
            {
               logHistory.RemoveAt(0);
            }

            logHistory.Add(entry);
         }

         LogReceived?.Invoke(entry);
      }

   #endregion

   }
}
