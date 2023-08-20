using System;
using System.IO;
using System.Threading.Tasks;
using Blish_HUD;
namespace Nekres.Stream_Out
{
    internal static class FileUtil
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(FileUtil));

        public static async Task WriteAllTextAsync(string filePath, string data, bool overwrite = true)
        {
            if (string.IsNullOrEmpty(filePath)) {
                return;
            }

            if (!overwrite && File.Exists(filePath)) {
                return;
            }

            data ??= string.Empty;

            try {
                using var sw = new StreamWriter(filePath);
                await sw.WriteAsync(data);
            } catch (Exception e) {
                switch (e) { 
                    case DirectoryNotFoundException:
                        Logger.Info(e, e.Message);
                        break;
                    case IOException:
                        Logger.Info(e, e.Message);
                        break;
                    case UnauthorizedAccessException:
                        Logger.Info(e, e.Message);
                        break;
                    default:
                        Logger.Warn(e, e.Message);
                        break;
                }
            }
        }

        public static async Task<bool> DeleteAsync(string filePath, int retries = 2, int delayMs = 2000, Logger logger = null)
        {
            logger ??= Logger.GetLogger(typeof(FileUtil));
            try {
                File.Delete(filePath);
                return true;
            } catch (Exception e) {
                if (retries > 0) {
                    await Task.Delay(delayMs);
                    return await DeleteAsync(filePath, retries - 1, delayMs, logger);
                }

                switch (e) {
                    case IOException:
                        logger.Info(e, e.Message);
                        break;
                    case UnauthorizedAccessException:
                        logger.Info(e, e.Message);
                        break;
                    default:
                        logger.Warn(e, e.Message);
                        break;
                }

                return false;
            }
        }

        public static async Task<bool> DeleteDirectoryAsync(string dirPath, int retries = 2, int delayMs = 2000, Logger logger = null)
        {
            logger ??= Logger.GetLogger(typeof(FileUtil));
            try {
                Directory.Delete(dirPath, true);
                return true;
            } catch (Exception e) {
                if (retries > 0) {
                    await Task.Delay(delayMs);
                    return await DeleteDirectoryAsync(dirPath, retries - 1, delayMs, logger);
                }

                switch (e) {
                    case IOException:
                        logger.Info(e, e.Message);
                        break;
                    case UnauthorizedAccessException:
                        logger.Info(e, e.Message);
                        break;
                    default:
                        logger.Error(e, e.Message);
                        break;
                }

                return false;
            }
        }
    }
}
