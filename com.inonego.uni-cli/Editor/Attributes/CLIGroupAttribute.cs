using System;

namespace inonego.UniCLI.Attribute
{
   // ============================================================
   /// <summary>
   /// Marks a class as a CLI command group.
   /// The group name is used as the first argument after 'unicli'.
   /// </summary>
   // ============================================================
   [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
   public class CLIGroupAttribute : System.Attribute
   {

   #region Fields

      // ------------------------------------------------------------
      /// <summary>
      /// The group name used in CLI invocation (e.g. "scene", "go").
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

      public CLIGroupAttribute(string name, string description)
      {
         Name = name;
         Description = description;
      }

   #endregion

   }
}
