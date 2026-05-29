# Hướng Dẫn Xử Lý Lỗi AI Service

## Lỗi ServiceUnavailable (503)

### Nguyên Nhân
Lỗi này xảy ra khi:
1. **Model đang quá tải**: Gemini AI model đang có nhu cầu sử dụng cao
2. **Spike tạm thời**: Lượng request tăng đột biến
3. **Maintenance**: Google đang bảo trì service

### Giải Pháp Đã Implement

#### 1. **Retry Logic với Exponential Backoff**
```csharp
// Retry cho cả 429 (Rate Limit) và 503 (Service Unavailable)
if ((response.StatusCode == HttpStatusCode.TooManyRequests || 
     response.StatusCode == HttpStatusCode.ServiceUnavailable) && 
    retryCount < maxRetries)
{
    retryCount++;
    var backoffMs = (int)(Math.Pow(2, retryCount) * 1000) + new Random().Next(0, 500);
    await Task.Delay(backoffMs, cancellationToken);
    continue;
}
```

**Retry Schedule:**
- Retry 1: ~2 seconds
- Retry 2: ~4 seconds  
- Retry 3: ~8 seconds

#### 2. **Fallback Models**
Hệ thống tự động thử các model khác nếu model chính bị lỗi:

```csharp
private static readonly string[] FallbackModels = new[]
{
    "gemini-2.5-flash",      // Fastest, newest
    "gemini-2.0-flash",      // Stable
    "gemini-flash-latest",   // Latest stable
    "gemini-2.5-pro",        // More capable
    "gemini-3.5-flash"       // Alternative
};
```

#### 3. **Error Detection trong Stream**
```csharp
await foreach (var chunk in _geminiService.StreamGenerateContent(prompt, 0.5, cancellationToken))
{
    // Check if chunk contains error message
    if (chunk.StartsWith("Lỗi") || chunk.StartsWith("Error"))
    {
        hasError = true;
        // Send error to frontend
        var errorJson = JsonSerializer.Serialize(new { error = chunk });
        await Response.WriteAsync($"data: {errorJson}\n\n", cancellationToken);
        break;
    }
    // ... continue streaming
}
```

#### 4. **User-Friendly Error Messages**
```csharp
HttpStatusCode.ServiceUnavailable => 
    "Lỗi 503: Dịch vụ AI đang quá tải. Model đang có nhu cầu cao. Vui lòng thử lại sau vài phút."
```

### Cách Xử Lý Từ Frontend

#### 1. **Hiển Thị Thông Báo Thân Thiện**
```javascript
eventSource.onmessage = (event) => {
  const data = JSON.parse(event.data);
  
  if (data.error) {
    // Hiển thị lỗi cho user
    showErrorModal({
      title: "Lỗi",
      message: data.error,
      actions: [
        { text: "Thử lại", onClick: () => retryGeneration() },
        { text: "Đóng", onClick: () => closeModal() }
      ]
    });
    eventSource.close();
    return;
  }
  
  // Append chunk to display
  appendToDisplay(data);
};
```

#### 2. **Auto Retry với Delay**
```javascript
async function generateWithRetry(projectId, maxRetries = 2) {
  for (let i = 0; i <= maxRetries; i++) {
    try {
      await generateAIStrategy(projectId);
      return; // Success
    } catch (error) {
      if (i === maxRetries) {
        // Final retry failed
        showError("Không thể kết nối đến dịch vụ AI. Vui lòng thử lại sau.");
        return;
      }
      
      // Wait before retry
      const delay = Math.pow(2, i + 1) * 1000; // 2s, 4s, 8s
      await new Promise(resolve => setTimeout(resolve, delay));
    }
  }
}
```

#### 3. **Loading State với Timeout**
```javascript
const TIMEOUT = 60000; // 60 seconds

function generateAIStrategy(projectId) {
  return new Promise((resolve, reject) => {
    const eventSource = new EventSource(`/api/ai/stream-project-strategy?projectId=${projectId}`);
    
    const timeout = setTimeout(() => {
      eventSource.close();
      reject(new Error("Request timeout. Vui lòng thử lại."));
    }, TIMEOUT);
    
    eventSource.onmessage = (event) => {
      clearTimeout(timeout);
      // Handle message
    };
    
    eventSource.onerror = (error) => {
      clearTimeout(timeout);
      eventSource.close();
      reject(error);
    };
  });
}
```

