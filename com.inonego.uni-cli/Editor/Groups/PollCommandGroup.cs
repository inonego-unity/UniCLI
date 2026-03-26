using System;

using InoCLI;

using Newtonsoft.Json.Linq;

namespace inonego.UniCLI.Group
{
   using Core;

   // ============================================================
   /// <summary>
   /// Poll async job status.
   /// </summary>
   // ============================================================
   public static class PollCommandGroup
   {

   #region Commands

      [CLICommand("poll", description = "Poll job status")]
      public static object Poll(CommandArgs args)
      {
         string jobId = args[0];

         if (string.IsNullOrEmpty(jobId))
         {
            throw new CLIException(Constants.Error.InvalidArgs, "Job ID required.");
         }

         var job = JobTracker.Get(jobId);

         if (job == null)
         {
            throw new CLIException(Constants.Error.InvalidArgs, $"Job not found: {jobId}");
         }

         var result = new JObject
         {
            ["status"] = job.Status.ToString().ToLower()
         };

         if (job.Result != null)
         {
            result["result"] = JToken.FromObject(job.Result);
         }

         return result;
      }

   #endregion

   }
}
