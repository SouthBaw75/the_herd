using System.Globalization;
using System.Text;
using CattleRanch.Sim;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Web;

/// <summary>
/// Hand-drawn SVG pasture backdrop (sky, weather, field, fence, pond, barn),
/// emitted as raw markup for MarkupString injection inside the Blazor-owned
/// scene svg. Companion to <see cref="CowArt"/> — same cozy children's-book
/// style, evening light. Pure presentation: reads sim values, draws shapes.
/// No sim access beyond the passed-in readings, and NEVER state.Rng — all
/// scatter placement uses a private fixed-seed LCG (pure function of consts).
/// Scene coordinate space: viewBox 0 0 900 620, horizon at y=230.
/// </summary>
internal static class SceneArt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public const double Width = 900;
    public const double Height = 620;
    public const double HorizonY = 230;

    private static string N(double v) => v.ToString("F1", Inv);

    // ------------------------------------------------------------------
    // Sky band: season/weather palette, sun, clouds, animated weather
    // ------------------------------------------------------------------

    public static string Sky(Season season, WeatherCondition weather)
    {
        (string top, string mid, string bot) = SkyColors(season, weather);
        var sb = new StringBuilder(4000);

        sb.Append("<defs><linearGradient id=\"skyG\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">")
          .Append("<stop offset=\"0\" stop-color=\"").Append(top).Append("\"/>")
          .Append("<stop offset=\"0.62\" stop-color=\"").Append(mid).Append("\"/>")
          .Append("<stop offset=\"1\" stop-color=\"").Append(bot).Append("\"/>")
          .Append("</linearGradient></defs>");

        sb.Append("<rect x=\"0\" y=\"0\" width=\"900\" height=\"").Append(N(HorizonY + 6))
          .Append("\" fill=\"url(#skyG)\"/>");

        bool overcast = weather == WeatherCondition.WetSpring || weather == WeatherCondition.Blizzard;
        bool hot = weather == WeatherCondition.Drought || weather == WeatherCondition.HeatWave;

        if (!overcast)
        {
            string sunCore = season == Season.Winter ? "#eef2f0" : hot ? "#ffc46a" : "#ffdf9e";
            string sunGlow = season == Season.Winter ? "#c9d4d6" : hot ? "#ff9d4d" : "#ffcf7d";
            double r = hot ? 40 : 33;
            sb.Append("<circle cx=\"720\" cy=\"88\" r=\"").Append(N(r * 1.9))
              .Append("\" fill=\"").Append(sunGlow).Append("\" opacity=\"0.22\"/>");
            sb.Append("<circle cx=\"720\" cy=\"88\" r=\"").Append(N(r * 1.35))
              .Append("\" fill=\"").Append(sunGlow).Append("\" opacity=\"0.3\"/>");
            sb.Append("<circle cx=\"720\" cy=\"88\" r=\"").Append(N(r))
              .Append("\" fill=\"").Append(sunCore).Append("\"/>");
        }

        AppendClouds(sb, season, weather, overcast);

        if (hot) { AppendShimmer(sb); }

        return sb.ToString();
    }

    /// <summary>
    /// Falling weather particles, rendered ABOVE the field and herd so rain
    /// and snow drift across the whole scene, not just the sky band.
    /// </summary>
    public static string WeatherOverlay(WeatherCondition weather)
    {
        if (weather != WeatherCondition.WetSpring && weather != WeatherCondition.Blizzard)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(2500);
        sb.Append("<g class=\"weather-overlay\">");
        if (weather == WeatherCondition.WetSpring) { AppendRain(sb); }
        else { AppendSnowfall(sb); }
        sb.Append("</g>");
        return sb.ToString();
    }

    private static (string, string, string) SkyColors(Season s, WeatherCondition w) => w switch
    {
        WeatherCondition.WetSpring => ("#2b3947", "#49596a", "#75838f"),
        WeatherCondition.Blizzard => ("#333e4d", "#5a6878", "#8f9dac"),
        WeatherCondition.Drought => ("#54412c", "#96703f", "#dda05a"),
        WeatherCondition.HeatWave => ("#5c3a2a", "#a05d38", "#f0a05a"),
        _ => s switch
        {
            Season.Spring => ("#32496e", "#7a6a8c", "#e8a37e"),
            Season.Summer => ("#2f5d84", "#8a7a80", "#f0b06a"),
            Season.Fall => ("#463a54", "#8f5a52", "#e08a5a"),
            _ => ("#2c3a4a", "#5c6d80", "#aab8c4"),
        },
    };

    private static void AppendClouds(StringBuilder sb, Season season, WeatherCondition w, bool overcast)
    {
        string fill = overcast
            ? "#66727f"
            : season == Season.Winter ? "#c3ccd4" : "#e8c8a8";
        double op = overcast ? 0.9 : 0.55;

        // Two drifting cloud clusters, one slow, one slower.
        sb.Append("<g class=\"cloud cloud-a\" opacity=\"").Append(N(op)).Append("\">");
        CloudPuff(sb, 150, 70, 1.0, fill);
        sb.Append("</g>");
        sb.Append("<g class=\"cloud cloud-b\" opacity=\"").Append(N(op * 0.85)).Append("\">");
        CloudPuff(sb, 470, 120, 0.8, fill);
        sb.Append("</g>");
        if (overcast)
        {
            sb.Append("<g class=\"cloud cloud-a\" opacity=\"0.85\">");
            CloudPuff(sb, 700, 55, 1.2, fill);
            sb.Append("</g>");
        }
    }

    private static void CloudPuff(StringBuilder sb, double x, double y, double s, string fill)
    {
        sb.Append("<g transform=\"translate(").Append(N(x)).Append(' ').Append(N(y))
          .Append(") scale(").Append(N(s)).Append(")\" fill=\"").Append(fill).Append("\">")
          .Append("<ellipse cx=\"0\" cy=\"0\" rx=\"46\" ry=\"18\"/>")
          .Append("<ellipse cx=\"-28\" cy=\"7\" rx=\"30\" ry=\"13\"/>")
          .Append("<ellipse cx=\"30\" cy=\"6\" rx=\"32\" ry=\"14\"/>")
          .Append("<ellipse cx=\"6\" cy=\"-11\" rx=\"26\" ry=\"13\"/>")
          .Append("</g>");
    }

    private static void AppendRain(StringBuilder sb)
    {
        sb.Append("<g class=\"rain-layer\">");
        uint r = 77;
        for (int i = 0; i < 26; i++)
        {
            double x = 10 + i * 35 + Next(ref r) % 24;
            double delay = (Next(ref r) % 70) / 100.0;
            sb.Append("<line class=\"raindrop\" x1=\"").Append(N(x)).Append("\" y1=\"-40\" x2=\"")
              .Append(N(x - 10)).Append("\" y2=\"-16\" style=\"animation-delay:-").Append(N(delay))
              .Append("s\"/>");
        }

        sb.Append("</g>");
    }

    private static void AppendSnowfall(StringBuilder sb)
    {
        sb.Append("<g class=\"snow-layer\">");
        uint r = 913;
        for (int i = 0; i < 34; i++)
        {
            double x = 8 + i * 26 + Next(ref r) % 22;
            double size = 1.6 + (Next(ref r) % 20) / 10.0;
            double dur = 6.0 + (Next(ref r) % 50) / 10.0;
            double delay = (Next(ref r) % 90) / 10.0;
            sb.Append("<circle class=\"snowflake\" cx=\"").Append(N(x)).Append("\" cy=\"-14\" r=\"")
              .Append(N(size)).Append("\" style=\"animation-duration:").Append(N(dur))
              .Append("s;animation-delay:-").Append(N(delay)).Append("s\"/>");
        }

        sb.Append("</g>");
    }

    private static void AppendShimmer(StringBuilder sb)
    {
        // Wavy heat-shimmer rays rising off the horizon, plus pulsing sun rays.
        sb.Append("<g class=\"sunrays\">");
        for (int i = 0; i < 7; i++)
        {
            double ang = -140 + i * 28;
            double rad = ang * System.Math.PI / 180.0;
            double x1 = 720 + System.Math.Cos(rad) * 52;
            double y1 = 88 + System.Math.Sin(rad) * 52;
            double x2 = 720 + System.Math.Cos(rad) * 78;
            double y2 = 88 + System.Math.Sin(rad) * 78;
            sb.Append("<line x1=\"").Append(N(x1)).Append("\" y1=\"").Append(N(y1))
              .Append("\" x2=\"").Append(N(x2)).Append("\" y2=\"").Append(N(y2)).Append("\"/>");
        }

        sb.Append("</g><g class=\"shimmer-layer\">");
        uint r = 321;
        for (int i = 0; i < 9; i++)
        {
            double x = 50 + i * 100 + Next(ref r) % 40;
            double delay = (Next(ref r) % 30) / 10.0;
            sb.Append("<path class=\"shimmer\" d=\"M ").Append(N(x)).Append(" ").Append(N(HorizonY - 4))
              .Append(" q 5 -13 0 -26 q -5 -13 0 -26\" style=\"animation-delay:-")
              .Append(N(delay)).Append("s\"/>");
        }

        sb.Append("</g>");
    }

    // ------------------------------------------------------------------
    // The field: ground color = f(grass, soil, season), barn, fence,
    // pond, tufts/flowers when lush, dirt patches when depleted, snow.
    // ------------------------------------------------------------------

    public static string Field(double grassFrac, double soilHealth, Season season, WeatherCondition weather)
    {
        grassFrac = System.Math.Clamp(grassFrac, 0.0, 1.0);
        soilHealth = System.Math.Clamp(soilHealth, 0.0, 1.0);
        bool winter = season == Season.Winter;
        bool blizzard = weather == WeatherCondition.Blizzard;

        string ground = GroundColor(grassFrac, soilHealth, winter, blizzard);
        string groundFar = Mix(ground, "#e8c9a0", winter ? 0.10 : 0.22); // hazy light at the horizon
        string groundNear = Mix(ground, "#1a140c", 0.24);

        var sb = new StringBuilder(6000);

        sb.Append("<defs><linearGradient id=\"groundG\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">")
          .Append("<stop offset=\"0\" stop-color=\"").Append(groundFar).Append("\"/>")
          .Append("<stop offset=\"0.45\" stop-color=\"").Append(ground).Append("\"/>")
          .Append("<stop offset=\"1\" stop-color=\"").Append(groundNear).Append("\"/>")
          .Append("</linearGradient></defs>");

        // Distant hills behind the fence for depth.
        string hill = Mix(ground, winter ? "#d8dfe2" : "#4a3f2f", 0.35);
        sb.Append("<path d=\"M 0 ").Append(N(HorizonY + 2))
          .Append(" Q 160 ").Append(N(HorizonY - 26)).Append(" 340 ").Append(N(HorizonY))
          .Append(" T 660 ").Append(N(HorizonY - 6)).Append(" T 900 ").Append(N(HorizonY + 2))
          .Append(" V ").Append(N(HorizonY + 12)).Append(" H 0 Z\" fill=\"").Append(hill).Append("\"/>");

        // The pasture itself.
        sb.Append("<rect x=\"0\" y=\"").Append(N(HorizonY))
          .Append("\" width=\"900\" height=\"").Append(N(Height - HorizonY))
          .Append("\" fill=\"url(#groundG)\"/>");

        // Soft mottling so the ground doesn't read flat.
        uint r = 4242;
        string mottle = Mix(ground, "#ffffff", 0.07);
        for (int i = 0; i < 8; i++)
        {
            double x = Next(ref r) % 900;
            double y = HorizonY + 40 + Next(ref r) % 300;
            double rx = 60 + Next(ref r) % 90;
            sb.Append("<ellipse cx=\"").Append(N(x)).Append("\" cy=\"").Append(N(y))
              .Append("\" rx=\"").Append(N(rx)).Append("\" ry=\"").Append(N(rx * 0.22))
              .Append("\" fill=\"").Append(mottle).Append("\" opacity=\"0.5\"/>");
        }

        AppendBarn(sb, winter || blizzard);
        AppendFence(sb, winter || blizzard);
        AppendPond(sb, winter, blizzard);

        if (grassFrac < 0.30)
        {
            AppendDirtPatches(sb, grassFrac, ground);
        }

        if (grassFrac > 0.80 && !winter && !blizzard)
        {
            AppendTuftsAndFlowers(sb, ground, season);
        }

        if (winter || blizzard)
        {
            AppendSnowDust(sb, blizzard);
        }

        return sb.ToString();
    }

    private static string GroundColor(double g, double soil, bool winter, bool blizzard)
    {
        // Lush green -> yellowing -> bare dirt, darkened a touch by poor soil.
        string bare = "#8a6a48";
        string dry = "#a8944e";
        string lush = "#567f3e";
        string c = g >= 0.55
            ? Mix(dry, lush, (g - 0.55) / 0.45)
            : Mix(bare, dry, g / 0.55);
        c = Mix(c, "#3a2f20", 0.22 * (1.0 - soil));
        if (blizzard) { return Mix(c, "#dfe6e8", 0.78); }
        if (winter) { return Mix(c, "#c2ccc8", 0.42); }
        return c;
    }

    private static void AppendBarn(StringBuilder sb, bool snowy)
    {
        // Little gambrel barn on the rise, left of center, behind the fence.
        sb.Append("<g transform=\"translate(96 ").Append(N(HorizonY + 18)).Append(")\">");
        // Body
        sb.Append("<rect x=\"0\" y=\"-58\" width=\"118\" height=\"58\" rx=\"3\" fill=\"#6b3428\"/>");
        // Roof (gambrel silhouette)
        sb.Append("<path d=\"M -8 -56 L 12 -84 L 59 -96 L 106 -84 L 126 -56 Z\" fill=\"#42211a\"/>");
        if (snowy)
        {
            sb.Append("<path d=\"M -8 -56 L 12 -84 L 59 -96 L 106 -84 L 126 -56 L 118 -56 L 100 -80 L 59 -90 L 18 -80 L 0 -56 Z\" fill=\"#e9eef0\" opacity=\"0.9\"/>");
        }

        // Loft window + big door
        sb.Append("<circle cx=\"59\" cy=\"-70\" r=\"7\" fill=\"#f4d9a0\" opacity=\"0.9\"/>");
        sb.Append("<rect x=\"41\" y=\"-36\" width=\"36\" height=\"36\" rx=\"2\" fill=\"#3a1d15\"/>");
        sb.Append("<path d=\"M 41 -36 L 77 0 M 77 -36 L 41 0\" stroke=\"#8a4a36\" stroke-width=\"2.5\"/>");
        // Trim
        sb.Append("<rect x=\"0\" y=\"-58\" width=\"118\" height=\"58\" rx=\"3\" fill=\"none\" stroke=\"#42211a\" stroke-width=\"2\"/>");
        sb.Append("</g>");
    }

    private static void AppendFence(StringBuilder sb, bool snowy)
    {
        string post = "#4d3a26";
        string rail = "#5d4730";
        double y = HorizonY + 6;
        sb.Append("<g>");
        sb.Append("<line x1=\"0\" y1=\"").Append(N(y + 8)).Append("\" x2=\"900\" y2=\"").Append(N(y + 8))
          .Append("\" stroke=\"").Append(rail).Append("\" stroke-width=\"3\"/>");
        sb.Append("<line x1=\"0\" y1=\"").Append(N(y + 17)).Append("\" x2=\"900\" y2=\"").Append(N(y + 17))
          .Append("\" stroke=\"").Append(rail).Append("\" stroke-width=\"3\"/>");
        for (double x = 14; x < 900; x += 66)
        {
            sb.Append("<rect x=\"").Append(N(x)).Append("\" y=\"").Append(N(y))
              .Append("\" width=\"6\" height=\"26\" rx=\"2\" fill=\"").Append(post).Append("\"/>");
            if (snowy)
            {
                sb.Append("<rect x=\"").Append(N(x - 1)).Append("\" y=\"").Append(N(y - 2))
                  .Append("\" width=\"8\" height=\"4\" rx=\"2\" fill=\"#e9eef0\"/>");
            }
        }

        sb.Append("</g>");
    }

    private static void AppendPond(StringBuilder sb, bool winter, bool blizzard)
    {
        string water = winter || blizzard ? "#9fb6c4" : "#3e5d74";
        string glint = winter || blizzard ? "#c9dae4" : "#587a8e";
        sb.Append("<ellipse cx=\"745\" cy=\"498\" rx=\"118\" ry=\"46\" fill=\"")
          .Append(Mix(water, "#1a140c", 0.3)).Append("\"/>");
        sb.Append("<ellipse cx=\"745\" cy=\"494\" rx=\"110\" ry=\"41\" fill=\"").Append(water).Append("\"/>");
        sb.Append("<ellipse class=\"pond-glint\" cx=\"720\" cy=\"488\" rx=\"48\" ry=\"10\" fill=\"")
          .Append(glint).Append("\" opacity=\"0.75\"/>");
        sb.Append("<ellipse cx=\"782\" cy=\"506\" rx=\"26\" ry=\"5\" fill=\"").Append(glint).Append("\" opacity=\"0.5\"/>");
        // Reeds on the bank
        string reed = winter ? "#8a8a72" : "#5d7a3a";
        sb.Append("<g stroke=\"").Append(reed).Append("\" stroke-width=\"2.5\" stroke-linecap=\"round\" fill=\"none\">")
          .Append("<path d=\"M 632 486 q -3 -14 2 -24\"/><path d=\"M 641 490 q 1 -16 -3 -26\"/>")
          .Append("<path d=\"M 852 478 q 4 -13 0 -23\"/><path d=\"M 861 483 q -2 -15 3 -24\"/>")
          .Append("</g>");
    }

    private static void AppendDirtPatches(StringBuilder sb, double grassFrac, string ground)
    {
        string dirt = Mix("#6f5238", ground, 0.25);
        int count = 4 + (int)((0.30 - grassFrac) * 30);
        uint r = 555;
        for (int i = 0; i < count; i++)
        {
            double x = 40 + Next(ref r) % 820;
            double y = HorizonY + 60 + Next(ref r) % 300;
            if (InPond(x, y)) { continue; }
            double rx = 26 + Next(ref r) % 40;
            sb.Append("<ellipse cx=\"").Append(N(x)).Append("\" cy=\"").Append(N(y))
              .Append("\" rx=\"").Append(N(rx)).Append("\" ry=\"").Append(N(rx * 0.3))
              .Append("\" fill=\"").Append(dirt).Append("\" opacity=\"0.85\"/>");
        }
    }

    private static void AppendTuftsAndFlowers(StringBuilder sb, string ground, Season season)
    {
        string tuft = Mix(ground, "#8fce5a", 0.55);
        uint r = 808;
        sb.Append("<g class=\"tufts\" stroke=\"").Append(tuft)
          .Append("\" stroke-width=\"2.4\" stroke-linecap=\"round\" fill=\"none\">");
        for (int i = 0; i < 20; i++)
        {
            double x = 24 + Next(ref r) % 852;
            double y = HorizonY + 48 + Next(ref r) % 320;
            if (InPond(x, y)) { continue; }
            sb.Append("<path d=\"M ").Append(N(x)).Append(' ').Append(N(y))
              .Append(" q -3 -9 -5 -12 M ").Append(N(x)).Append(' ').Append(N(y))
              .Append(" q 0 -10 1 -14 M ").Append(N(x)).Append(' ').Append(N(y))
              .Append(" q 4 -8 6 -11\"/>");
        }

        sb.Append("</g>");

        if (season == Season.Spring || season == Season.Summer)
        {
            sb.Append("<g class=\"flowers\">");
            for (int i = 0; i < 12; i++)
            {
                double x = 40 + Next(ref r) % 820;
                double y = HorizonY + 60 + Next(ref r) % 300;
                if (InPond(x, y)) { continue; }
                string c = (Next(ref r) % 3) switch { 0 => "#e8c46a", 1 => "#d9788a", _ => "#e6e2cf" };
                sb.Append("<circle cx=\"").Append(N(x)).Append("\" cy=\"").Append(N(y))
                  .Append("\" r=\"2.6\" fill=\"").Append(c).Append("\"/>");
                sb.Append("<circle cx=\"").Append(N(x)).Append("\" cy=\"").Append(N(y))
                  .Append("\" r=\"1\" fill=\"#8a6a2a\"/>");
            }

            sb.Append("</g>");
        }
    }

    private static void AppendSnowDust(StringBuilder sb, bool blizzard)
    {
        uint r = 99;
        int count = blizzard ? 26 : 16;
        double op = blizzard ? 0.85 : 0.5;
        for (int i = 0; i < count; i++)
        {
            double x = 20 + Next(ref r) % 860;
            double y = HorizonY + 50 + Next(ref r) % 320;
            if (InPond(x, y)) { continue; }
            double rx = 30 + Next(ref r) % 60;
            sb.Append("<ellipse cx=\"").Append(N(x)).Append("\" cy=\"").Append(N(y))
              .Append("\" rx=\"").Append(N(rx)).Append("\" ry=\"").Append(N(rx * 0.24))
              .Append("\" fill=\"#e9eef0\" opacity=\"").Append(N(op)).Append("\"/>");
        }
    }

    private static bool InPond(double x, double y)
    {
        double dx = (x - 745) / 140.0;
        double dy = (y - 498) / 62.0;
        return dx * dx + dy * dy < 1.0;
    }

    // ------------------------------------------------------------------
    // Start-screen vignette: a tiny dusk ranch (its own 480x200 viewBox).
    // ------------------------------------------------------------------

    public static string Vignette()
    {
        var sb = new StringBuilder(3000);
        sb.Append("<defs><linearGradient id=\"vSky\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">")
          .Append("<stop offset=\"0\" stop-color=\"#32496e\"/><stop offset=\"0.7\" stop-color=\"#8a6a80\"/>")
          .Append("<stop offset=\"1\" stop-color=\"#e8a37e\"/></linearGradient></defs>");
        sb.Append("<rect x=\"0\" y=\"0\" width=\"480\" height=\"122\" fill=\"url(#vSky)\" rx=\"0\"/>");
        sb.Append("<circle cx=\"370\" cy=\"48\" r=\"34\" fill=\"#ffcf7d\" opacity=\"0.3\"/>");
        sb.Append("<circle cx=\"370\" cy=\"48\" r=\"20\" fill=\"#ffdf9e\"/>");
        CloudPuff(sb, 110, 38, 0.55, "#e8c8a8");
        sb.Append("<rect x=\"0\" y=\"118\" width=\"480\" height=\"82\" fill=\"#567f3e\"/>");
        sb.Append("<rect x=\"0\" y=\"170\" width=\"480\" height=\"30\" fill=\"#43642f\"/>");
        // Fence
        sb.Append("<line x1=\"0\" y1=\"130\" x2=\"480\" y2=\"130\" stroke=\"#5d4730\" stroke-width=\"2\"/>");
        for (double x = 8; x < 480; x += 46)
        {
            sb.Append("<rect x=\"").Append(N(x)).Append("\" y=\"122\" width=\"4\" height=\"16\" rx=\"1.5\" fill=\"#4d3a26\"/>");
        }

        // Barn
        sb.Append("<g transform=\"translate(48 128) scale(0.62)\">")
          .Append("<rect x=\"0\" y=\"-58\" width=\"118\" height=\"58\" rx=\"3\" fill=\"#6b3428\"/>")
          .Append("<path d=\"M -8 -56 L 12 -84 L 59 -96 L 106 -84 L 126 -56 Z\" fill=\"#42211a\"/>")
          .Append("<circle cx=\"59\" cy=\"-70\" r=\"7\" fill=\"#f4d9a0\" opacity=\"0.9\"/>")
          .Append("<rect x=\"41\" y=\"-36\" width=\"36\" height=\"36\" rx=\"2\" fill=\"#3a1d15\"/>")
          .Append("</g>");
        return sb.ToString();
    }

    /// <summary>Tiny fixed-seed LCG for decorative scatter — NOT the sim RNG.</summary>
    private static uint Next(ref uint s)
    {
        s = s * 1664525u + 1013904223u;
        return s >> 8;
    }

    private static string Mix(string hexA, string hexB, double t)
    {
        t = System.Math.Clamp(t, 0.0, 1.0);
        (int ra, int ga, int ba) = ParseHex(hexA);
        (int rb, int gb, int bb) = ParseHex(hexB);
        int rr = (int)System.Math.Round(ra + (rb - ra) * t);
        int gg = (int)System.Math.Round(ga + (gb - ga) * t);
        int bz = (int)System.Math.Round(ba + (bb - ba) * t);
        return $"#{rr:x2}{gg:x2}{bz:x2}";
    }

    private static (int, int, int) ParseHex(string hex) =>
        (int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber, Inv),
         int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber, Inv),
         int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber, Inv));
}
