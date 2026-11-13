namespace TrainingReports
{
    public interface ITrainingTheme
    {
        TrainingColor GetColor(TrainingThemeColor role);
    }

    public enum TrainingThemeColor
    {
        Scrim,
        Window,
        Surface,
        Brand,
        PrimaryText,
        SecondaryText,
        NeutralControl,
        SelectedSurface,
        Warning,
        PrimaryButtonSurface,
        PrimaryButtonText,
        PrimaryButtonPressed,
        SecondaryButtonSurface,
        SecondaryButtonText,
        SecondaryButtonPressed
    }

    public readonly struct TrainingColor
    {
        public readonly byte Red;
        public readonly byte Green;
        public readonly byte Blue;
        public readonly byte Alpha;

        public TrainingColor(byte red, byte green, byte blue, byte alpha = 255)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }
    }

    public sealed class DefaultTrainingTheme : ITrainingTheme
    {
        public static readonly DefaultTrainingTheme Instance = new DefaultTrainingTheme();

        private DefaultTrainingTheme()
        {
        }

        public TrainingColor GetColor(TrainingThemeColor role)
        {
            switch (role)
            {
                case TrainingThemeColor.Scrim: return new TrainingColor(0, 0, 0, 180);
                case TrainingThemeColor.Window: return new TrainingColor(248, 249, 252);
                case TrainingThemeColor.Surface: return new TrainingColor(255, 255, 255);
                case TrainingThemeColor.Brand: return new TrainingColor(47, 69, 91);
                case TrainingThemeColor.PrimaryText: return new TrainingColor(35, 41, 48);
                case TrainingThemeColor.SecondaryText: return new TrainingColor(94, 103, 112);
                case TrainingThemeColor.NeutralControl: return new TrainingColor(232, 236, 240);
                case TrainingThemeColor.SelectedSurface: return new TrainingColor(215, 225, 235);
                case TrainingThemeColor.Warning: return new TrainingColor(166, 54, 48);
                case TrainingThemeColor.PrimaryButtonSurface: return new TrainingColor(47, 69, 91);
                case TrainingThemeColor.PrimaryButtonText: return new TrainingColor(255, 255, 255);
                case TrainingThemeColor.PrimaryButtonPressed: return new TrainingColor(31, 47, 64);
                case TrainingThemeColor.SecondaryButtonSurface: return new TrainingColor(241, 243, 246);
                case TrainingThemeColor.SecondaryButtonText: return new TrainingColor(47, 69, 91);
                case TrainingThemeColor.SecondaryButtonPressed: return new TrainingColor(215, 225, 235);
                default: return new TrainingColor(255, 0, 255);
            }
        }
    }
}
