namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Shared constants.
   /// </summary>
   // ============================================================
   public static class Constants
   {

      // ============================================================
      /// <summary>
      /// Standard error codes for CLI error responses.
      /// </summary>
      // ============================================================
      public static class Error
      {
         public const string ParseError     = "PARSE_ERROR";
         public const string UnknownCommand = "UNKNOWN_COMMAND";
         public const string InvalidArgs    = "INVALID_ARGS";
         public const string CompileError   = "COMPILE_ERROR";
         public const string RuntimeError   = "RUNTIME_ERROR";
         public const string InternalError  = "INTERNAL_ERROR";
         public const string NotAvailable   = "NOT_AVAILABLE";
      }

   }
}
