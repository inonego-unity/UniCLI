/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SceneHandleUtility.cs
수정일 : 2026-07-25

# 설명
Unity 버전에 맞춰 SceneHandle의 원시 식별 값을 반환한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine.SceneManagement;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Unity 버전별 SceneHandle API 차이를 캡슐화한다.
   /// </summary>
   // ============================================================
   internal static class SceneHandleUtility
   {
      // ------------------------------------------------------------
      /// <summary>
      /// SceneHandle의 원시 식별 값을 부호 없는 정수로 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      public static ulong GetRawData(SceneHandle handle)
      {
      #if UNITY_6000_7_OR_NEWER
         return handle.GetRawData();
      #else
         return unchecked((ulong)(int)handle);
      #endif
      }
   }
}
