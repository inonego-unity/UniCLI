using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Detects native modal dialogs via Win32 API.
   /// Runs entirely on background threads (no main thread needed).
   /// </summary>
   // ============================================================
   public static class ModalWatcher
   {

   #region Win32

      [DllImport("user32.dll", SetLastError = true)]
      private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

      [DllImport("user32.dll", SetLastError = true)]
      private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

      [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
      private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

      [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
      private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

      [DllImport("user32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

      [DllImport("user32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      private static extern bool EnumWindows(EnumChildProc lpEnumFunc, IntPtr lParam);

      [DllImport("user32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

      [DllImport("user32.dll")]
      private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

      [DllImport("user32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      private static extern bool IsWindowVisible(IntPtr hWnd);

      private const uint   BM_CLICK       = 0x00F5;
      private const string DIALOG_CLASS   = "#32770";
      private const string PROGRESS_CLASS = "msctls_progress32";

      private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

   #endregion

   #region Internal Data

      // ------------------------------------------------------------
      /// <summary>
      /// Represents a detected modal dialog.
      /// </summary>
      // ------------------------------------------------------------
      public class ModalInfo
      {
         public IntPtr   Handle;
         public string   Title;
         public string[] Buttons;
      }

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Detects a modal dialog in the current process.
      /// Safe to call from any thread.
      /// </summary>
      // ------------------------------------------------------------
      public static ModalInfo Detect()
      {
         int pid = System.Diagnostics.Process.GetCurrentProcess().Id;

         ModalInfo found = null;

         EnumChildProc threadCallback = (hWnd, _) =>
         {
            if (!IsWindowVisible(hWnd))
            {
               return true;
            }

            var className = new StringBuilder(256);
            GetClassName(hWnd, className, 256);

            if (className.ToString() != DIALOG_CLASS)
            {
               return true;
            }

            int windowPid;
            GetWindowThreadProcessId(hWnd, out windowPid);

            if (windowPid != pid)
            {
               return true;
            }

            var title = new StringBuilder(256);
            GetWindowText(hWnd, title, 256);

            var  buttons        = new List<string>();
            bool hasProgressBar = false;

            EnumChildWindows(hWnd, (childHwnd, __) =>
            {
               var childClass = new StringBuilder(256);
               GetClassName(childHwnd, childClass, 256);

               string cls = childClass.ToString();

               if (cls == "Button")
               {
                  var buttonText = new StringBuilder(256);
                  GetWindowText(childHwnd, buttonText, 256);

                  string text = buttonText.ToString();

                  if (!string.IsNullOrEmpty(text))
                  {
                     buttons.Add(text);
                  }
               }
               else if (cls == PROGRESS_CLASS)
               {
                  hasProgressBar = true;
               }

               return true;
            }, IntPtr.Zero);

            // Skip progress dialogs and transient system dialogs
            if (hasProgressBar || buttons.Count == 0)
            {
               return true;
            }

            found = new ModalInfo
            {
               Handle  = hWnd,
               Title   = title.ToString(),
               Buttons = buttons.ToArray()
            };

            return false;
         };

         EnumWindows(threadCallback, IntPtr.Zero);

         return found;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Clicks a button on a modal dialog by button text.
      /// Safe to call from any thread.
      /// </summary>
      // ------------------------------------------------------------
      public static bool ClickButton(string buttonText)
      {
         var modal = Detect();

         if (modal == null)
         {
            return false;
         }

         IntPtr target = IntPtr.Zero;

         EnumChildWindows(modal.Handle, (childHwnd, _) =>
         {
            var childClass = new StringBuilder(256);
            GetClassName(childHwnd, childClass, 256);

            if (childClass.ToString() != "Button")
            {
               return true;
            }

            var text = new StringBuilder(256);
            GetWindowText(childHwnd, text, 256);

            if (text.ToString().Equals(buttonText, StringComparison.OrdinalIgnoreCase))
            {
               target = childHwnd;
               return false;
            }

            return true;
         }, IntPtr.Zero);

         if (target == IntPtr.Zero)
         {
            return false;
         }

         SendMessage(target, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
         return true;
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Watches for a modal dialog in the background.
      /// <br/> Returns ModalInfo when detected, or null if cancelled.
      /// </summary>
      // ----------------------------------------------------------------------
      public static async Task<ModalInfo> WatchAsync
      (
         CancellationToken token,
         int pollIntervalMs = 300
      )
      {
         while (!token.IsCancellationRequested)
         {
            var modal = Detect();

            if (modal != null)
            {
               return modal;
            }

            try
            {
               await Task.Delay(pollIntervalMs, token);
            }
            catch (TaskCanceledException)
            {
               break;
            }
         }

         return null;
      }

   #endregion

   }
}
