using System.Collections.Generic;

namespace SOFD_Core.Model
{
    public class DeltaSync
    {
        public int offset { get; set; }
        public List<Change> uuids { get; set; }
    }
}
