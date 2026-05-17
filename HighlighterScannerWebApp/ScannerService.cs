namespace HighlighterScannerWebApp;

using OpenCvSharp;
using Tesseract;
using System.IO;

public class ScannerService
{
    // data path for tesseract... i hope this is right
    private readonly string _tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"); 

    public List<string> ExtractHighlightedText(string img, HighlighterColour cp) // shorter names
    {
        var results = new List<string>();
        
        using var src = Cv2.ImRead(img);
        if (src.Empty()) {
            throw new Exception("where is the image??");
        }

        using var hsv = new Mat();
        Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);

        // magic numbers and conversions...
        int h = cp.hue / 2; 
        int s = (int)(cp.saturation * 2.55); 
        int v = (int)(cp.value * 2.55);      

        int hTol = 20; // hue tolerance
        int svTol = 20; // saturation and value tolerance

        var low = new Scalar(Math.Max(0, h - hTol), Math.Max(0, s - svTol), Math.Max(0, v - svTol));
        var high = new Scalar(Math.Min(180, h + hTol), Math.Min(255, s + svTol), Math.Min(255, v + svTol));

        using var mask = new Mat();
        Cv2.InRange(hsv, low, high, mask);

        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        // start the engine!!
        using var eng = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);

        foreach (var c in contours)
        {
            // ignore small stuff
            if (Cv2.ContourArea(c) < 500) continue;

            var r = Cv2.BoundingRect(c);
            using var crop = new Mat(src, r);
            
            Cv2.ImEncode(".png", crop, out var b); // encode to png for tesseract
            
            using var pix = Pix.LoadFromMemory(b);
            using var pg = eng.Process(pix);
            
            string t = pg.GetText().Trim();
            if (!string.IsNullOrWhiteSpace(t)) { 
                results.Add(t); 
            }
        }

        return results;
    }
}