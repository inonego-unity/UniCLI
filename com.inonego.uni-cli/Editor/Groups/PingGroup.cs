using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;

using InoCLI;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// Ping command group.
   /// Returns server connectivity and project information.
   /// </summary>
   // ============================================================
   public static class PingGroup
   {

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Returns server and project information.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("ping", description = "Check server connectivity and get project info")]
      public static object Ping(CommandArgs args)
      {
         var result = new JObject
         {
            ["pipe"]     = CLIServer.PipeName,
            ["project"]  = Application.productName,
            ["unity"]    = Application.unityVersion,
            ["platform"] = EditorUserBuildSettings.activeBuildTarget.ToString()
         };

         return result;
      }

   #endregion

   }
}
