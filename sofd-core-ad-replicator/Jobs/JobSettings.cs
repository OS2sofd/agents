
using System;

namespace sofd_core_ad_replicator.Jobs
{
    public class JobSettings
    {
        private string _cronValue = null;

        public string FullSyncCron
        {
            get
            {
                if (_cronValue == null)
                {
                    _cronValue = GenerateCron();
                }

                return _cronValue;
            }
            set
            {
                _cronValue = value;
            }
        }

        // run twice per day - once between 04:30-05:00 and once between 16:30-17:00
        private string GenerateCron()
        {
            Random rnd = new Random();

            long second = rnd.NextInt64(60);
            long minute = rnd.NextInt64(30) + 30;

            return second + " " + minute + " 4,16 * * ? *";
        }
    }
}
