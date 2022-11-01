using SOFD_Core.Model.Enums;

namespace SOFD_Core.Model
{
    public class Change
    {
        public string uuid { get; set; }
        public ChangeType changeType { get; set; }
    }
}