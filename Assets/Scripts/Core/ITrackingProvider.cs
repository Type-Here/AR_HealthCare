using ARHealthCare.DataClasses;

namespace ARHealthCare.Core
{
    public interface ITrackingProvider
    {
        PatientTrackingData GetPose();
    }
}