### Best Practices

#### 1. **Không Spam Requests**
```javascript
// Debounce generate button
let isGenerating = false;

async function handleGenerateClick() {
  if (isGenerating) {
    showWarning("Đang tạo kế hoạch, vui lòng đợi...");
    return;
  }
  
  isGenerating = true;
  try {
    await generateAIStrategy(projectId);
  } finally {
    isGenerating = false;
  }
}
```

#### 2. **Cache Results**
```javascript
// Cache AI strategy để tránh regenerate không cần thiết
const strategyCache = new Map();

async function getOrGenerateStrategy(projectId) {
  if (strategyCache.has(projectId)) {
    return strategyCache.get(projectId);
  }
  
  const strategy = await generateAIStrategy(projectId);
  strategyCache.set(projectId, strategy);
  return strategy;
}
```

#### 3. **Inform User About Status**
```javascript
// Show progress indicator
function showGeneratingStatus() {
  showNotification({
    type: "info",
    message: "Đang tạo kế hoạch AI...",
    duration: null, // Don't auto-hide
    icon: "⏳"
  });
}

function showSuccessStatus() {
  showNotification({
    type: "success",
    message: "Đã tạo kế hoạch thành công!",
    duration: 3000,
    icon: "✅"
  });
}

function showErrorStatus(error) {
  showNotification({
    type: "error",
    message: error,
    duration: 5000,
    icon: "❌",
    actions: [
      { text: "Thử lại", onClick: () => retryGeneration() }
    ]
  });
}
```

### Monitoring & Logging

#### Backend Logging
```csharp
_logger.LogWarning("Gemini API {Status} for model {Model}. Retrying {Count}/{Max} after {Delay}ms...", 
    response.StatusCode, modelName, retryCount, maxRetries, backoffMs);

_logger.LogInformation("Trying next fallback model due to {Status} error", response.StatusCode);

_logger.LogInformation("Successfully saved AI strategy for project {ProjectId}", projectId);
```

#### Frontend Logging
```javascript
// Log errors to analytics
function logAIError(error, context) {
  analytics.track('ai_generation_error', {
    error: error.message,
    projectId: context.projectId,
    timestamp: new Date().toISOString(),
    userAgent: navigator.userAgent
  });
}
```

### Troubleshooting

#### Lỗi Vẫn Xảy Ra Sau Retry?

1. **Kiểm tra API Key**
   ```bash
   # Check appsettings.json
   "Gemini": {
     "ApiKey": "YOUR_API_KEY_HERE",
     "ModelName": "gemini-1.5-flash"
   }
   ```

2. **Kiểm tra Quota**
   - Truy cập: https://aistudio.google.com/app/apikey
   - Xem usage và limits
   - Nâng cấp plan nếu cần

3. **Thử Model Khác**
   ```json
   "Gemini": {
     "ModelName": "gemini-2.0-flash"  // Try different model
   }
   ```

4. **Giảm Request Size**
   ```csharp
   // Reduce prompt length
   private const int MaxUserInputLength = 4000; // Reduce from 6000
   ```

5. **Tăng Timeout**
   ```csharp
   builder.Services.AddHttpClient<IGeminiService, GeminiService>()
       .ConfigureHttpClient(client => {
           client.Timeout = TimeSpan.FromSeconds(120); // Increase timeout
       });
   ```

### Khi Nào Cần Liên Hệ Support?

- Lỗi 503 kéo dài > 30 phút
- Tất cả fallback models đều fail
- Quota không tăng sau khi nâng cấp
- API Key bị revoke không rõ lý do

**Google AI Support**: https://ai.google.dev/support

### Summary

✅ **Đã Implement:**
- Retry logic với exponential backoff
- Fallback models tự động
- Error detection trong stream
- User-friendly error messages
- Comprehensive logging

✅ **Frontend Nên Làm:**
- Auto retry với delay
- Loading states rõ ràng
- Cache results
- Debounce requests
- Inform user về status

✅ **Monitoring:**
- Log tất cả errors
- Track retry attempts
- Monitor success rate
- Alert khi error rate cao

Với các cải tiến này, hệ thống sẽ xử lý lỗi ServiceUnavailable một cách graceful và user-friendly! 🚀
