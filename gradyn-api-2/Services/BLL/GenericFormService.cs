using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using gradyn_api_2.Models;
using gradyn_api_2.Services.DAL;

namespace gradyn_api_2.Services.BLL;

/// <summary>
/// This service is for saving data to a CSV file in nextcloud. It tolerates concurrent submissions via a thread-safe
/// channel queue.
/// It also somewhat tolerates manual edits concurrent with the queue. This is done via an ETag and a 3-try retry policy.
/// Since it just retries 3 times when there is an ETag conflict, I do not recommend using this for when cross-service
/// concurrent writes are expected. You would want something like redis for that but that is out of scope here.
/// It is also designed to be purely config so that adding new static pages that just need to dump data into a convenient location
/// do not require code. Though if I ever do want that, I can make this abstract and it would be 1 DI singleton per backend
/// </summary>
public class GenericFormService : IGenericFormService
{
    private readonly IConfiguration _configuration;
    private readonly INextcloudClient _nextcloudClient;

    // Per-form writer queues/workers
    private readonly ConcurrentDictionary<string, FormWriter> _writers = new();

    private const int MaxETagRetries = 3;

    public GenericFormService(IConfiguration configuration, INextcloudClient nextcloudClient)
    {
        _configuration = configuration;
        _nextcloudClient = nextcloudClient;
    }

    /// <summary>
    /// Appends a CSV row for the given form key. The CSV header is read from the existing file on each submission.
    /// The submitted dictionary keys should match header names. Missing keys are written as empty cells.
    /// Extra keys are ignored.
    /// </summary>
    public Task SubmitAsync(
        string formKey,
        IReadOnlyDictionary<string, string?> fields,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formKey))
            throw new ArgumentException("formKey is required", nameof(formKey));
        if (fields is null)
            throw new ArgumentNullException(nameof(fields));

        var remotePath = ResolveRemotePath(formKey);

        var writer = _writers.GetOrAdd(formKey, _ => new FormWriter(remotePath, _nextcloudClient));
        return writer.EnqueueAsync(fields, cancellationToken);
    }

    private string ResolveRemotePath(string formKey)
    {
        var path = _configuration[$"FormFiles:{formKey}"];
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                $"No FormFiles mapping found for key '{formKey}'. Expected configuration key: FormFiles:{formKey}");
        }

        return path;
    }

    private sealed class FormWriter
    {
        private readonly string _remotePath;
        private readonly INextcloudClient _nextcloudClient;

        private readonly Channel<WorkItem> _channel;
        private readonly Task _worker;

        public FormWriter(string remotePath, INextcloudClient nextcloudClient)
        {
            _remotePath = remotePath;
            _nextcloudClient = nextcloudClient;

            _channel = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

            _worker = Task.Run(WorkerLoop);
        }

        public async Task EnqueueAsync(IReadOnlyDictionary<string, string?> fields, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await _channel.Writer.WriteAsync(new WorkItem(fields, tcs, ct), ct).ConfigureAwait(false);
            await tcs.Task.ConfigureAwait(false);
        }

        private async Task WorkerLoop()
        {
            await foreach (var item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (item.CallerToken.IsCancellationRequested)
                {
                    item.Tcs.TrySetCanceled(item.CallerToken);
                    continue;
                }

                try
                {
                    await AppendWithRetriesAsync(item.Fields, item.CallerToken).ConfigureAwait(false);
                    item.Tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    item.Tcs.TrySetException(ex);
                }
            }
        }

        private async Task AppendWithRetriesAsync(IReadOnlyDictionary<string, string?> fields, CancellationToken ct)
        {
            for (int attempt = 1; attempt <= MaxETagRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var download = await _nextcloudClient.DownloadAsync(_remotePath, ct).ConfigureAwait(false);
                

                string existingText;
                using (download.Stream)
                using (var reader = new StreamReader(download.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024, leaveOpen: false))
                {
                    existingText = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                }

                if (!TryGetFirstLine(existingText, out var headerLine))
                {
                    throw new InvalidOperationException($"CSV file '{_remotePath}' is empty; expected a header row.");
                }

                var headerColumns = ParseCsvLine(headerLine);
                if (headerColumns.Count == 0)
                    throw new InvalidOperationException($"CSV file '{_remotePath}' header row contained no columns.");

                // build row aligned to header
                var row = new string?[headerColumns.Count];
                for (int i = 0; i < headerColumns.Count; i++)
                {
                    var colName = headerColumns[i];
                    fields.TryGetValue(colName, out var value);
                    row[i] = value;
                }

                var sb = new StringBuilder(existingText.Length + 256);
                sb.Append(existingText);

                if (sb.Length > 0 && sb[^1] != '\n')
                    sb.Append('\n');

                sb.Append(ToCsvLine(row));
                sb.Append('\n');

                var newBytes = Encoding.UTF8.GetBytes(sb.ToString());
                using var uploadStream = new MemoryStream(newBytes, writable: false);

                try
                {
                    await _nextcloudClient.UploadAsync(
                        remotePath: _remotePath,
                        content: uploadStream,
                        ifMatchETag: download.ETag,
                        failIfExists: false,
                        contentType: "text/csv",
                        cancellationToken: ct).ConfigureAwait(false);
                    return;
                }
                catch (ETagConflictException) when (attempt < MaxETagRetries)
                {
                    // Someone (probably you) edited the file between our read and write. Retry.
                    await Task.Delay(50 * attempt, ct).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException($"Failed to append CSV row to '{_remotePath}' due to repeated ETag conflicts.");
        }

        // --------- CSV helpers ---------

        private static bool TryGetFirstLine(string text, out string firstLine)
        {
            if (string.IsNullOrEmpty(text))
            {
                firstLine = "";
                return false;
            }

            // Find first newline. Handle \r\n or \n
            int i = text.IndexOf('\n');
            if (i < 0)
            {
                // single-line file
                firstLine = text.TrimEnd('\r');
                return firstLine.Length > 0;
            }

            firstLine = text.Substring(0, i).TrimEnd('\r');
            return firstLine.Length > 0;
        }

        /// <summary>
        /// Parses a single CSV record line into fields (handles quotes and doubled quotes).
        /// Good enough for a header row.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (line.Length == 0) return result;

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // doubled quote -> literal quote
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            result.Add(sb.ToString());
            return result;
        }

        /// <summary>
        /// Converts values to a single CSV record line using RFC4180-ish quoting:
        /// - Quote fields containing comma, quote, CR, or LF
        /// - Escape quotes by doubling
        /// </summary>
        private static string ToCsvLine(IReadOnlyList<string?> values)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');

                var v = values[i] ?? "";
                var mustQuote = v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;

                if (!mustQuote)
                {
                    sb.Append(v);
                }
                else
                {
                    sb.Append('"');
                    sb.Append(v.Replace("\"", "\"\""));
                    sb.Append('"');
                }
            }
            return sb.ToString();
        }

        private readonly record struct WorkItem(
            IReadOnlyDictionary<string, string?> Fields,
            TaskCompletionSource Tcs,
            CancellationToken CallerToken);
    }
}