/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : EntityIdUtility.cs
수정일 : 2026-07-25

# 설명
Unity 버전에 맞춰 객체 EntityId를 직렬화하고 객체 참조로 복원한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using UnityEditor;

using Newtonsoft.Json.Linq;

using Object = UnityEngine.Object;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Unity 버전별 객체 식별자 API 차이를 캡슐화한다.
   /// </summary>
   // ============================================================
   internal static class EntityIdUtility
   {
      // ------------------------------------------------------------
      /// <summary>
      /// 객체 식별자를 JSON 숫자 값으로 직렬화한다.
      /// </summary>
      // ------------------------------------------------------------
      public static JValue Serialize(Object obj)
      {
         if (obj == null)
         {
            return JValue.CreateNull();
         }

      #if UNITY_6000_7_OR_NEWER
         return new JValue(EntityId.ToULong(obj.GetEntityId()));
      #else
         return new JValue(obj.GetInstanceID());
      #endif
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 직렬화된 객체 식별자를 Unity 객체로 복원한다.
      /// </summary>
      // ------------------------------------------------------------
      public static Object Resolve(string rawData)
      {
         if (string.IsNullOrEmpty(rawData))
         {
            return null;
         }

      #if UNITY_6000_7_OR_NEWER
         return ulong.TryParse(rawData, out ulong value)
            ? EditorUtility.EntityIdToObject(EntityId.FromULong(value))
            : null;
      #else
         return int.TryParse(rawData, out int value)
            ? EditorUtility.EntityIdToObject(value)
            : null;
      #endif
      }
   }
}
