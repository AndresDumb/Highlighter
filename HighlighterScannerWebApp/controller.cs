using HighlighterScannerWebApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;



[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScannerController : ControllerBase
{
    private readonly ScannerDBContext _db;
    private readonly ScannerService _scannerService;

    public ScannerController(ScannerDBContext db, ScannerService scannerService)
    {
        _db = db;
        _scannerService = scannerService;
    }

    public string GetCurrentUser()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? throw new UnauthorizedAccessException("User not authenticated");
    }

    // 1. Save a new highlighter color profile
    [HttpPost("colours")]
    public async Task<IActionResult> AddColour([FromBody] HighlighterColour colour)
    {
        _db.HighlighterColours.Add(colour);
        await _db.SaveChangesAsync();
        return Ok(colour);
    }

    // 2. Upload an image, scan it, and log it to the timeline
    [HttpPost("scan/{colorId}")]
    public async Task<IActionResult> UploadAndScan(int colorId, IFormFile file)
    {
        var currentUserId = GetCurrentUser();
        var colour = await _db.HighlighterColours.FindAsync(colorId);
        if (colour == null) return NotFound("Colour profile not found.");

        // Save file temporarily
        var filePath = Path.GetTempFileName();
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Process Image
        var extractedLines = _scannerService.ExtractHighlightedText(filePath, colour);
        
        var userPageCount = await _db.ScannedPageLogs.Where(p => p.scannedBy == GetCurrentUser()).CountAsync();

        // Create log entry for timeline
        var pageLog = new ScannedPageLog
        {
            scannedTime = DateTime.UtcNow,
            fileName = file.FileName,
            pageNumber = userPageCount + 1,
            scannedBy = currentUserId// Simple page tracking
        };

        foreach (var text in extractedLines)
        {
            pageLog.ExtractedLines.Add(new ExtractedLine
            {
                lineText = text,
                colourId = colour.id
            });
        }

        _db.ScannedPageLogs.Add(pageLog);
        await _db.SaveChangesAsync();

        System.IO.File.Delete(filePath); // Cleanup

        return Ok(pageLog);
    }

    // 3. Get Timeline Data
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline()
    {
        var currentUserId = GetCurrentUser();
        var timeline = await _db.ScannedPageLogs
            .Where(p => p.scannedBy == currentUserId)
            .Include(p => p.ExtractedLines)
            .OrderByDescending(p => p.scannedTime) // Order by newest for timeline
            .ToListAsync();

        return Ok(new
        {
            TotalPagesLogged = timeline.Count,
            TimelineEvents = timeline
        });
    }
    
    
}
