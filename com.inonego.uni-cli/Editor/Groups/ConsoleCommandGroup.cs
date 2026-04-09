using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

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
   /// Console log capture via UnityEditor.LogEntries reflection.
   /// </summary>
   // ============================================================
   public static class ConsoleCommandGroup
   {

   #region Reflection Cache

      private static readonly Type          LogEntriesType;
      private static readonly Type          LogEntryType;
      private static readonly MethodInfo    MethodStart;
      private static readonly MethodInfo    MethodEnd;
      private static readonly MethodInfo    MethodGetEntry;
      private static readonly MethodInfo    MethodClear;
      private static readonly FieldInfo     FieldMessage;
      private static readonly FieldInfo     FieldMode;
      private static readonly FieldInfo     FieldCallstackStart;

      // ======================================================================
      /// <summary>
      /// Initialize reflection cache for LogEntries API.
      /// </summary>
      // ======================================================================
      static ConsoleCommandGroup()
      {
         var asm = typeof(UnityEditor.Editor).Assembly;

         LogEntriesType = asm.GetType("UnityEditor.LogEntries");
         LogEntryType   = asm.GetType("UnityEditor.LogEntry");

         const BindingFlags staticPublic = BindingFlags.Static | BindingFlags.Public;
         const BindingFlags instancePublic = BindingFlags.Instance | BindingFlags.Public;

         MethodStart      = LogEntriesType?.GetMethod("StartGettingEntries", staticPublic);
         MethodEnd        = LogEntriesType?.GetMethod("EndGettingEntries", staticPublic);
         MethodGetEntry   = LogEntriesType?.GetMethod("GetEntryInternal", staticPublic);
         MethodClear      = LogEntriesType?.GetMethod("Clear", staticPublic);

         FieldMessage         = LogEntryType?.GetField("message", instancePublic);
         FieldMode            = LogEntryType?.GetField("mode", instancePublic);
         FieldCallstackStart  = LogEntryType?.GetField("callstackTextStartUTF8", instancePublic);
      }

   #endregion

   #region Commands

      // ------------------------------------------------------------
      /// <summary>
      /// Reads console log entries from Unity LogEntries.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("console", description = "Read console logs")]
      public static object Read(CommandArgs args)
      {
         if (LogEntriesType == null)
         {
            return new JArray();
         }

         var typeFilters = args.All("type", new List<string>());
         var typeFilterSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

         foreach (var filter in typeFilters)
         {
            typeFilterSet.Add(filter);
         }

         var result = new JArray();

         try
         {
            int count = (int)MethodStart.Invoke(null, null);

            for (int i = 0; i < count; i++)
            {
               var entryObj = Activator.CreateInstance(LogEntryType);
               object[] getEntryArgs = { i, entryObj };

               bool success = (bool)MethodGetEntry.Invoke(null, getEntryArgs);
               if (!success) continue;

               entryObj = getEntryArgs[1];

               string fullMessage = (string)FieldMessage.GetValue(entryObj) ?? "";
               int mode           = (int)FieldMode.GetValue(entryObj);
               int callstackStart = (int)FieldCallstackStart.GetValue(entryObj);

               string type = ModeToType(mode);

               // Filter by type if specified (include if typeFilterSet is empty or matches)
               if (typeFilterSet.Count > 0 && !typeFilterSet.Contains(type))
               {
                  continue;
               }

               // Split message and stacktrace
               string message = fullMessage;
               var stacktrace = new JArray();

               if (callstackStart > 0 && callstackStart < fullMessage.Length)
               {
                  message = fullMessage[..callstackStart].TrimEnd('\n');
                  var stackLines = fullMessage[callstackStart..].Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
                  foreach (var line in stackLines)
                  {
                     stacktrace.Add(line);
                  }
               }

               var entryJson = new JObject
               {
                  ["type"]    = type,
                  ["message"] = message
               };

               if (stacktrace.Count > 0)
               {
                  entryJson["stacktrace"] = stacktrace;
               }

               result.Add(entryJson);
            }
         }
         finally
         {
            MethodEnd.Invoke(null, null);
         }

         return result;
      }

      // ======================================================================
      /// <summary>
      /// Convert LogEntry mode flags to human-readable type string.
      /// </summary>
      // ======================================================================
      private static string ModeToType(int mode)
      {
         // Flags based on Unity's LogMessageFlags enum
         if ((mode & 2048) != 0) return "CompileError";     // ScriptCompileError
         if ((mode & 4096) != 0) return "CompileWarning";   // ScriptCompileWarning
         if ((mode & 256) != 0) return "Error";             // ScriptingError
         if ((mode & 512) != 0) return "Warning";           // ScriptingWarning
         if ((mode & 1) != 0) return "Error";               // Error
         if ((mode & 2) != 0) return "Warning";             // Warning
         return "Log";
      }

      // ======================================================================
      /// <summary>
      /// Clears the console log buffer.
      /// </summary>
      // ======================================================================
      [CLICommand("console", "clear", description = "Clear console log buffer")]
      public static object Clear(CommandArgs args)
      {
         if (MethodClear != null)
         {
            MethodClear.Invoke(null, null);
         }

         return null;
      }

   #endregion

   }
}
