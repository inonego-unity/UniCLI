using System;

namespace inonego.UniCLI.Group
{
   using Attribute;
   using Core;

   // ============================================================
   /// <summary>
   /// Lua code execution via UniLua.
   /// Registers under the "eval" group automatically.
   /// </summary>
   // ============================================================
   [CLIGroup("eval", "Code evaluation")]
   public class LuaEvalGroup
   {

   #region Commands

      // ------------------------------------------------------------
      /// <summary>
      /// Evaluates Lua code via UniLua.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("lua", "Evaluate Lua code")]
      public static object EvalLua(CommandArgs args)
      {
         string code = args.Arg(0);

         if (string.IsNullOrEmpty(code))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Code argument required.");
         }

         EnsureLuaEnv();

         try
         {
            object[] results = luaEnv.DoString(code);

            if (results == null || results.Length == 0)
            {
               return null;
            }

            if (results.Length == 1)
            {
               return results[0];
            }

            return results;
         }
         catch (UniLua.LuaException ex)
         {
            throw new CLIException(ErrorCode.RUNTIME_ERROR, ex.Message);
         }
      }

   #endregion

   #region Fields

      private static inonego.UniLua.LuaEnv luaEnv = null;

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Ensures the static LuaEnv instance is initialized.
      /// </summary>
      // ------------------------------------------------------------
      private static void EnsureLuaEnv()
      {
         if (luaEnv != null && !luaEnv.IsDisposed)
         {
            return;
         }

         luaEnv = new inonego.UniLua.LuaEnv();
      }

   #endregion

   }
}
