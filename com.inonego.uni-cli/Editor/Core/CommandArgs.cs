using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Holds parsed arguments and options for a CLI command.
   /// Created by CLIRouter from the incoming JSON request.
   /// </summary>
   // ============================================================
   public class CommandArgs
   {

   #region Fields

      private readonly string[] args;
      private readonly Dictionary<string, object> options;

      internal Dictionary<string, object> RawOptions => options;

   #endregion

   #region Constructors

      public CommandArgs(string[] args, Dictionary<string, object> options)
      {
         this.args    = args ?? Array.Empty<string>();
         this.options = options ?? new Dictionary<string, object>();
      }

   #endregion

   #region Positional Arguments

      // ------------------------------------------------------------
      /// <summary>
      /// Number of positional arguments.
      /// </summary>
      // ------------------------------------------------------------
      public int ArgCount => args.Length;

      // ------------------------------------------------------------
      /// <summary>
      /// Gets a positional argument by index, or null if out of range.
      /// </summary>
      // ------------------------------------------------------------
      public string Arg(int index)
      {
         if (index < 0 || index >= args.Length)
         {
            return null;
         }

         return args[index];
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets a positional argument by index with a default value.
      /// </summary>
      // ------------------------------------------------------------
      public string Arg(int index, string defaultValue)
      {
         return Arg(index) ?? defaultValue;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets a positional argument as an integer.
      /// </summary>
      // ------------------------------------------------------------
      public int ArgInt(int index, int defaultValue = 0)
      {
         var value = Arg(index);

         if (value != null && int.TryParse(value, out int result))
         {
            return result;
         }

         return defaultValue;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets all positional arguments from a starting index.
      /// </summary>
      // ------------------------------------------------------------
      public string[] ArgsFrom(int index)
      {
         if (index < 0 || index >= args.Length)
         {
            return Array.Empty<string>();
         }

         return args.Skip(index).ToArray();
      }

   #endregion

   #region Named Options

      // ------------------------------------------------------------
      /// <summary>
      /// Checks if a named option or flag exists.
      /// </summary>
      // ------------------------------------------------------------
      public bool Has(string name)
      {
         return options.ContainsKey(name);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets a named option value as string, or null.
      /// </summary>
      // ------------------------------------------------------------
      public string Option(string name)
      {
         if (options.TryGetValue(name, out object value))
         {
            return value?.ToString();
         }

         return null;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets a named option value with a default.
      /// </summary>
      // ------------------------------------------------------------
      public string Option(string name, string defaultValue)
      {
         return Option(name) ?? defaultValue;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets a named option value as integer.
      /// </summary>
      // ------------------------------------------------------------
      public int OptionInt(string name, int defaultValue = 0)
      {
         var value = Option(name);

         if (value != null && int.TryParse(value, out int result))
         {
            return result;
         }

         return defaultValue;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Gets a boolean flag value.
      /// Returns true if the flag exists.
      /// </summary>
      // ------------------------------------------------------------
      public bool Flag(string name)
      {
         if (!options.TryGetValue(name, out object value))
         {
            return false;
         }

         if (value is bool b)
         {
            return b;
         }

         return true;
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// Gets a repeated option as a string array.
      /// Handles both single value and array values.
      /// </summary>
      // ----------------------------------------------------------------------
      public string[] OptionArray(string name)
      {
         if (!options.TryGetValue(name, out object value))
         {
            return Array.Empty<string>();
         }

         if (value is IEnumerable<object> enumerable)
         {
            return enumerable.Select(v => v?.ToString()).ToArray();
         }

         if (value is string s)
         {
            return new string[] { s };
         }

         return new string[] { value?.ToString() };
      }

   #endregion

   }
}
