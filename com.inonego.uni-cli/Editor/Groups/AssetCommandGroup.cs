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
   /// Asset database commands.
   /// </summary>
   // ============================================================
   public static class AssetCommandGroup
   {

   #region Commands

      [CLICommand("asset", "import", description = "Import an asset")]
      public static object Import(CommandArgs args)
      {
         string path = args[0];

         if (string.IsNullOrEmpty(path))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Asset path required.");
         }

         AssetDatabase.ImportAsset(path);

         return new JObject { ["path"] = path };
      }

      [CLICommand("asset", "mkdir", description = "Create a folder")]
      public static object Mkdir(CommandArgs args)
      {
         string path = args[0];

         if (string.IsNullOrEmpty(path))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Folder path required.");
         }

         int lastSlash = path.LastIndexOf('/');
         string parent = path.Substring(0, lastSlash);
         string name   = path.Substring(lastSlash + 1);

         string guid = AssetDatabase.CreateFolder(parent, name);

         return new JObject
         {
            ["path"] = AssetDatabase.GUIDToAssetPath(guid)
         };
      }

      [CLICommand("asset", "rm", description = "Delete an asset")]
      public static object Rm(CommandArgs args)
      {
         string path = args[0];

         if (string.IsNullOrEmpty(path))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Asset path required.");
         }

         AssetDatabase.DeleteAsset(path);

         return new JObject { ["path"] = path };
      }

      [CLICommand("asset", "mv", description = "Move an asset")]
      public static object Mv(CommandArgs args)
      {
         string from = args[0];
         string to   = args[1];

         if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Source and destination paths required.");
         }

         string error = AssetDatabase.MoveAsset(from, to);

         if (!string.IsNullOrEmpty(error))
         {
            throw new CLIException(Constants.Error.InternalError, error);
         }

         return new JObject { ["from"] = from, ["to"] = to };
      }

      [CLICommand("asset", "cp", description = "Copy an asset")]
      public static object Cp(CommandArgs args)
      {
         string from = args[0];
         string to   = args[1];

         if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Source and destination paths required.");
         }

         AssetDatabase.CopyAsset(from, to);

         return new JObject { ["from"] = from, ["to"] = to };
      }

      [CLICommand("asset", "rename", description = "Rename an asset")]
      public static object Rename(CommandArgs args)
      {
         string path = args[0];
         string name = args[1];

         if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(name))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Path and new name required.");
         }

         string error = AssetDatabase.RenameAsset(path, name);

         if (!string.IsNullOrEmpty(error))
         {
            throw new CLIException(Constants.Error.InternalError, error);
         }

         return new JObject { ["path"] = path };
      }

      [CLICommand("asset", "refresh", description = "Refresh the asset database")]
      public static object Refresh(CommandArgs args)
      {
         AssetDatabase.Refresh();
         return null;
      }

      [CLICommand("asset", "save", description = "Save assets")]
      public static object Save(CommandArgs args)
      {
         if (args.Flag("all"))
         {
            AssetDatabase.SaveAssets();
            return null;
         }

         string idStr = args["id"];

         if (idStr != null && int.TryParse(idStr, out int id))
         {
            var obj = EditorUtility.EntityIdToObject(id);

            if (obj == null)
            {
               throw new CLIException(Constants.Error.InvalidArgs, $"Object {id} not found.");
            }

            AssetDatabase.SaveAssetIfDirty(obj);
            return null;
         }

         throw new CLIException(Constants.Error.InvalidArgs, "--all or --id required.");
      }

   #endregion

   }
}
