using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Inject FileService with RootPath from configuration
var rootPath = builder.Configuration["FileServerSettings:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "files");
builder.Services.AddSingleton<FileService>(_ => new FileService(rootPath));

// Explicitly configure Kestrel using appsettings.json
builder.WebHost.ConfigureKestrel(options =>
{
    options.Configure(builder.Configuration.GetSection("Kestrel"));
});

var app = builder.Build();

app.UseMiddleware<BasicAuthenticationMiddleware>();

// Serve static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// API route for browsing files and directories
app.MapGet("/api/files/{*path}", (string? path, FileService fileService) =>
{
    try
    {
        var contents = fileService.GetDirectoryContents(path);
        return Results.Ok(contents.Select(item => new
        {
            item.Name,
            item.Path,
            item.IsDirectory,
            item.Size
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
app.MapGet("/api/files/download/{*filePath}", (string filePath, FileService fileService) =>
{
    try
    {
        var file = fileService.GetFile(filePath);
        return Results.File(file.FullPath, "application/octet-stream", file.Name);
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound("File not found.");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// API route for uploading files
app.MapPost("/api/files/upload/{*path}", async (HttpRequest request, string? path, FileService fileService) =>
{
    if (!request.Form.Files.Any())
    {
        return Results.BadRequest("No files were uploaded.");
    }

    try
    {
        foreach (var file in request.Form.Files)
        {
            await fileService.SaveFileAsync(file, path);
        }
        return Results.Ok(new { Message = "Files uploaded successfully." });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// API route for searching files
app.MapGet("/api/files/search", (string query, FileService fileService) =>
{
    try
    {
        var results = fileService.SearchFiles(query);
        return Results.Ok(results.Select(item => new
        {
            item.Name,
            item.Path,
            item.IsDirectory,
            item.Size
        }));
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();

// FileService Class
public class FileService
{
    private readonly string _rootPath;

    public FileService(string rootPath)
    {
        _rootPath = rootPath;
        if (!Directory.Exists(_rootPath))
        {
            Directory.CreateDirectory(_rootPath);
        }
    }

    public IEnumerable<FileItem> GetDirectoryContents(string? path)
    {
        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");
        }

        return new DirectoryInfo(fullPath).GetFileSystemInfos()
            .Select(info => new FileItem
            {
                Name = info.Name,
                Path = Path.GetRelativePath(_rootPath, info.FullName).Replace("\\", "/"),
                IsDirectory = info.Attributes.HasFlag(FileAttributes.Directory),
                Size = info is FileInfo fileInfo ? fileInfo.Length : (long?)null
            });
    }

    public FileItem GetFile(string filePath)
    {
        var fullPath = GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"The file '{filePath}' does not exist.");
        }

        return new FileItem
        {
            Name = Path.GetFileName(fullPath),
            Path = Path.GetRelativePath(_rootPath, fullPath).Replace("\\", "/"),
            FullPath = fullPath,
            IsDirectory = false,
            Size = new FileInfo(fullPath).Length
        };
    }

    public async Task SaveFileAsync(IFormFile file, string? path)
    {
        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        var filePath = Path.Combine(fullPath, file.FileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
    }

    public IEnumerable<FileItem> SearchFiles(string query)
    {
        return new DirectoryInfo(_rootPath).EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
            .Where(info => info.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(info => new FileItem
            {
                Name = info.Name,
                Path = Path.GetRelativePath(_rootPath, info.FullName).Replace("\\", "/"),
                IsDirectory = info.Attributes.HasFlag(FileAttributes.Directory),
                Size = info is FileInfo fileInfo ? fileInfo.Length : (long?)null
            });
    }

    private string GetFullPath(string? path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, path ?? string.Empty));

        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        return fullPath;
    }
}

// FileItem Class
public class FileItem
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string FullPath { get; set; }
    public bool IsDirectory { get; set; }
    public long? Size { get; set; }
}
