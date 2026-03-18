using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Serializes command results to JToken.
   /// Uses JsonUtility for Unity/[Serializable] types,
   /// Newtonsoft with CLIJsonConverter for the rest.
   /// </summary>
   // ============================================================
   public static class ResultSerializer
   {

   #region Fields

      private static readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings
      {
         Converters = { new CLIJsonConverter() }
      };

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Serializes a result object to a JToken.
      /// </summary>
      // ------------------------------------------------------------
      public static JToken Serialize(object value)
      {
         if (value == null)
         {
            return JValue.CreateNull();
         }

         // JToken — already serialized
         if (value is JToken token)
         {
            return token;
         }

         var lType = value.GetType();

         // Primitives (string, bool, int, float, double, decimal, char, DateTime, etc.)
         if (Type.GetTypeCode(lType) != TypeCode.Object)
         {
            return new JValue(value);
         }

         // Enum — name string
         if (lType.IsEnum)
         {
            return new JValue(value.ToString());
         }

         // Scene
         if (value is Scene scene)
         {
            return SerializeScene(scene);
         }

         // GameObject
         if (value is GameObject go)
         {
            return SerializeGameObject(go);
         }

         // Component
         if (value is Component comp)
         {
            return SerializeComponent(comp);
         }

         // UnityEngine.Object
         if (value is UnityEngine.Object uobj)
         {
            return SerializeUnityObject(uobj);
         }

         // Dictionary
         if (value is IDictionary dict)
         {
            return SerializeDictionary(dict);
         }

         // IEnumerable (but not string)
         if (value is IEnumerable enumerable)
         {
            return SerializeEnumerable(enumerable);
         }

         // [Serializable]
         if (lType.IsSerializable)
         {
            return SerializeJsonUtility(value);
         }

         // Unity types (UnityEngine.*, UnityEditor.*)
         if (lType.Namespace?.StartsWith("Unity") == true)
         {
            return SerializeJsonUtility(value);
         }

         // Fallback — Newtonsoft with CLIJsonConverter
         return SerializeGeneric(value);
      }

   #endregion

   #region Unity Objects

      // ------------------------------------------------------------
      /// <summary>
      /// Serializes a UnityEngine.Object to base identification.
      /// </summary>
      // ------------------------------------------------------------
      private static JToken SerializeUnityObject(UnityEngine.Object obj)
      {
         if (obj == null)
         {
            return JValue.CreateNull();
         }

         return new JObject
         {
            ["instance_id"] = obj.GetInstanceID(),
            ["name"]        = obj.name,
            ["type"]        = obj.GetType().FullName
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Serializes a GameObject with extended fields.
      /// </summary>
      // ------------------------------------------------------------
      private static JToken SerializeGameObject(GameObject go)
      {
         if (go == null)
         {
            return JValue.CreateNull();
         }

         int scene = go.scene.handle;

         return new JObject
         {
            ["instance_id"] = go.GetInstanceID(),
            ["name"]        = go.name,
            ["type"]        = go.GetType().FullName,
            ["active"]      = go.activeSelf,
            ["tag"]         = go.tag,
            ["layer"]       = go.layer,
            ["scene"]       = scene
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Serializes a Component with the owning GameObject ID.
      /// </summary>
      // ------------------------------------------------------------
      private static JToken SerializeComponent(Component comp)
      {
         if (comp == null)
         {
            return JValue.CreateNull();
         }

         return new JObject
         {
            ["instance_id"] = comp.GetInstanceID(),
            ["name"]        = comp.name,
            ["type"]        = comp.GetType().FullName,
            ["game_object"] = comp.gameObject.GetInstanceID()
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Serializes a Scene struct.
      /// </summary>
      // ------------------------------------------------------------
      private static JToken SerializeScene(Scene scene)
      {
         int handle = scene.handle;

         return new JObject
         {
            ["name"]   = scene.name,
            ["path"]   = string.IsNullOrEmpty(scene.path) ? null : scene.path,
            ["handle"] = handle,
            ["active"] = scene == SceneManager.GetActiveScene(),
            ["dirty"]  = scene.isDirty
         };
      }

   #endregion

   #region Collections

      // ------------------------------------------------------------
      /// <summary>
      /// Serializes an IDictionary as a JObject.
      /// </summary>
      // ------------------------------------------------------------
      private static JToken SerializeDictionary(IDictionary dict)
      {
         var obj = new JObject();

         foreach (DictionaryEntry entry in dict)
         {
            obj[entry.Key.ToString()] = Serialize(entry.Value);
         }

         return obj;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Serializes an IEnumerable as a JArray.
      /// </summary>
      // ------------------------------------------------------------
      private static JToken SerializeEnumerable(IEnumerable enumerable)
      {
         var arr = new JArray();

         foreach (var item in enumerable)
         {
            arr.Add(Serialize(item));
         }

         return arr;
      }

   #endregion

   #region Serialization

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Serializes via JsonUtility.
      /// <br/> Falls back to SerializeGeneric if result is empty.
      /// </summary>
      // ----------------------------------------------------------------------
      private static JToken SerializeJsonUtility(object value)
      {
         try
         {
            string json = JsonUtility.ToJson(value);

            if (json != null && json != "{}")
            {
               return JToken.Parse(json);
            }
         }
         catch {}

         return SerializeGeneric(value);
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Serializes via Newtonsoft with CLIJsonConverter.
      /// <br/> Nested Unity/[Serializable] types are routed back
      /// <br/> through Serialize() by the converter.
      /// </summary>
      // ----------------------------------------------------------------------
      private static JToken SerializeGeneric(object value)
      {
         try
         {
            return JToken.FromObject(value, JsonSerializer.Create(serializerSettings));
         }
         catch
         {
            return new JValue(value.ToString());
         }
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// Newtonsoft converter that routes Unity and [Serializable]
   /// types back through ResultSerializer.Serialize().
   /// </summary>
   // ============================================================
   internal class CLIJsonConverter : JsonConverter
   {

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Returns true for types that should use our serialization.
      /// </summary>
      // ------------------------------------------------------------
      public override bool CanConvert(Type type)
      {
         if (Type.GetTypeCode(type) != TypeCode.Object) return false;
         if (type.IsEnum) return false;
         if (type == typeof(Scene)) return true;
         if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return true;
         if (typeof(IDictionary).IsAssignableFrom(type)) return false;
         if (typeof(IEnumerable).IsAssignableFrom(type)) return false;
         if (type.IsSerializable) return true;
         if (type.Namespace?.StartsWith("Unity") == true) return true;

         return false;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Writes the value through ResultSerializer.Serialize().
      /// </summary>
      // ------------------------------------------------------------
      public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
      {
         ResultSerializer.Serialize(value).WriteTo(writer);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Not used — serialization only.
      /// </summary>
      // ------------------------------------------------------------
      public override bool CanRead => false;

      public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
      {
         throw new NotImplementedException();
      }

   #endregion

   }
}
