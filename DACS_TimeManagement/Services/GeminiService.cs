using System;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Threading;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DACS_TimeManagement.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _apiVersion;
        private readonly int _maxPromptLength;

        public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _config = configuration;
            _logger = logger;
            _apiKey = _config["Gemini:ApiKey"];
            // Prefer newer stable model by default; can be overridden in appsettings.json
            _model = _config["Gemini:ModelName"] ?? "gemini-flash-latest";
            _apiVersion = _config["Gemini:ApiVersion"] ?? "v1beta";
            // Read configured max prompt length once (fallback to 8000 chars/bytes)
            _maxPromptLength = _config.GetValue<int>("Gemini:MaxPromptLength", 8000);
        }


        public Task<string> GenerateContent(string prompt)
        {
            // Default behavior: delegate to core implementation with default temperature
            return GenerateContent(prompt, CancellationToken.None);
        }

        public async Task<string> GenerateContent(string prompt, CancellationToken cancellationToken = default)
        {
            // Default temperature used by legacy callers
            return await GenerateContentInternal(prompt, 0.2, cancellationToken);
        }

        // Public helper used by callers that can cast to concrete implementation
        public async Task<string> GenerateContentWithTemperature(string prompt, double temperature, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("Gemini API Key is missing.");
                return "Lỗi: Chưa cấu hình API Key cho dịch vụ AI.";
            }

            // Log kích thước prompt thực tế trước khi gửi
            int charCount = prompt.Length;
            int byteCount = Encoding.UTF8.GetByteCount(prompt);
            _logger.LogInformation("Sending prompt: {Chars} chars, {Bytes} bytes", charCount, byteCount);

            var url = $"https://generativelanguage.googleapis.com/{_apiVersion}/models/{_model}:generateContent?key={_apiKey}";

            return await GenerateContentInternal(prompt, temperature, cancellationToken);
        }

        public async IAsyncEnumerable<string> StreamGenerateContent(string prompt, double temperature = 0.2, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                yield return "Lỗi: Chưa cấu hình API Key.";
                yield break;
            }

            var url = $"https://generativelanguage.googleapis.com/{_apiVersion}/models/{_model}:streamGenerateContent?key={_apiKey}&alt=sse";
            
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = temperature,
                    maxOutputTokens = 4096,
                    candidateCount = 1
                }
            };

            int retryCount = 0;
            const int maxRetries = 2;
            HttpResponseMessage response = null;

            while (true)
            {
                var json = JsonSerializer.Serialize(requestBody);
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                if (response.StatusCode == HttpStatusCode.TooManyRequests && retryCount < maxRetries)
                {
                    retryCount++;
                    var backoffMs = (int)(Math.Pow(2, retryCount) * 1000) + new Random().Next(0, 500);
                    _logger.LogWarning("Gemini Stream API Rate Limit hit (429). Retrying {Count}/{Max} after {Delay}ms...", retryCount, maxRetries, backoffMs);
                    await Task.Delay(backoffMs, cancellationToken);
                    continue;
                }
                break;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                yield return HandleApiError(response.StatusCode, errorBody);
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.StartsWith("data: ")) continue;

                var jsonData = line.Substring(6);
                if (jsonData == "[DONE]") break;

                // Attempt to parse only when the data chunk appears JSON-like; skip malformed chunks
                var trimmed = jsonData.TrimStart();
                if (trimmed.Length > 0 && trimmed[0] == '{')
                {
                    using var doc = JsonDocument.Parse(jsonData);
                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var content = candidates[0].GetProperty("content");
                        if (content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        {
                            var text = parts[0].GetProperty("text").GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                yield return text;
                            }
                        }
                    }
                }
            }
        }

        private async Task<string> GenerateContentInternal(string prompt, double temperature, CancellationToken cancellationToken = default)
        {
            var url = $"https://generativelanguage.googleapis.com/{_apiVersion}/models/{_model}:generateContent?key={_apiKey}";
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = temperature,
                    maxOutputTokens = 4096,
                    candidateCount = 1
                }
            };

            int retryCount = 0;
            const int maxRetries = 3;
            HttpResponseMessage response = null;
            string responseBody = null;

            while (true)
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests && retryCount < maxRetries)
                {
                    retryCount++;
                    var backoffMs = (int)(Math.Pow(2, retryCount) * 500) + new Random().Next(0, 200);
                    _logger.LogWarning("Gemini API Rate Limit hit (429). Retrying {Count}/{Max} after {Delay}ms...", retryCount, maxRetries, backoffMs);
                    await Task.Delay(backoffMs, cancellationToken);
                    continue;
                }

                break;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API error. Status: {Status}, Body: {Body}", response.StatusCode, responseBody);
                return HandleApiError(response.StatusCode, responseBody);
            }

            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("No candidates in Gemini response. Body: {Body}", responseBody);
                return "AI không đưa ra phản hồi nào (có thể do nội dung bị chặn hoặc prompt không hợp lệ).";
            }

            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("finishReason", out var finishReasonProp))
            {
                var finishReason = finishReasonProp.GetString();
                if (finishReason != "STOP")
                {
                    _logger.LogWarning("Gemini finish reason: {FinishReason}", finishReason);
                    return finishReason switch
                    {
                        "SAFETY" => "Nội dung bị chặn bởi bộ lọc an toàn. Vui lòng diễn đạt lại yêu cầu.",
                        "MAX_TOKENS" => "Phản hồi bị cắt do vượt quá giới hạn token. Hãy rút gọn câu hỏi.",
                        _ => $"AI dừng với lý do: {finishReason}. Vui lòng thử lại."
                    };
                }
            }

            if (firstCandidate.TryGetProperty("content", out var contentObj) &&
                contentObj.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textProp))
            {
                var text = textProp.GetString();
                return !string.IsNullOrEmpty(text) ? text : "AI trả về nội dung rỗng.";
            }

            _logger.LogInformation("Gemini response missing text structure. Body: {Body}", responseBody);
            return "AI không thể tạo phản hồi ở định dạng mong đợi.";
        }

        private string HandleApiError(HttpStatusCode statusCode, string responseBody = null)
        {
            string detailedMessage = string.Empty;
            if (!string.IsNullOrEmpty(responseBody))
            {
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg))
                    {
                        detailedMessage = $" Chi tiết: {msg.GetString()}";
                    }
                }
                catch { }
            }

            if (statusCode == HttpStatusCode.BadRequest)
            {
                if (responseBody != null && (responseBody.Contains("too long", StringComparison.OrdinalIgnoreCase) ||
                                             responseBody.Contains("length", StringComparison.OrdinalIgnoreCase)))
                    return "Lỗi 400: Prompt quá dài. Hệ thống đã tự động rút gọn tối đa. Vui lòng nhập nội dung ngắn hơn (dưới 500 ký tự).";

                if (responseBody != null && responseBody.Contains("API_KEY", StringComparison.OrdinalIgnoreCase))
                    return "Lỗi 400: API Key không hợp lệ. Vui lòng kiểm tra lại cấu hình.";

                return "Lỗi 400: Yêu cầu không hợp lệ (Prompt quá dài hoặc ký tự lạ)." + detailedMessage;
            }

            return statusCode switch
            {
                HttpStatusCode.NotFound => "Lỗi 404: Không tìm thấy Model hoặc Endpoint. Kiểm tra cấu hình ModelName." + detailedMessage,
                HttpStatusCode.TooManyRequests => "Lỗi 429: Bạn đã vượt quá hạn mức yêu cầu (Quota). Vui lòng đợi khoảng 1 phút rồi thử lại. " + detailedMessage,
                HttpStatusCode.Unauthorized => "Lỗi 401: API Key không hợp lệ." + detailedMessage,
                HttpStatusCode.Forbidden => "Lỗi 403: Bị từ chối truy cập." + detailedMessage,
                _ => $"Lỗi hệ thống ({statusCode}): Vui lòng liên hệ quản trị viên." + detailedMessage
            };
        }

        // Cho phép nhập dài hơn để chứa đủ JSON tasks cho AutoPlan
        private const int MaxUserInputLength = 6000;

        public string BuildAdvancedPrompt(string context, string goal, string userInput)
        {
            // Cắt user input nếu quá dài
            var safeUserInput = TruncateSafe(userInput, MaxUserInputLength);

            var prompt = $"Vai trò: {context}\n\nMục tiêu: {goal}\n\nDữ liệu:\n{safeUserInput}";

            // Ensure total length stays within configured limits
            if (prompt.Length > _maxPromptLength)
            {
                prompt = TruncateSafe(prompt, _maxPromptLength);
            }

            _logger.LogInformation("Final prompt length: {Chars} chars, {Bytes} bytes", prompt.Length, Encoding.UTF8.GetByteCount(prompt));
            return prompt.Trim();
        }

        private static string TruncateSafe(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || maxLength <= 0)
                return string.Empty;

            if (text.Length <= maxLength)
                return text;

            int truncateLength = maxLength;
            if (char.IsHighSurrogate(text[truncateLength - 1]))
                truncateLength--;

            return text.Substring(0, truncateLength) + "…";
        }
    }
}