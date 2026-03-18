using System;

using UnityEngine;

namespace inonego.UniCLI.Group
{
   using Attribute;
   using Core;

   // ============================================================
   /// <summary>
   /// Eval command group.
   /// Provides C# and Lua code evaluation.
   /// </summary>
   // ============================================================
   [CLIGroup("eval", "Code evaluation")]
   public class EvalGroup
   {

   #region Commands

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Evaluates C# code.
      /// <br/> Compiles on background thread, executes on main thread.
      /// </summary>
      // ----------------------------------------------------------------------
      [CLICommand("cs", "Evaluate C# code")]
      public static async Awaitable<object> EvalCS(CommandArgs args)
      {
         var (assembly, className) = CSharpEval.Compile(args);

         await Awaitable.MainThreadAsync();

         return CSharpEval.Execute(assembly, className);
      }


   #endregion

   }
}
