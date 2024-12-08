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
    }
}
