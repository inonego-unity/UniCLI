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
   /// Asset database commands.
   /// </summary>
   // ============================================================
   [CLIGroup("asset", "Asset management")]
   public class AssetCommandGroup
   {

   #region Commands

      [CLICommand("import", "Import an asset")]
      public static object Import(CommandArgs args)
      {
         string path = args.Arg(0);

         if (string.IsNullOrEmpty(path))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Asset path required.");
         }

         AssetDatabase.ImportAsset(path);

         return new JObject { ["path"] = path };
      }

      [CLICommand("mkdir", "Create a folder")]
      public static object Mkdir(CommandArgs args)
      {
         string path = args.Arg(0);

         if (string.IsNullOrEmpty(path))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Folder path required.");
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

      [CLICommand("rm", "Delete an asset")]
      public static object Rm(CommandArgs args)
      {
         string path = args.Arg(0);

         if (string.IsNullOrEmpty(path))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Asset path required.");
         }

         AssetDatabase.DeleteAsset(path);

         return new JObject { ["path"] = path };
      }

      [CLICommand("mv", "Move an asset")]
      public static object Mv(CommandArgs args)
      {
         string from = args.Arg(0);
         string to   = args.Arg(1);

         if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Source and destination paths required.");
         }

         string error = AssetDatabase.MoveAsset(from, to);

         if (!string.IsNullOrEmpty(error))
         {
            throw new CLIException(ErrorCode.INTERNAL_ERROR, error);
         }

         return new JObject { ["from"] = from, ["to"] = to };
      }

      [CLICommand("cp", "Copy an asset")]
      public static object Cp(CommandArgs args)
      {
         string from = args.Arg(0);
         string to   = args.Arg(1);

         if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Source and destination paths required.");
         }

         AssetDatabase.CopyAsset(from, to);

         return new JObject { ["from"] = from, ["to"] = to };
      }

      [CLICommand("rename", "Rename an asset")]
      public static object Rename(CommandArgs args)
      {
         string path = args.Arg(0);
         string name = args.Arg(1);

         if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(name))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Path and new name required.");
         }

         string error = AssetDatabase.RenameAsset(path, name);

         if (!string.IsNullOrEmpty(error))
         {
            throw new CLIException(ErrorCode.INTERNAL_ERROR, error);
         }

         return new JObject { ["path"] = path };
      }

      [CLICommand("refresh", "Refresh the asset database")]
      public static object Refresh(CommandArgs args)
      {
         AssetDatabase.Refresh();
         return null;
      }

      [CLICommand("save", "Save assets")]
      public static object Save(CommandArgs args)
      {
         if (args.Flag("all"))
         {
            AssetDatabase.SaveAssets();
            return null;
         }

         string idStr = args.Option("id");

         if (idStr != null && int.TryParse(idStr, out int id))
         {
            var obj = EditorUtility.EntityIdToObject(id);

            if (obj == null)
            {
               throw new CLIException(ErrorCode.INVALID_ARGS, $"Object {id} not found.");
            }

            AssetDatabase.SaveAssetIfDirty(obj);
            return null;
         }

         throw new CLIException(ErrorCode.INVALID_ARGS, "--all or --id required.");
      }

   #endregion

   }
}
