namespace HighlighterScannerWebApp;

using OpenCvSharp;
using OpenCvSharp.Extensions;
using Tesseract;
using System.Drawing;

public class ScannerService
{
    private readonly string _tessDataPath = @"./tesseract.data.english"; // Path to your eng.traineddata

    public List<string> ExtractHighlightedText(string imagePath, HighlighterColour colourProfile)
    {
        var extractedTexts = new List<string>();
        
        using var src = Cv2.ImRead(imagePath);
        if (src.Empty()) throw new Exception("Image not found.");
        
        using var hsv = new Mat();
        Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
        
        using var mask = new Mat();
        var lowerBound = new Scalar(colourProfile.hueMin, colourProfile.saturationMin, colourProfile.valueMin);
        var upperBound = new Scalar(colourProfile.hueMax, colourProfile.saturationMax, colourProfile.valueMax);
        Cv2.InRange(hsv, lowerBound, upperBound, mask);
        
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        
        using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);

        foreach (var contour in contours)
        {
            // Filter out tiny noise contours
            if (Cv2.ContourArea(contour) < 500) continue;

            // Get bounding rectangle around the highlight
            var rect = Cv2.BoundingRect(contour);
            
            // Crop the image to just the highlighted area
            using var croppedMat = new Mat(src, rect);
            using var bitmap = BitmapConverter.ToBitmap(croppedMat);

            // 6. Perform OCR on the cropped area
            using var pix = Pix.LoadFromMemory(croppedMat.ToBytes());
            using var page = engine.Process(pix);
            string text = page.GetText().Trim();

            if (!string.IsNullOrWhiteSpace(text))
            {
                extractedTexts.Add(text);
            }
        }

        return extractedTexts;
    }
}