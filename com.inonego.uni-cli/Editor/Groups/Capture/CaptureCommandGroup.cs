using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Rendering;

using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Attribute;
   using Core;

   // ============================================================
   /// <summary>
   /// Screen capture and recording commands.
   /// </summary>
   // ============================================================
   [CLIGroup("capture", "Screen capture")]
   public class CaptureCommandGroup
   {

   #region Commands

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Captures a screenshot of game view, scene view,
      /// <br/> or a specific editor window by instance ID.
      /// </summary>
      // ----------------------------------------------------------------------
      [CLICommand("", "Capture game/scene/window")]
      public static async Awaitable<object> Capture(CommandArgs args)
      {
         string target = args.Arg(0);

         if (string.IsNullOrEmpty(target))
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Target required: game, scene, or window <id>");
         }

         string path  = args.Option("path");
         float  scale = 1f;
         string scaleStr = args.Option("scale");

         if (scaleStr != null)
         {
            float.TryParse(scaleStr, out scale);
         }

         if (target == "game")
         {
            return await CaptureGameView(path, scale);
         }
         else if (target == "scene")
         {
            return CaptureSceneView(path, scale);
         }
         else if (target == "window")
         {
            int id = args.ArgInt(1);
            return CaptureWindow(id, path, scale);
         }

         throw new CLIException(ErrorCode.INVALID_ARGS, "Target must be: game, scene, or window <id>");
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Captures the Game view.
      /// </summary>
      // ------------------------------------------------------------
      private static async Awaitable<object> CaptureGameView(string path, float scale)
      {
         await Awaitable.MainThreadAsync();

         if (string.IsNullOrEmpty(path))
         {
            path = $"Screenshots/capture_game_{DateTime.Now:yyyyMMdd_HHmmss}.png";
         }

         int superSize = Mathf.Max(1, Mathf.RoundToInt(scale));

         var tcs = new TaskCompletionSource<Texture2D>();

         void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
         {
            if (cam != Camera.main)
            {
               return;
            }

            RenderPipelineManager.endCameraRendering -= OnEndCamera;

            var tex = ScreenCapture.CaptureScreenshotAsTexture(superSize);

            tcs.TrySetResult(tex);
         }

         RenderPipelineManager.endCameraRendering += OnEndCamera;

         // Force Game View to repaint
         var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
         var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);

         if (gameView != null)
         {
            gameView.Repaint();
         }

         var captured = await tcs.Task;

         if (captured == null)
         {
            throw new CLIException(ErrorCode.INTERNAL_ERROR, "Failed to capture game view.");
         }

         Directory.CreateDirectory(Path.GetDirectoryName(path));
         File.WriteAllBytes(path, captured.EncodeToPNG());

         int width  = captured.width;
         int height = captured.height;

         UnityEngine.Object.DestroyImmediate(captured);

         return new JObject
         {
            ["path"]   = path,
            ["width"]  = width,
            ["height"] = height
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Captures the Scene view.
      /// </summary>
      // ------------------------------------------------------------
      private static object CaptureSceneView(string path, float scale)
      {
         var sceneView = SceneView.lastActiveSceneView;

         if (sceneView == null)
         {
            throw new CLIException(ErrorCode.INTERNAL_ERROR, "No active Scene view.");
         }

         int width  = (int)(sceneView.position.width * scale);
         int height = (int)(sceneView.position.height * scale);

         var cam = sceneView.camera;
         var rt  = new RenderTexture(width, height, 24);
         var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

         cam.targetTexture = rt;
         cam.Render();

         RenderTexture.active = rt;
         tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
         tex.Apply();

         cam.targetTexture = null;
         RenderTexture.active = null;

         if (string.IsNullOrEmpty(path))
         {
            path = $"Screenshots/capture_scene_{DateTime.Now:yyyyMMdd_HHmmss}.png";
         }

         Directory.CreateDirectory(Path.GetDirectoryName(path));
         File.WriteAllBytes(path, tex.EncodeToPNG());

         UnityEngine.Object.DestroyImmediate(rt);
         UnityEngine.Object.DestroyImmediate(tex);

         return new JObject
         {
            ["path"]   = path,
            ["width"]  = width,
            ["height"] = height
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Captures an editor window by instance ID.
      /// </summary>
      // ------------------------------------------------------------
      private static object CaptureWindow(int id, string path, float scale)
      {
         var window = EditorUtility.EntityIdToObject(id) as EditorWindow;

         if (window == null)
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, $"Window {id} not found.");
         }

         // Try GUIView.GrabPixels first, fallback to ReadScreenPixel
         Texture2D tex = TryCaptureViaGrabPixels(window);

         if (tex == null)
         {
            window.Focus();

            Rect  pos = window.position;
            float dpi = EditorGUIUtility.pixelsPerPoint;
            int   w   = (int)(pos.width * dpi);
            int   h   = (int)(pos.height * dpi);

            Color[] pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel
            (
               new Vector2(pos.x * dpi, pos.y * dpi), w, h
            );

            tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.SetPixels(pixels);
            tex.Apply();
         }

         int width  = tex.width;
         int height = tex.height;

         if (string.IsNullOrEmpty(path))
         {
            path = $"Screenshots/capture_window_{DateTime.Now:yyyyMMdd_HHmmss}.png";
         }

         try
         {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());

            return new JObject
            {
               ["path"]   = path,
               ["width"]  = width,
               ["height"] = height
            };
         }
         finally
         {
            UnityEngine.Object.DestroyImmediate(tex);
         }
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Captures a window via GUIView.GrabPixels reflection.
      /// <br/> Works even if the window is behind other windows.
      /// <br/> Returns null if reflection fails.
      /// </summary>
      // ----------------------------------------------------------------------
      private static Texture2D TryCaptureViaGrabPixels(EditorWindow window)
      {
         try
         {
            // Get the GUIView parent
            var parentField = typeof(EditorWindow).GetField
            (
               "m_Parent",
               System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );

            object guiView = parentField?.GetValue(window);

            if (guiView == null)
            {
               return null;
            }

            // Repaint to ensure fresh content
            var repaintMethod = guiView.GetType().GetMethod
            (
               "RepaintImmediately",
               System.Reflection.BindingFlags.Instance |
               System.Reflection.BindingFlags.Public |
               System.Reflection.BindingFlags.NonPublic
            );

            repaintMethod?.Invoke(guiView, null);

            // Get GUIView type and GrabPixels method
            Type guiViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GUIView");

            var grabMethod = guiViewType?.GetMethod
            (
               "GrabPixels",
               System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );

            if (grabMethod == null)
            {
               return null;
            }

            // Get window dimensions
            var screenPosProp = guiViewType.GetProperty
            (
               "screenPosition",
               System.Reflection.BindingFlags.Instance |
               System.Reflection.BindingFlags.Public |
               System.Reflection.BindingFlags.NonPublic
            );

            Rect screenPos = (Rect)screenPosProp.GetValue(guiView);
            float dpi = EditorGUIUtility.pixelsPerPoint;
            int   w   = (int)(screenPos.width * dpi);
            int   h   = (int)(screenPos.height * dpi);

            // Grab pixels into RenderTexture
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24);

            grabMethod.Invoke(guiView, new object[] { rt, new Rect(0, 0, w, h) });

            // GPU flip vertically (GrabPixels returns top-down)
            RenderTexture flippedRT = RenderTexture.GetTemporary(w, h, 0);
            Graphics.Blit(rt, flippedRT, new Vector2(1, -1), new Vector2(0, 1));

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = flippedRT;

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            RenderTexture.ReleaseTemporary(flippedRT);

            return tex;
         }
         catch
         {
            return null;
         }
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// Screen recording commands.
   /// </summary>
   // ============================================================
   [CLIGroup("record", "Screen recording")]
   public class RecordCommandGroup
   {

   #region Fields

      private static RecorderController activeController = null;
      private static string activeRecordingPath = null;
      private static float  activeRecordingDuration = 0f;
      private static double activeRecordingStartTime = 0;

   #endregion

   #region Commands

      // ----------------------------------------------------------------------
      /// <summary>
      /// Starts screen recording using Unity Recorder.
      /// </summary>
      // ----------------------------------------------------------------------
      [CLICommand("start", "Start recording")]
      public static object Start(CommandArgs args)
      {
         if (activeController != null && activeController.IsRecording())
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "Recording already in progress.");
         }

         string path   = args.Option("path", $"Recordings/recording_{DateTime.Now:yyyyMMdd_HHmmss}");
         int    fps    = args.OptionInt("fps", 30);
         float  duration = 0f;
         string durStr = args.Option("duration");

         if (durStr != null)
         {
            float.TryParse(durStr, out duration);
         }

         // Create settings
         var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
         controllerSettings.SetRecordModeToManual();
         controllerSettings.FrameRate = fps;

         var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
         movieSettings.OutputFile = path;

         var encoderSettings = new CoreEncoderSettings
         {
            Codec           = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
         };

         movieSettings.EncoderSettings = encoderSettings;

         controllerSettings.AddRecorderSettings(movieSettings);

         // Start
         activeController = new RecorderController(controllerSettings);
         activeController.PrepareRecording();
         activeController.StartRecording();

         activeRecordingPath      = path;
         activeRecordingDuration  = duration;
         activeRecordingStartTime = EditorApplication.timeSinceStartup;

         // Auto-stop by duration
         if (duration > 0f)
         {
            EditorApplication.update += OnAutoStopUpdate;
         }

         return new JObject
         {
            ["path"] = path + ".mp4",
            ["fps"]  = fps
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Stops the active recording.
      /// </summary>
      // ------------------------------------------------------------
      [CLICommand("stop", "Stop recording")]
      public static object Stop(CommandArgs args)
      {
         if (activeController == null || !activeController.IsRecording())
         {
            throw new CLIException(ErrorCode.INVALID_ARGS, "No recording in progress.");
         }

         EditorApplication.update -= OnAutoStopUpdate;

         activeController.StopRecording();

         string path = activeRecordingPath;

         activeController         = null;
         activeRecordingPath      = null;
         activeRecordingDuration  = 0f;
         activeRecordingStartTime = 0;

         return new JObject
         {
            ["path"] = path + ".mp4"
         };
      }

   #endregion

   #region Helpers

      // ------------------------------------------------------------
      /// <summary>
      /// Auto-stops recording when duration is reached.
      /// </summary>
      // ------------------------------------------------------------
      private static void OnAutoStopUpdate()
      {
         if (activeController == null || !activeController.IsRecording())
         {
            EditorApplication.update -= OnAutoStopUpdate;
            return;
         }

         float elapsed = (float)(EditorApplication.timeSinceStartup - activeRecordingStartTime);

         if (elapsed >= activeRecordingDuration)
         {
            activeController.StopRecording();

            CLILog.Info($"Recording auto-stopped after {activeRecordingDuration}s: {activeRecordingPath}");

            EditorApplication.update -= OnAutoStopUpdate;
            activeController         = null;
            activeRecordingPath      = null;
            activeRecordingDuration  = 0f;
            activeRecordingStartTime = 0;
         }
      }

   #endregion

   }
}
