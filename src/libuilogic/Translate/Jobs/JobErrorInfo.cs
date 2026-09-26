namespace Nikse.SubtitleEdit.UiLogic.Translate.Jobs
{
    /// <summary>
    /// A user-facing error: what happened, why, and what to do about it - plus the untouched
    /// technical text for the "details" expander. The translation layer never surfaces raw
    /// engine JSON on the beginner path.
    /// </summary>
    public sealed class JobErrorInfo
    {
        public string WhatHappened { get; }
        public string Why { get; }
        public string WhatToDo { get; }
        public string? TechnicalDetails { get; }

        public JobErrorInfo(string whatHappened, string why, string whatToDo, string? technicalDetails)
        {
            WhatHappened = whatHappened;
            Why = why;
            WhatToDo = whatToDo;
            TechnicalDetails = technicalDetails;
        }

        /// <summary>
        /// Maps the most common failure shapes to beginner language. Unknown failures get an
        /// honest generic message with the technical text attached - never a guess.
        /// </summary>
        public static JobErrorInfo FromException(Exception exception, string engineName)
        {
            var technical = exception.Message;
            var aggregated = exception as AggregateException;
            if (aggregated != null && aggregated.InnerExceptions.Count > 0)
            {
                technical = string.Join(Environment.NewLine, aggregated.InnerExceptions.Select(e => e.Message));
            }

            var message = technical ?? string.Empty;

            if (exception is OperationCanceledException)
            {
                return new JobErrorInfo(
                    "The job was cancelled.",
                    "Cancellation was requested while the job was running.",
                    "No action needed - progress up to this point was saved and the job can be resumed.",
                    technical);
            }

            if (exception is HttpRequestException || message.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("connectfailure", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("name or service not known", StringComparison.OrdinalIgnoreCase))
            {
                if (message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
                {
                    return new JobErrorInfo(
                        $"Could not reach {engineName}.",
                        "The service refused the connection - a local server (Ollama, llama.cpp, LM Studio) is probably not running.",
                        "Start the local service (or use the Auto-translate window to download and start it), then run the job again.",
                        technical);
                }

                return new JobErrorInfo(
                    "No internet connection (or the service could not be reached).",
                    "The selected engine runs online and the request could not be delivered.",
                    "Check the internet connection, or switch the job to a local engine.",
                    technical);
            }

            if (message.Contains("401", StringComparison.Ordinal) || message.Contains("403", StringComparison.Ordinal) ||
                message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase))
            {
                return new JobErrorInfo(
                    $"{engineName} rejected the API key.",
                    "The key is missing, wrong, or lacks access to the selected model.",
                    "Open Options ▸ Settings ▸ Auto-translate (or the Auto-translate window), enter a valid API key for this engine, and run the job again.",
                    technical);
            }

            if (message.Contains("404", StringComparison.Ordinal) || message.Contains("model", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return new JobErrorInfo(
                    $"{engineName} could not find the selected model.",
                    "The model name or the service address is wrong, or the model is not installed.",
                    "Check the engine settings (address and model), or pick a different engine.",
                    technical);
            }

            if (message.Contains("returned no translation", StringComparison.OrdinalIgnoreCase))
            {
                return new JobErrorInfo(
                    $"The translation engine {engineName} stopped answering.",
                    "The engine repeatedly returned no translation for a line.",
                    "Check that the engine/service is healthy, then resume the job - it continues from the last completed line.",
                    technical);
            }

            return new JobErrorInfo(
                "The job failed unexpectedly.",
                "An error occurred that the workflow does not recognize.",
                "Try running the job again; use 'Resume' to continue from the saved progress. Technical details are available below.",
                technical);
        }
    }
}
