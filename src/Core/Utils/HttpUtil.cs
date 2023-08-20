using Blish_HUD;
using Flurl.Http;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nekres.Stream_Out {
    internal static class HttpUtil {

        public static bool TryParseJson<T>(string json, out T result) {
            bool success = true;
            var settings = new JsonSerializerSettings {
                Error = (_, args) => {
                    success = false; 
                    args.ErrorContext.Handled = true;
                },
                MissingMemberHandling = MissingMemberHandling.Error
            };
            result = JsonConvert.DeserializeObject<T>(json, settings);
            return success;
        }

        public static async Task<T> RetryAsync<T>(Func<Task<HttpResponseMessage>> request, int retries = 2, int delayMs = 2000, Logger logger = null) {

            async Task<T> HttpResponseWrapper() {
                var response = await request();
                var json= await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(json);
            }

            return await RetryAsync(HttpResponseWrapper, retries, delayMs, logger);
        }

        public static async Task<T> RetryAsync<T>(Func<Task<T>> request, int retries = 2, int delayMs = 2000, Logger logger = null) {

            logger ??= Logger.GetLogger(typeof(HttpUtil));

            try {
                return await request();
            } catch (Exception e) {

                if (retries > 0) {
                    logger.Warn(e, $"Failed to request data. Retrying in {delayMs / 1000} second(s) (remaining retries: {retries}).");
                    await Task.Delay(delayMs);
                    return await RetryAsync<T>(request, retries - 1, delayMs, logger);
                }

                //TODO: Consider adjusting exception behaviour and log levels.
                switch (e) {
                    case FlurlHttpTimeoutException:
                        logger.Warn(e, e.Message);
                        break;
                    case FlurlHttpException:
                        logger.Warn(e, e.Message);
                        break;
                    case JsonReaderException:
                        logger.Warn(e, e.Message);
                        break;
                    default:
                        logger.Error(e, e.Message);
                        break;
                }
            }

            return default;
        }
    }
}
