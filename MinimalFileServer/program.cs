using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<FileService>();

var app = builder.Build();

app.UseMiddleware<BasicAuthenticationMiddleware>();


// Serve static files from the "wwwroot" folder
app.UseDefaultFiles(); // Ensures index.html is served automatically
app.UseStaticFiles();  // Serves static files from wwwroot


// API route for browsing files and directories
app.MapGet("/api/files/{*path}", (string? path, FileService fileService) =>
{
    var rootPath = Path.Combine(Directory.GetCurrentDirectory(), "files");
    var fullPath = Path.Combine(rootPath, path ?? string.Empty);

    try
    {
        var contents = fileService.GetDirectoryContents(fullPath);
        return Results.Ok(contents.Select(item => new
        {
            item.Name,
            Path = Path.GetRelativePath(rootPath, item.FullName).Replace("\\", "/"),
            IsDirectory = item.Attributes.HasFlag(FileAttributes.Directory),
            Size = item is FileInfo fileInfo ? fileInfo.Length : (long?)null
        }));
    }
    catch (DirectoryNotFoundException)
    {
        return Results.NotFound("Directory not found.");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// API route for downloading files
app.MapGet("/api/files/download/{*filePath}", (string filePath) =>
{
    var rootPath = Path.Combine(Directory.GetCurrentDirectory(), "files");
    var fullPath = Path.Combine(rootPath, filePath);

    if (!File.Exists(fullPath))
    {
        return Results.NotFound("File not found.");
    }

    return Results.File(fullPath, "application/octet-stream", Path.GetFileName(fullPath));
});

// API route for uploading files
app.MapPost("/api/files/upload/{*path}", async (HttpRequest request, string? path) =>
{
    var rootPath = Path.Combine(Directory.GetCurrentDirectory(), "files");
    var targetDirectory = Path.Combine(rootPath, path ?? string.Empty);

    if (!Directory.Exists(targetDirectory))
    {
        Directory.CreateDirectory(targetDirectory);
    }

    if (!request.Form.Files.Any())
    {
        return Results.BadRequest("No files were uploaded.");
    }

    foreach (var file in request.Form.Files)
    {
        var filePath = Path.Combine(targetDirectory, file.FileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
    }

    return Results.Ok(new { Message = "Files uploaded successfully." });
});

app.MapGet("/api/files/search", (string query, FileService fileService) =>
{
    var rootPath = Path.Combine(Directory.GetCurrentDirectory(), "files");

    try
    {
        var results = fileService.SearchFiles(rootPath, query);
        return Results.Ok(results.Select(item => new
        {
            item.Name,
            Path = Path.GetRelativePath(rootPath, item.FullName).Replace("\\", "/"),
            IsDirectory = item.Attributes.HasFlag(FileAttributes.Directory),
            Size = item is FileInfo fileInfo ? fileInfo.Length : (long?)null
        }));
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});


app.Run();

public class FileService
{
    public IEnumerable<FileSystemInfo> GetDirectoryContents(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");
        }

        var directoryInfo = new DirectoryInfo(path);
        return directoryInfo.GetFileSystemInfos();
    }

    public IEnumerable<FileSystemInfo> SearchFiles(string rootPath, string query, long? minSize = null, long? maxSize = null)
    {
        var directoryInfo = new DirectoryInfo(rootPath);
        return directoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
            .Where(f => f.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(f => f is FileInfo fileInfo && (!minSize.HasValue || fileInfo.Length >= minSize)
                                              && (!maxSize.HasValue || fileInfo.Length <= maxSize));
    }



}
