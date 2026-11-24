namespace F9SDMS.Services
{
    /// <summary>
    /// Profile photos: one small JPEG per person (cropped and resized in the browser), stored in
    /// App_Data/photos (or FileStorage:Root/photos on the server) and served through /photos/{userId}.
    /// </summary>
    public class ProfilePhotos
    {
        /// <summary>Largest photo accepted after cropping (they're about 20–60 KB).</summary>
        public const int MaxBytes = 512 * 1024;

        private readonly string _root;

        public ProfilePhotos(IWebHostEnvironment env, IConfiguration config)
        {
            var root = config["FileStorage:Root"];
            _root = Path.Combine(string.IsNullOrWhiteSpace(root) ? Path.Combine(env.ContentRootPath, "App_Data") : root, "photos");
            Directory.CreateDirectory(_root);
        }

        /// <summary>The address of someone's photo, or null when they have none (show initials).</summary>
        public static string? Url(string userId, DateTime? updatedAt) =>
            updatedAt is DateTime at ? $"/photos/{Uri.EscapeDataString(userId)}?v={at.Ticks}" : null;

        /// <summary>Saves a JPEG. Throws <see cref="InvalidDataException"/> if it isn't one or is too big.</summary>
        public async Task SaveAsync(string userId, byte[] jpeg)
        {
            if (jpeg.Length == 0 || jpeg.Length > MaxBytes)
                throw new InvalidDataException("The photo is too large.");
            if (jpeg.Length < 3 || jpeg[0] != 0xFF || jpeg[1] != 0xD8 || jpeg[2] != 0xFF)
                throw new InvalidDataException("That isn't a JPEG image.");

            var path = PathFor(userId);
            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, jpeg);
            File.Move(temp, path, overwrite: true);
        }

        public void Delete(string userId)
        {
            try
            {
                var path = PathFor(userId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Best effort: an orphaned file is harmless.
            }
        }

        /// <summary>Full path of someone's photo file, or null if there isn't one.</summary>
        public string? GetPath(string userId)
        {
            var path = PathFor(userId);
            return File.Exists(path) ? path : null;
        }

        // Identity user ids are GUIDs; anything else never names a file.
        private string PathFor(string userId) =>
            Path.Combine(_root, Guid.TryParse(userId, out var id) ? $"{id:N}.jpg" : "invalid.jpg");
    }
}
