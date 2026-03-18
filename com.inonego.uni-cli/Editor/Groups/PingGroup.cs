using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Attribute;
   using Core;

   // ============================================================
   /// <summary>
   /// Ping command group.
   /// Returns server connectivity and project information.
   /// </summary>
   // ============================================================
   [CLIGroup("ping", "Server connectivity and project info")]
   public class PingGroup
   {

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Returns server and project information.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("", "Check server connectivity and get project info")]
      public static object Ping(CommandArgs args)
      {
         var result = new JObject
         {
            ["port"]     = CLISettings.Port,
            ["project"]  = Application.productName,
            ["unity"]    = Application.unityVersion,
            ["platform"] = EditorUserBuildSettings.activeBuildTarget.ToString()
         };

         return result;
      }

   #endregion

   }
}
