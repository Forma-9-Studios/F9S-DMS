namespace F9SDMS.Services
{
    /// <summary>
    /// Stores files attached to IT tickets (screenshots, logs, documents) in App_Data/tickets.
    /// Files are only served through /tickets/file/{messageId}, which checks who is asking.
    /// </summary>
    public class TicketFileStore
    {
        /// <summary>Largest attachment accepted: 10 MB.</summary>
        public const long MaxBytes = 10L * 1024 * 1024;

        /// <summary>Allowed file types and how they're served.</summary>
        private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".pdf"] = "application/pdf",
            [".txt"] = "text/plain",
            [".log"] = "text/plain",
            [".zip"] = "application/zip",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        public const string Accept = ".png,.jpg,.jpeg,.gif,.webp,.pdf,.txt,.log,.zip,.docx,.xlsx";

        private readonly string _root;

        public TicketFileStore(IWebHostEnvironment env)
        {
            _root = Path.Combine(env.ContentRootPath, "App_Data", "tickets");
            Directory.CreateDirectory(_root);
        }

        public static bool IsAllowed(string fileName) => ContentTypes.ContainsKey(Path.GetExtension(fileName));

        public static bool IsImage(string? fileName) =>
            fileName is not null && ContentTypes.TryGetValue(Path.GetExtension(fileName), out var type) && type.StartsWith("image/");

        public static string ContentTypeOf(string fileName) =>
            ContentTypes.TryGetValue(Path.GetExtension(fileName), out var type) ? type : "application/octet-stream";

        /// <summary>Copies the upload to disk and returns its stored name and size.</summary>
        public async Task<(string StoredName, long Size)> SaveAsync(Stream source, string fileName, CancellationToken cancellationToken = default)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (!ContentTypes.ContainsKey(ext))
            {
                throw new InvalidDataException("That type of file can't be attached.");
            }

            var storedName = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(_root, storedName);
            long size = 0;

            try
            {
                await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        size += read;
                        if (size > MaxBytes)
                        {
                            throw new InvalidDataException("The file is larger than 10 MB.");
                        }
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                }

                if (size == 0)
                {
                    throw new InvalidDataException("The file is empty.");
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
            if (string.IsNullOrEmpty(storedName) || storedName.Length < 33 || !storedName[..32].All(Uri.IsHexDigit)
                || !ContentTypes.ContainsKey(Path.GetExtension(storedName)) || storedName != Path.GetFileName(storedName))
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
                // Best effort.
            }
        }
    }
}
