using System;

namespace ARHealthCare.Network
{
    [Serializable]
    public class PatientRecord
    {
        public string id;
        public string display_name;
        public int age;
        public string diagnosis;
        public string specialty;
        public string planned_procedure;
        public string procedure_date;
        public string current_treatment;
        public string[] history;
        public string notes;
        public string marker_bone;
    }

    [Serializable]
    public class FaceRecognitionRequest
    {
        public string image_b64;
    }

    [Serializable]
    public class FaceRecognitionResponse
    {
        public string patient_id;
        public float confidence;
    }

    [Serializable]
    public class ChecklistResponse
    {
        public string specialty;
        public string[] items;
    }

    [Serializable]
    public class SuggestRequest
    {
        public PatientRecord patient;
        public string specialty;
        public string mode;
        public string context;
    }

    [Serializable]
    public class SuggestResponse
    {
        public string suggestion;
    }
}
