using System;

using UnityEngine;

using InoCLI;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// Eval command group.
   /// Provides C# and Lua code evaluation.
   /// </summary>
   // ============================================================
   public static class EvalGroup
   {

   #region Commands

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Evaluates C# code.
      /// <br/> Compiles on background thread, executes on main thread.
      /// </summary>
      // ----------------------------------------------------------------------
      [CLICommand("eval", "cs", description = "Evaluate C# code")]
      public static async Awaitable<object> EvalCS(CommandArgs args)
      {
         var (assembly, className) = CSharpEval.Compile(args);

         await Awaitable.MainThreadAsync();

         return CSharpEval.Execute(assembly, className);
      }


   #endregion

   }
}
