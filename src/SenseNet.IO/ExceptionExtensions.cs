using SenseNet.Client;
using System;
using System.Net.Http;
using System.Net;

namespace SenseNet.IO
{
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Returns whether the retry cycle should end.
        /// Checks if there was an exception during execution. In case there wasn't, the result
        /// is TRUE and the retry cycle breaks.
        /// In case there was an error and it is one of the well-known exceptions, this method
        /// returns FALSE and the system will retry the operation.
        /// If the exception is unknown, this method will throw it.
        /// </summary>
        /// <param name="exception">An exception if there was an error during execution.</param>
        /// <param name="remainingRetryCount">Retry iteration index that counts downwards towards 1.</param>
        public static bool CheckRetryConditionOrThrow(this Exception exception, int remainingRetryCount)
        {
            return exception switch
            {
                null => true,
                ClientException { StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.GatewayTimeout }
                    when remainingRetryCount > 1 => false,
                ClientException { InnerException: HttpRequestException rex } when remainingRetryCount > 1 &&
                    (rex.Message.Contains("The SSL connection could not be established") ||
                     rex.Message.Contains("An error occurred while sending the request"))
                    => false,
                ClientException { StatusCode: HttpStatusCode.InternalServerError } cex
                    when (cex.Message.Contains("Error in datastore when loading nodes.") ||
                          cex.Message.Contains("Data layer timeout occurred.")) &&
                         remainingRetryCount > 1 => false,
                _ => throw exception
            };
        }
    }
}
