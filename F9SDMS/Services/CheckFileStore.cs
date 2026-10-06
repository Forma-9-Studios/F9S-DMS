namespace F9SDMS.Services
{
    /// <summary>
    /// Stores the PDFs submitted for checking (and the checkers' marked-up PDFs) in
    /// App_Data/checks under the app folder. Files are only served through the
    /// /checks/{id}/{kind} endpoint, which checks who is asking.
    /// </summary>
    public class CheckFileStore
    {
        /// <summary>Largest PDF accepted: 50 MB.</summary>
        public const long MaxBytes = 50L * 1024 * 1024;

        private readonly string _root;

        public CheckFileStore(IWebHostEnvironment env)
        {
            _root = Path.Combine(env.ContentRootPath, "App_Data", "checks");
            Directory.CreateDirectory(_root);
        }

        /// <summary>
        /// Copies a PDF from <paramref name="source"/> to disk and returns its stored name and size.
        /// Throws <see cref="InvalidDataException"/> if it isn't a PDF or is larger than 50 MB.
        /// </summary>
        public async Task<(string StoredName, long Size)> SaveAsync(Stream source, CancellationToken cancellationToken = default)
        {
            var storedName = $"{Guid.NewGuid():N}.pdf";
            var path = Path.Combine(_root, storedName);
            long size = 0;

            try
            {
                await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    var checkedHeader = false;
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        if (!checkedHeader)
                        {
                            // Every PDF starts with "%PDF-".
                            if (read < 5 || buffer[0] != '%' || buffer[1] != 'P' || buffer[2] != 'D' || buffer[3] != 'F' || buffer[4] != '-')
                            {
                                throw new InvalidDataException("That file isn't a PDF.");
                            }
                            checkedHeader = true;
                        }

                        size += read;
                        if (size > MaxBytes)
                        {
                            throw new InvalidDataException("The PDF is larger than 50 MB.");
                        }

                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }

                    if (!checkedHeader)
                    {
                        throw new InvalidDataException("The file is empty.");
                    }
                }

                return (storedName, size);
            }
            catch
            {
                Delete(storedName);
                throw;
            }
        }

        /// <summary>Full path of a stored file, or null if the name isn't one this store created.</summary>
        public string? GetPath(string? storedName)
        {
            if (string.IsNullOrEmpty(storedName) || storedName.Length != 36 || !storedName.EndsWith(".pdf", StringComparison.Ordinal)
                || !storedName[..32].All(Uri.IsHexDigit))
            {
                return null;
            }

            var path = Path.Combine(_root, storedName);
            return File.Exists(path) ? path : null;
        }

        public void Delete(string? storedName)
        {
            if (string.IsNullOrEmpty(storedName)) return;

            try
            {
                var path = Path.Combine(_root, Path.GetFileName(storedName));
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Best effort: an orphaned file is harmless.
            }
        }

        public static string FormatSize(long bytes) => bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:0.0} MB"
            : $"{Math.Max(1, bytes / 1024)} KB";
    }
}
