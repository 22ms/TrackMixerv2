namespace TrackMixerv2;

public static class SliderWheelRules
{
    public static double GetWheelStep(double smallChange, double tickFrequency)
    {
        if (smallChange > 0)
            return smallChange;

        if (tickFrequency > 0)
            return tickFrequency;

        return 1;
    }
}
