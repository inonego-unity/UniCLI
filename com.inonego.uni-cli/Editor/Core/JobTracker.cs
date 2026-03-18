using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.UniCLI.Core
{
   // ============================================================
   /// <summary>
   /// Manages async job state for long-running operations
   /// such as test runs and builds.
   /// </summary>
   // ============================================================
   public static class JobTracker
   {

   #region Internal data

      // ------------------------------------------------------------
      /// <summary>
      /// Represents the status of an async job.
      /// </summary>
      // ------------------------------------------------------------
      public enum JobStatus { Running, Completed, Failed }

      // ------------------------------------------------------------
      /// <summary>
      /// Holds state and result for a single job.
      /// </summary>
      // ------------------------------------------------------------
      public class JobData
      {
         public string    Id;
         public JobStatus Status;
         public object    Result;
         public DateTime  CreatedAt;
      }

   #endregion

   #region Fields

      private static readonly Dictionary<string, JobData> jobs = new Dictionary<string, JobData>();

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Creates a new running job and returns its ID.
      /// </summary>
      // ------------------------------------------------------------
      public static string Create()
      {
         string id = Guid.NewGuid().ToString();

         jobs[id] = new JobData
         {
            Id        = id,
            Status    = JobStatus.Running,
            CreatedAt = DateTime.Now
         };

         return id;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Marks a job as completed with a result payload.
      /// </summary>
      // ------------------------------------------------------------
      public static void Complete(string id, object result)
      {
         if (!jobs.TryGetValue(id, out JobData job))
         {
            return;
         }

         job.Status = JobStatus.Completed;
         job.Result = result;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Marks a job as failed with an error message.
      /// </summary>
      // ------------------------------------------------------------
      public static void Fail(string id, string error)
      {
         if (!jobs.TryGetValue(id, out JobData job))
         {
            return;
         }

         job.Status = JobStatus.Failed;
         job.Result = new { error = error };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Returns job data by ID, or null if not found.
      /// </summary>
      // ------------------------------------------------------------
      public static JobData Get(string id)
      {
         jobs.TryGetValue(id, out JobData job);
         return job;
      }

   #endregion

   }
}
