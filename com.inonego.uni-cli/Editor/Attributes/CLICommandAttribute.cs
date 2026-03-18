using System;

namespace inonego.UniCLI.Attribute
{
   // ============================================================
   /// <summary>
   /// Marks a static method as a CLI command within a group.
   /// The method must accept a single CommandArgs parameter.
   /// </summary>
   // ============================================================
   [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
   public class CLICommandAttribute : System.Attribute
   {

   #region Fields

      // ------------------------------------------------------------
      /// <summary>
      /// The command name used after the group name.
      /// Empty string means this is the default command for the group.
      /// </summary>
      // ------------------------------------------------------------
      public string Name { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// A short description shown in --help output.
      /// </summary>
      // ------------------------------------------------------------
      public string Description { get; }

   #endregion

   #region Constructors

      public CLICommandAttribute(string name, string description)
      {
         Name = name;
         Description = description;
      }

   #endregion

   }
}
