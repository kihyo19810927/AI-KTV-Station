using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Station.Application.Configuration;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Domain.Models;
using Station.Infrastructure.Media;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Scanning;
using Station.Infrastructure.Search;
using Xunit.Abstractions;

namespace Station.Core.Tests;

public sealed class ScanSampleBenchmarkTests(ITestOutputHelper output)
{
    [Fact, Trait("Category", "ManualBenchmark")]
    public async Task Ten_selected_files_are_measured_without_scanning_the_library()
    {
        var sampleRoot = Environment.GetEnvironmentVariable("KTV_SCAN_SAMPLE_ROOT");
        if (string.IsNullOrWhiteSpace(sampleRoot)) return;
        var work = Environment.GetEnvironmentVariable("KTV_SCAN_BENCHMARK_WORK")!;
        var ffprobe = Environment.GetEnvironmentVariable("KTV_STATION_FFPROBE")!;
        Directory.CreateDirectory(work);
        // Enumerate one explicitly selected directory; never recurse or traverse other years.
        var files = new DirectoryInfo(sampleRoot).EnumerateFiles().Where(x => MediaFormatPolicy.Classify(x.Name) == MediaEntryKind.PlayableMedia).Take(10).ToArray();
        Assert.Equal(10, files.Length);
        foreach (var mode in new[] { "remote", "local" })
        {
            var root = sampleRoot;
            if (mode == "local")
            {
                root = Path.Combine(work, "local-samples");
                Directory.CreateDirectory(root);
                // Matched copies give the local comparison identical codecs, duration and content.
                foreach (var file in files)
                {
                    var target = Path.Combine(root, file.Name);
                    await using (var input = file.OpenRead())
                    await using (var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
                        await input.CopyToAsync(destination);
                    File.SetLastWriteTimeUtc(target, file.LastWriteTimeUtc);
                }
            }
            foreach (var concurrency in new[] { 1, 2 })
            {
                await using var db = new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={Path.Combine(work, $"{mode}-{concurrency}.db")}").Options);
                await db.Database.MigrateAsync();
                var source = new MediaSource { Name = mode, RootPath = root };
                db.MediaSources.Add(source); await db.SaveChangesAsync();
                var options = new ScanOptions { BasicIndexOnly = true, ProbeConcurrency = concurrency };
                var scanner = new MediaScanService(new EfMediaScanRepository(db), new SelectedFiles(files.Select(x => x.Name).ToArray()),
                    mediaProbe: new FfprobeMediaProbe(ffprobe, TimeSpan.FromSeconds(30)), scanOptions: options,
                    searchIndex: new SqliteSongSearchIndex(db, new InvariantSearchTextNormalizer()));
                var timer = Stopwatch.StartNew();
                var basic = (await scanner.ScanAsync(source.Id)).Value;
                var basicSeconds = timer.Elapsed.TotalSeconds;
                Assert.Equal(10, basic.IndexedFiles);
                options.BasicIndexOnly = false;
                timer.Restart();
                var probed = (await scanner.ScanAsync(source.Id)).Value;
                var probeSeconds = timer.Elapsed.TotalSeconds;
                timer.Restart();
                var repeat = (await scanner.ScanAsync(source.Id)).Value;
                var result = JsonSerializer.Serialize(new
                {
                    mode,
                    concurrency,
                    count = 10,
                    basicSeconds,
                    probeSeconds,
                    repeatSeconds = timer.Elapsed.TotalSeconds,
                    probed.ProbedFiles,
                    probed.ErrorCount,
                    repeat.CachedFiles,
                    repeat.ProbeAttempts,
                    averageProbeSeconds = probed.ProbeAttempts == 0 ? 0 : probed.ProbeMilliseconds / probed.ProbeAttempts / 1000
                });
                output.WriteLine(result);
                await File.WriteAllTextAsync(Path.Combine(work, $"{mode}-{concurrency}.json"), result);
                if (mode == "local")
                {
                    Assert.Equal(10, probed.ProbedFiles);
                    Assert.Equal(0, repeat.ProbeAttempts);
                }
            }
        }
    }
    private sealed class SelectedFiles(string[] names) : IMediaFileEnumerator
    {
        public async IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var name in names)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = new FileInfo(Path.Combine(source.RootPath, name));
                yield return MediaEnumerationEntry.File(name, file.Length, file.LastWriteTimeUtc);
                await Task.Yield();
            }
        }
    }
}
