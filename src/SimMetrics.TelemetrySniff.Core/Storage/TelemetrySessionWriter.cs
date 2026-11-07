using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SimMetrics.TelemetrySniff.Core.Models;

namespace SimMetrics.TelemetrySniff.Core.Storage
{
    /// <summary>
    /// High-throughput, non-blocking telemetry recorder that persists packets
    /// to newline-delimited JSON (.jsonl) and exports a schema catalog (.catalog.json).
    /// </summary>
    public class TelemetrySessionWriter : IDisposable
    {
        private readonly BlockingCollection<TelemetryPacket> _queue = new BlockingCollection<TelemetryPacket>(10000);
        private CancellationTokenSource? _cts;
        private Task? _writerTask;
        private StreamWriter? _writer;

        public string OutputDirectory { get; }
        public string? CurrentSessionFile { get; private set; }
        public string? CurrentCatalogFile { get; private set; }
        public bool IsRecording { get; private set; }
        public long FramesWritten { get; private set; }

        public TelemetrySessionWriter(string? outputDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrWhiteSpace(docs))
                {
                    docs = AppContext.BaseDirectory;
                }
                OutputDirectory = Path.Combine(docs, "SimMetrics", "TelemetrySniff");
            }
            else
            {
                OutputDirectory = outputDirectory!;
            }
        }

        public void Start(string gameName = "AssettoCorsa")
        {
            if (IsRecording) return;

            Directory.CreateDirectory(OutputDirectory);
            var safeGame = string.Concat(gameName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            CurrentSessionFile = Path.Combine(OutputDirectory, $"sniff_{safeGame}_{timestamp}.jsonl");
            CurrentCatalogFile = Path.Combine(OutputDirectory, $"sniff_{safeGame}_{timestamp}.catalog.json");

            _writer = new StreamWriter(new FileStream(CurrentSessionFile, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = false
            };

            _cts = new CancellationTokenSource();
            _writerTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
            FramesWritten = 0;
            IsRecording = true;
        }

        public void Enqueue(TelemetryPacket packet)
        {
            if (!IsRecording) return;
            _queue.TryAdd(packet);
        }

        public void Stop(TelemetryPacket? samplePacketForCatalog = null)
        {
            if (!IsRecording) return;
            IsRecording = false;

            _cts?.Cancel();

            try
            {
                _writerTask?.Wait(2000);
            }
            catch { /* ignore timeout */ }

            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;

            if (samplePacketForCatalog != null && CurrentCatalogFile != null)
            {
                ExportSchemaCatalog(samplePacketForCatalog, CurrentCatalogFile);
            }
        }

        private void ExportSchemaCatalog(TelemetryPacket sample, string catalogPath)
        {
            try
            {
                var descriptors = new List<PropertyDescriptor>();

                // Inspect Normalized channels
                foreach (var prop in typeof(NormalizedData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    descriptors.Add(new PropertyDescriptor
                    {
                        Name = $"Normalized.{prop.Name}",
                        TypeName = prop.PropertyType.Name,
                        SampleValue = prop.GetValue(sample.Normalized)
                    });
                }

                // Inspect RawPayload if available
                if (sample.RawPayload != null)
                {
                    foreach (var prop in sample.RawPayload.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        descriptors.Add(new PropertyDescriptor
                        {
                            Name = $"Raw.{prop.Name}",
                            TypeName = prop.PropertyType.Name,
                            SampleValue = prop.GetValue(sample.RawPayload)
                        });
                    }
                }

                var json = JsonConvert.SerializeObject(descriptors.OrderBy(d => d.Name), Formatting.Indented);
                File.WriteAllText(catalogPath, json);
            }
            catch { /* schema export best-effort */ }
        }

        private async Task ProcessQueueAsync(CancellationToken token)
        {
            int batchCount = 0;
            while (!token.IsCancellationRequested || _queue.Count > 0)
            {
                try
                {
                    if (_queue.TryTake(out var packet, 100, token))
                    {
                        var json = JsonConvert.SerializeObject(packet, Formatting.None);
                        if (_writer != null)
                        {
                            await _writer.WriteLineAsync(json);
                            FramesWritten++;
                            batchCount++;

                            if (batchCount >= 50)
                            {
                                await _writer.FlushAsync();
                                batchCount = 0;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Prevent queue loop crash
                }
            }

            if (_writer != null)
            {
                await _writer.FlushAsync();
            }
        }

        public void Dispose()
        {
            Stop();
            _queue.Dispose();
            _cts?.Dispose();
        }
    }
}
