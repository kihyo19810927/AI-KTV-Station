using System.Xml;
using System.Xml.Linq;
using Station.Application.Metadata;

namespace Station.Infrastructure.Metadata;

public sealed class NfoXmlMetadataReader(long maximumBytes = 1024 * 1024) : INfoMetadataReader
{
    public async Task<NfoReadResult> ReadForMediaAsync(string mediaPath, CancellationToken cancellationToken = default)
    {
        var nfoPath = Path.ChangeExtension(mediaPath, ".nfo");
        if (!File.Exists(nfoPath)) return NfoReadResult.Missing;

        try
        {
            var info = new FileInfo(nfoPath);
            if (info.Length > maximumBytes) return Warning("metadata.nfo_too_large");
            await using var stream = new FileStream(nfoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = maximumBytes,
                IgnoreComments = true,
            });
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
            var title = Value(document, "title");
            var artists = document.Descendants().Where(x => Is(x, "artist"))
                .Select(x => x.HasElements ? x.Elements().FirstOrDefault(e => Is(e, "name"))?.Value : x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var metadata = new NfoSongMetadata(
                title,
                artists,
                Value(document, "language"),
                Value(document, "category") ?? Value(document, "genre"),
                ParseYear(Value(document, "year")),
                Value(document, "quality"),
                Value(document, "version") ?? Value(document, "edition"));
            return new NfoReadResult(metadata, []);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Warning("metadata.nfo_unreadable");
        }
    }

    private static string? Value(XContainer document, string name) => document.Descendants().FirstOrDefault(x => Is(x, name))?.Value.Trim() is { Length: > 0 } value ? value : null;
    private static bool Is(XElement element, string name) => string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);
    private static int? ParseYear(string? value) => int.TryParse(value, out var year) && year is >= 1000 and <= 9999 ? year : null;
    private static NfoReadResult Warning(string code) => new(null, [code]);
}
