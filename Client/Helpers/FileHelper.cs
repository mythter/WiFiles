namespace Client.Helpers
{
    public static class FileHelper
    {
        public static string GetUniqueFilePath(string fileName, string directory)
        {
            string extension = Path.GetExtension(fileName);
            string tempName = Path.GetFileNameWithoutExtension(fileName);
            string filePath = Path.Combine(directory, fileName);
            int n = 1;
            while (File.Exists(filePath))
            {
                fileName = $"{tempName} ({n}){extension}";
                filePath = Path.Combine(directory, fileName);
                n++;
            }

            return filePath;
        }

        public static int GetBufferSizeByFileSize(long fileSize)
        {
            return fileSize switch
            {                             // file size is:
                < 10_485_760  => 1024,    // less than 10 MB
                < 104_857_600 => 4096,    // less than 100 MB
                _             => 16384    // more or equal 100 MB
            };
        }

        public static void DeleteFileIfExists(string? filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
