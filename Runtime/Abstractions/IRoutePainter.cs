namespace TrainingReports
{
    public interface IRoutePainter
    {
        bool TryGetRouteImage(out byte[] pngBytes, out int width, out int height);
    }
}
