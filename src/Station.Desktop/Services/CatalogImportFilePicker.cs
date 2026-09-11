using Microsoft.Win32;

namespace Station.Desktop.Services;

public interface ICatalogImportFilePicker { string? Pick(); }

public sealed class CatalogImportFilePicker : ICatalogImportFilePicker
{
    public string? Pick()
    {
        var dialog = new OpenFileDialog { Title = "选择曲库索引", Filter = "曲库索引 (*.json;*.jsonl)|*.json;*.jsonl|JSON (*.json)|*.json|JSON Lines (*.jsonl)|*.jsonl", CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
