using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Inject FileService with RootPath from configuration
var rootPath = builder.Configuration["FileServerSettings:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "files");
builder.Services.AddSingleton<FileService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<FileService>>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    return new FileService(rootPath, logger, configuration);
});

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
app.MapGet("/api/files/{*path}", (string? path, FileService fileService, int page = 1, int pageSize = 10) =>
{
    try
    {
        var contents = fileService.GetDirectoryContents(path, page, pageSize);
        return Results.Ok(contents);
    }
    catch (DirectoryNotFoundException)
    {
        return Results.NotFound("Directory not found.");
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            title: "An error occurred while processing your request.",
            instance: Guid.NewGuid().ToString()
        );
    }
});

// API route for downloading files
app.MapGet("/api/files/download/{*filePath}", (string filePath, FileService fileService) =>
{
    try
    {
        // Decode the filePath to handle URL-encoded values like %2F
        filePath = Uri.UnescapeDataString(filePath);

        var file = fileService.GetFile(filePath);
        return Results.File(file.FullPath, "application/octet-stream", file.Name);
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound("File not found.");
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            title: "An error occurred while processing your request.",
            instance: Guid.NewGuid().ToString()
        );
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
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            title: "An error occurred while processing your request.",
            instance: Guid.NewGuid().ToString()
        );
    }
});

// API route for searching files
app.MapGet("/api/files/search", (string query, FileService fileService) =>
{
    try
    {
        var results = fileService.SearchFiles(query);
        return Results.Ok(results);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            title: "An error occurred while processing your request.",
            instance: Guid.NewGuid().ToString()
        );
    }
});

// API route for fetching allowed file types
app.MapGet("/api/files/allowed-types", (IConfiguration configuration) =>
{
    var allowedTypes = configuration.GetSection("FileServerSettings:AllowedFileTypes").Get<string[]>();
    return Results.Ok(allowedTypes ?? Array.Empty<string>());
});

app.Run();

// FileService Class
public class FileService
{
    private readonly string _rootPath;
    private readonly ILogger<FileService> _logger;
    private readonly string[] _allowedExtensions;

    public FileService(string rootPath, ILogger<FileService> logger, IConfiguration configuration)
    {
        _rootPath = rootPath;
        _logger = logger;

        // Read allowed file types from appsettings.json
        _allowedExtensions = configuration.GetSection("FileServerSettings:AllowedFileTypes").Get<string[]>()
            ?? Array.Empty<string>();

        if (!Directory.Exists(_rootPath))
        {
            Directory.CreateDirectory(_rootPath);
            _logger.LogInformation($"Root directory created at {_rootPath}");
        }
    }

    public IEnumerable<FileItem> GetDirectoryContents(string? path, int page = 1, int pageSize = 10)
    {
        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");
        }

        var contents = new DirectoryInfo(fullPath).GetFileSystemInfos()
            .OrderBy(info => info.Attributes.HasFlag(FileAttributes.Directory) ? 0 : 1) // Folders first
            .ThenBy(info => info.Name, StringComparer.OrdinalIgnoreCase);

        return contents
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        var fileExtension = Path.GetExtension(file.FileName);

        // Validate file type using allowed extensions from configuration
        if (!_allowedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"File type '{fileExtension}' is not allowed.");
        }

        var fileName = file.FileName;
        var filePath = Path.Combine(fullPath, fileName);

        if (File.Exists(filePath))
        {
            var uniqueSuffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            fileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{uniqueSuffix}{fileExtension}";
            filePath = Path.Combine(fullPath, fileName);
        }

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        _logger.LogInformation($"File saved: {fileName} at {fullPath}");
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
