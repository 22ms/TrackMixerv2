namespace TrackMixerv2;

public static class SliderWheelRules
{
    public static double GetWheelStep(double smallChange, double tickFrequency, int speedMultiplier = 1)
    {
        double baseStep = GetBaseWheelStep(smallChange, tickFrequency);
        return baseStep * Math.Max(1, speedMultiplier);
    }

    public static double GetBaseWheelStep(double smallChange, double tickFrequency)
    {
        if (smallChange > 0)
            return smallChange;

        if (tickFrequency > 0)
            return tickFrequency;

        return 1;
    }
}
