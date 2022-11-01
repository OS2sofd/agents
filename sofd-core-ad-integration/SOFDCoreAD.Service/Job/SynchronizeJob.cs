using Quartz;
using Serilog;
using SOFDCoreAD.Service.ActiveDirectory;
using SOFDCoreAD.Service.Backend;
using SOFDCoreAD.Service.Photo;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SOFDCoreAD.Service.Job
{
    [DisallowConcurrentExecution]
    public class SynchronizeJob : IJob
    {
        public ILogger Logger { get; set; }
        public ActiveDirectoryService ActiveDirectoryService { get; set; }
        public BackendService BackendService { get; set; }
        public PhotoHashRepository PhotoHashRepository { get; set; }

        private byte[] directorySynchronizationCookie;
        private static bool shouldPerformFullSync = true;

        public static void ResetDirSync()
        {
            shouldPerformFullSync = true;
        }

        public Task Execute(IJobExecutionContext context)
        {
            if (shouldPerformFullSync)
            {
                shouldPerformFullSync = false; // in case of failure below, we do not want to go into an infinite full sync loop

                Logger.Information("Performing full sync");
                try
                {
                    var users = ActiveDirectoryService.GetFullSyncUsers(out directorySynchronizationCookie);
                    HandlePhotoStart(ref users);
                    BackendService.FullSync(users);
                    HandlePhotoEnd();
                }
                catch (System.Exception e)
                {
                    Logger.Error(e, "Exception caught in SynchronizeJob (full)");
                }

                Logger.Information("Full sync complete");
            }
            else {
                if (directorySynchronizationCookie == null || directorySynchronizationCookie.Length == 0)
                {
                    Log.Warning("No dirsync cookie, aborting!");
                }
                else
                {
                    try
                    {
                        var users = ActiveDirectoryService.GetDeltaSyncUsers(ref directorySynchronizationCookie);
                        HandlePhotoStart(ref users);
                        BackendService.DeltaSync(users);
                        HandlePhotoEnd();
                    }
                    catch (System.Exception e)
                    {
                        Logger.Error(e, "Exception caught in SynchronizeJob (delta)");
                    }
                }
            }

            return Task.CompletedTask;
        }

        private void HandlePhotoStart(ref IEnumerable<Model.ADUser> users)
        {
            if (PhotoHashRepository.PhotosEnabled)
            {
                PhotoHashRepository.Load();
                foreach (var user in users)
                {
                    if (user.Deleted)
                    {
                        continue;
                    }

                    if (PhotoHashRepository.InsertPhoto(user))
                    {
                        // send empty byte array to force delete in SOFD
                        user.Photo = user.Photo ?? new byte[0];
                    }
                    else
                    {
                        // null Photo is excluded from json and SOFD should not update in this case
                        user.Photo = null;
                    }
                }
            }
        }

        private void HandlePhotoEnd()
        {
            if (PhotoHashRepository.PhotosEnabled)
            {
                PhotoHashRepository.Save();
            }
        }

    }
}