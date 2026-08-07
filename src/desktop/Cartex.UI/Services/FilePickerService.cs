using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Cartex.UI.Services;

public sealed record PickedFile(Stream Content, string FileName, string ContentType);

public interface IFilePickerService
{
    Task<PickedFile?> PickImageAsync();
    Task<PickedFile?> PickSpreadsheetAsync();
    Task<Stream?> SaveFileAsync(string suggestedName, string extension);
    Task<string?> SaveFilePathAsync(string suggestedName, string extension);
}

public sealed class FilePickerService : IFilePickerService
{
    private TopLevel? _top;

    public void Attach(TopLevel top) => _top = top;

    public async Task<PickedFile?> PickImageAsync()
    {
        if (_top is null)
            return null;

        var files = await _top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"] }]
        });

        if (files.Count == 0)
            return null;

        var file = files[0];
        var stream = await file.OpenReadAsync();
        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        return new PickedFile(stream, file.Name, contentType);
    }

    public async Task<PickedFile?> PickSpreadsheetAsync()
    {
        if (_top is null)
            return null;

        var files = await _top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        });

        if (files.Count == 0)
            return null;

        var file = files[0];
        return new PickedFile(await file.OpenReadAsync(), file.Name,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public async Task<Stream?> SaveFileAsync(string suggestedName, string extension)
    {
        if (_top is null)
            return null;

        var file = await _top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = [$"*.{extension}"] }]
        });

        return file is null ? null : await file.OpenWriteAsync();
    }

    public async Task<string?> SaveFilePathAsync(string suggestedName, string extension)
    {
        if (_top is null)
            return null;

        var file = await _top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = [$"*.{extension}"] }]
        });

        return file?.TryGetLocalPath();
    }
}
