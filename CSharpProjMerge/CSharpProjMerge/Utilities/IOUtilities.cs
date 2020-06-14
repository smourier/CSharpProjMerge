using System;
using System.IO;

namespace CSharpProjMerge.Utilities
{
    public static class IOUtilities
    {
        public static bool EnsureFileDirectory(string filePath, bool throwOnError = true)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.GetFullPath(filePath);
            }

            var dir = Path.GetDirectoryName(filePath);
            if (dir == null || Directory.Exists(dir))
                return false;

            try
            {
                Directory.CreateDirectory(dir);
                return true;
            }
            catch
            {
                if (throwOnError)
                    throw;

                return false;
            }
        }
    }
}
