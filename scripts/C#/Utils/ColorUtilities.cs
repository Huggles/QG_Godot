using Godot;
using System;

public static class ColorUtilities
{
    public static Color Random(this Color color)
    {
        Random random = new Random();
        return new Color(random.Next(255) / 255f, random.Next(255) / 255f, random.Next(255) / 255f);
    }
    public static Color Random(this Color color, float alpha)
    {
        Random random = new Random();
        return new Color(random.Next(255) / 255f, random.Next(255) / 255f, random.Next(255) / 255f, alpha);
    }
}
