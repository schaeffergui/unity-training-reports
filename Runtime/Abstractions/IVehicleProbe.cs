namespace TrainingReports
{
    public interface IVehicleProbe
    {
        bool EngineOn { get; }
        float Throttle { get; }
        bool Ignition { get; }
        bool Handbrake { get; }
        bool CanDrive { get; }
        float Speed { get; }
        float Rpm { get; }
    }
}
