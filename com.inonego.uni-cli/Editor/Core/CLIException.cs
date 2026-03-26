using System;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Exception with a structured error code for CLI responses.
   /// </summary>
   // ============================================================
   public class CLIException : Exception
   {

   #region Fields

      public string Code { get; }

   #endregion

   #region Constructors

      public CLIException(string code, string message) : base(message)
      {
         Code = code;
      }

      public CLIException(string code, string message, Exception inner) : base(message, inner)
      {
         Code = code;
      }

   #endregion

   }
}
