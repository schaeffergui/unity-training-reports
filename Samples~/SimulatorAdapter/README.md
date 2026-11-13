# Simulator adapter sample

This sample shows how a Unity host can provide lesson context, a vehicle probe, a theme, operation steps, metrics, and a report presenter without depending on simulator-specific types.

## Setup

1. Import this sample into a Unity 6 project.
2. Add `SampleTrainingAdapter` and `SampleVehicleProbe` to scene objects.
3. Assign the probe component to the adapter's Vehicle Probe field.
4. Assign the adapter component to `TrainingManager` and set the task panel references.
5. Configure a `TrainingTaskUI` with one or more task rows, then call `StartTrainingFromContext`.

The sample runner is illustrative. Replace its simple engine-ready checks with the host's operation-specific lesson logic and implement the metrics/report interfaces with the host's data sources.
