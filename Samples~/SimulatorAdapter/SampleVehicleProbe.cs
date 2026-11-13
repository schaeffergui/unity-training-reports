using TrainingReports;
using UnityEngine;

namespace TrainingReports.Samples
{
    public sealed class SampleVehicleProbe : MonoBehaviour, IVehicleProbe
    {
        [SerializeField] private bool engineOn;
        [SerializeField, Range(0f, 1f)] private float throttle;
        [SerializeField] private bool ignition;
        [SerializeField, Range(0f, 1f)] private float handbrake;
        [SerializeField] private bool canDrive = true;
        [SerializeField] private float speed;
        [SerializeField] private float rpm;

        public bool EngineOn => engineOn;
        public float Throttle => throttle;
        public bool Ignition => ignition;
        public bool Handbrake => handbrake > 0.5f;
        public bool CanDrive => canDrive;
        public float Speed => speed;
        public float Rpm => rpm;
    }
}
