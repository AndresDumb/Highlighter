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
    
    public ScannerController(ScannerDBContext db, ScannerService s) // s for service
    {
        _db = db;
        _scannerService = s;
    }

    private string GetMe() // renamed from GetCurrentUser
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == null) throw new UnauthorizedAccessException("oops not logged in");
        return id;
    }

    [HttpPost("colours")]
    public async Task<IActionResult> AddColour([FromBody] HighlighterColour c)
    {
        c.addedBy = GetMe();
        _db.HighlighterColours.Add(c);
        await _db.SaveChangesAsync();
        return Ok(c);
    }

    [HttpGet("colours")]
    public async Task<IActionResult> GetColours()
    {
        var me = GetMe();
        var cols = await _db.HighlighterColours.Where(x => x.addedBy == me).ToListAsync();
        return Ok(cols);
    }

    [HttpPost("scan/{colourId}")]
    public async Task<IActionResult> UploadAndScan(int colourId, IFormFile file)
    {
        var me = GetMe();
        var col = await _db.HighlighterColours.FindAsync(colourId);
        
        if (col == null || col.addedBy != me) {
            return NotFound("that colour doesn't exist for you buddy");
        }

        // temp file... hope it deletes
        var path = Path.GetTempFileName();
        using (var s = new FileStream(path, FileMode.Create)) { 
            await file.CopyToAsync(s); 
        }

        var lines = _scannerService.ExtractHighlightedText(path, col);
        
        // count how many pages they have
        var count = await _db.ScannedPageLogs.Where(p => p.scannedBy == me).CountAsync();
        
        var log = new ScannedPageLog { 
            scannedTime = DateTime.UtcNow, 
            fileName = file.FileName, 
            pageNumber = count + 1, 
            scannedBy = me 
        };

        foreach (var t in lines) { 
            log.ExtractedLines.Add(new ExtractedLine { lineText = t, colourId = col.id }); 
        }

        _db.ScannedPageLogs.Add(log);
        await _db.SaveChangesAsync();
        
        // cleanup!!
        System.IO.File.Delete(path); 
        
        return Ok(log);
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline()
    {
        var me = GetMe();
        var list = await _db.ScannedPageLogs
            .Where(p => p.scannedBy == me)
            .Include(p => p.ExtractedLines)
            .OrderByDescending(p => p.scannedTime)
            .ToListAsync();
            
        return Ok(new { TotalPagesLogged = list.Count, TimelineEvents = list });
    }
}
