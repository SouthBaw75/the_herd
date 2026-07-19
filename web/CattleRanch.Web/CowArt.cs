using System.Globalization;
using System.Text;
using CattleRanch.Sim;

namespace CattleRanch.Web;

/// <summary>
/// Hand-drawn SVG cow sprite, emitted as a raw markup string so it can be
/// injected (via MarkupString) inside an SVG element that Blazor owns.
/// Pure presentation: reads an Animal, draws shapes. No sim access, no RNG.
/// Style: cozy vector children's-book ranch — soft rounded shapes, warm colors.
/// </summary>
internal static class CowArt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string N(double v) => v.ToString("F1", Inv);

    private static string Esc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    /// <summary>
    /// Draws one cow centered on local (0,0), feet at y≈37, roughly 120 units
    /// wide. Caller wraps this in a positioned/translated &lt;g&gt;.
    /// </summary>
    public static string Svg(Animal a, double scale, bool flip, bool grazing, bool showName, bool newborn)
    {
        bool hereford = a.Breed == "Hereford";
        bool calf = a.AgeDays < 6 * GameDate.DaysPerSeason;
        bool bull = a.Sex == Sex.Male && !calf;
        bool matureCow = a.Sex == Sex.Female && !calf;
        bool preg = a.Pregnancy.IsPregnant;

        string body = hereford ? "#a8563a" : "#3a3431";
        string dark = hereford ? "#8a4128" : "#2a2522";
        string far = hereford ? "#7c3a24" : "#211d1a";
        string face = hereford ? "#f2e6cf" : "#3a3431";
        string muzzle = hereford ? "#e2b096" : "#8d8076";
        string tailTuft = hereford ? "#f2e6cf" : "#1d1a17";
        string cream = "#f2e6cf";

        var sb = new StringBuilder(2200);
        sb.Append("<g transform=\"scale(").Append(N(scale)).Append(")\">");

        // Ground shadow (never flipped)
        sb.Append("<ellipse class=\"cow-shadow\" cx=\"2\" cy=\"38\" rx=\"42\" ry=\"6.5\"/>");

        sb.Append("<g class=\"cow-pop\">");
        sb.Append("<g transform=\"scale(").Append(flip ? "-1" : "1").Append(",1)\">");

        // Tail (rear = +x)
        sb.Append("<path d=\"M 33 -15 C 45 -9 46 8 43 20\" fill=\"none\" stroke=\"")
          .Append(dark).Append("\" stroke-width=\"4\" stroke-linecap=\"round\"/>");
        sb.Append("<circle cx=\"43\" cy=\"23\" r=\"4.5\" fill=\"").Append(tailTuft).Append("\"/>");

        // Far legs
        Leg(sb, -20, far);
        Leg(sb, 16, far);

        // Body
        sb.Append("<ellipse cx=\"1\" cy=\"-3\" rx=\"38\" ry=\"21\" fill=\"").Append(body).Append("\"/>");
        if (bull)
        {
            // Chunky shoulder hump + thicker brisket
            sb.Append("<ellipse cx=\"-19\" cy=\"-17\" rx=\"19\" ry=\"11\" fill=\"").Append(body).Append("\"/>");
            sb.Append("<ellipse cx=\"-26\" cy=\"6\" rx=\"14\" ry=\"12\" fill=\"").Append(body).Append("\"/>");
        }

        if (preg)
        {
            sb.Append("<ellipse cx=\"8\" cy=\"6\" rx=\"26\" ry=\"16\" fill=\"").Append(body).Append("\"/>");
        }

        if (hereford)
        {
            // White underline along the belly — classic Hereford markings
            sb.Append("<ellipse cx=\"-2\" cy=\"").Append(preg ? "15" : "12")
              .Append("\" rx=\"22\" ry=\"7\" fill=\"").Append(cream).Append("\" opacity=\"0.9\"/>");
        }

        // Near legs
        Leg(sb, -30, dark);
        Leg(sb, 26, dark);

        // Udder for mature cows
        if (matureCow)
        {
            sb.Append("<ellipse cx=\"17\" cy=\"15\" rx=\"6\" ry=\"4.5\" fill=\"#dda093\"/>");
        }

        // Neck (part of the body so the grazing head rotates against it)
        sb.Append("<ellipse cx=\"-27\" cy=\"-9\" rx=\"14\" ry=\"12\" fill=\"").Append(body).Append("\"/>");

        // Head group — rotates down when grazing (CSS transition)
        sb.Append("<g class=\"cow-head").Append(grazing ? " grazing" : "").Append("\">");
        // Ear behind the head
        sb.Append("<ellipse cx=\"-36\" cy=\"-25\" rx=\"7\" ry=\"4\" fill=\"").Append(body)
          .Append("\" transform=\"rotate(-30 -36 -25)\"/>");
        sb.Append("<ellipse cx=\"-36.2\" cy=\"-24.8\" rx=\"3.8\" ry=\"2\" fill=\"#d9948a\" transform=\"rotate(-30 -36 -25)\"/>");
        if (bull)
        {
            sb.Append("<path d=\"M -49 -23 Q -56 -28 -55 -34\" fill=\"none\" stroke=\"#e8dcc2\" stroke-width=\"3.2\" stroke-linecap=\"round\"/>");
        }

        sb.Append("<ellipse cx=\"-45\" cy=\"-15\" rx=\"13\" ry=\"11\" fill=\"").Append(face).Append("\"/>");
        sb.Append("<ellipse cx=\"-53\" cy=\"-9\" rx=\"8\" ry=\"6\" fill=\"").Append(muzzle).Append("\"/>");
        sb.Append("<circle cx=\"-55.5\" cy=\"-9.5\" r=\"1\" fill=\"#3b2c22\"/>");
        if (!hereford)
        {
            // Light ring so the eye reads on a black face
            sb.Append("<circle cx=\"-44\" cy=\"-18\" r=\"3\" fill=\"#cfc4b2\" opacity=\"0.55\"/>");
        }

        sb.Append("<circle cx=\"-44\" cy=\"-18\" r=\"1.9\" fill=\"#241d15\"/>");
        sb.Append("<circle cx=\"-44.6\" cy=\"-18.6\" r=\"0.6\" fill=\"#fdf7ea\"/>");
        sb.Append("</g>"); // head

        sb.Append("</g>"); // flip
        sb.Append("</g>"); // pop

        // Badges + name float above, never mirrored
        if (preg)
        {
            sb.Append("<text class=\"cow-badge\" x=\"").Append(flip ? "-24" : "24").Append("\" y=\"-30\">\U0001F930</text>");
        }

        if (newborn)
        {
            sb.Append("<text class=\"cow-spark\" x=\"0\" y=\"-46\">✨</text>");
        }

        if (showName && a.Name.Length > 0)
        {
            sb.Append("<text class=\"cow-name\" x=\"0\" y=\"-54\">").Append(Esc(a.Name)).Append("</text>");
        }

        sb.Append("</g>"); // scale
        return sb.ToString();
    }

    private static void Leg(StringBuilder sb, double x, string fill)
    {
        sb.Append("<rect x=\"").Append(N(x)).Append("\" y=\"6\" width=\"8\" height=\"27\" rx=\"3.5\" fill=\"")
          .Append(fill).Append("\"/>");
        sb.Append("<rect x=\"").Append(N(x)).Append("\" y=\"31\" width=\"8\" height=\"6\" rx=\"2\" fill=\"#241c15\"/>");
    }
}
