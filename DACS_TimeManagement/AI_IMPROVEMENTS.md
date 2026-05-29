# Cải Tiến Chức Năng AI - DACS Time Management

## Tổng Quan

Đã cải thiện chức năng AI trong project để hoạt động mượt mà hơn với khả năng gợi ý task thông minh và thêm trực tiếp vào Kanban board.

## Các Cải Tiến Chính

### 1. **AITaskService - Service Chuyên Biệt**
- **File mới**: `Services/AITaskService.cs`
- **Chức năng**:
  - Extract tasks từ markdown response của AI
  - Import tasks vào project với validation đầy đủ
  - Tự động tạo BoardList (To Do, In Progress, Done) nếu chưa có
  - Kiểm tra duplicate tasks theo cả Key và Title
  - Parse priority và category từ AI suggestions
  - Tính toán dates dựa trên estimatedDays

### 2. **Cải Thiện AIController**
- **Thêm dependency injection**: `IAITaskService`
- **Thêm logging**: Đầy đủ error tracking và info logging
- **Endpoint mới**: `POST /api/ai/extract-tasks` - Extract tasks từ strategy text
- **Cải thiện endpoint**: `POST /api/ai/import-tasks` - Import với validation tốt hơn

### 3. **Enhanced AI Prompts**
Cải thiện prompt cho `stream-project-strategy`:

#### Tiếng Việt:
```
- Tạo 5-8 tasks cụ thể, thực tế
- Priority: Low, Medium, High, Urgent
- Category: Planning, Design, Development, Testing, Deployment, Documentation
- estimatedDays: 1-30 ngày
- Key unique: task_1, task_2, task_3...
```

#### English:
```
- Create 5-8 specific, realistic, actionable tasks
- Priority levels and categories
- Estimated completion days
- Unique task keys
```

### 4. **Enhanced SuggestedTaskDTO**
Thêm các field mới:
```csharp
public class SuggestedTaskDTO
{
    public string Key { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Priority { get; set; } = "Medium";      // NEW
    public int EstimatedDays { get; set; } = 7;           // NEW
    public string Category { get; set; } = "General";     // NEW
}
```

### 5. **Cải Thiện IGeminiService Interface**
Thêm overload method:
```csharp
Task<string> GenerateContent(string prompt, double temperature, CancellationToken cancellationToken);
```

### 6. **Service Registration**
Đã đăng ký `AITaskService` trong `Program.cs`:
```csharp
builder.Services.AddScoped<IAITaskService, AITaskService>();
```

## API Endpoints

### 1. Generate AI Strategy (Stream)
```http
GET /api/ai/stream-project-strategy?projectId={id}
```
- Stream AI-generated strategy với task suggestions
- Tự động lưu vào database
- Hỗ trợ đa ngôn ngữ (vi/en)

### 2. Extract Tasks
```http
POST /api/ai/extract-tasks
Content-Type: application/json

{
  "strategyText": "markdown text with ```json-tasks``` block"
}
```
**Response:**
```json
{
  "success": true,
  "tasks": [
    {
      "key": "task_1",
      "title": "Task title",
      "description": "Task description",
      "priority": "High",
      "estimatedDays": 3,
      "category": "Planning"
    }
  ],
  "count": 5
}
```

### 3. Import Tasks to Kanban
```http
POST /api/ai/import-tasks
Content-Type: application/json

{
  "projectId": 1,
  "tasks": [
    {
      "key": "task_1",
      "title": "Setup project structure",
      "description": "Initialize project with proper folder structure",
      "priority": "High",
      "estimatedDays": 2,
      "category": "Planning"
    }
  ]
}
```
**Response:**
```json
{
  "success": true,
  "count": 5,
  "message": "Successfully added 5 task(s) to Kanban board"
}
```

### 4. Translate Strategy
```http
POST /api/ai/translate-strategy
Content-Type: application/json

{
  "projectId": 1,
  "targetLang": "vi"
}
```

## Workflow Sử Dụng

### Frontend Integration Flow:

1. **User clicks "Generate Plan"**
   ```javascript
   // Call stream endpoint
   const eventSource = new EventSource(`/api/ai/stream-project-strategy?projectId=${projectId}`);
   
   eventSource.onmessage = (event) => {
     const chunk = JSON.parse(event.data);
     // Append to display
     strategyDisplay.innerHTML += chunk;
   };
   ```

2. **Extract tasks from generated strategy**
   ```javascript
   const response = await fetch('/api/ai/extract-tasks', {
     method: 'POST',
     headers: { 'Content-Type': 'application/json' },
     body: JSON.stringify({ strategyText: fullStrategyText })
   });
   
   const { tasks } = await response.json();
   // Display tasks in UI
   ```

3. **User selects tasks to import**
   ```javascript
   const selectedTasks = tasks.filter(t => userSelected.includes(t.key));
   
   const response = await fetch('/api/ai/import-tasks', {
     method: 'POST',
     headers: { 'Content-Type': 'application/json' },
     body: JSON.stringify({
       projectId: projectId,
       tasks: selectedTasks
     })
   });
   
   const { success, count, message } = await response.json();
   // Show success notification
   // Refresh Kanban board
   ```

## Tính Năng Nổi Bật

### ✅ Duplicate Prevention
- Kiểm tra theo `AITaskKey` (cross-language sync)
- Fallback kiểm tra theo normalized title
- Tránh tạo task trùng lặp khi switch language

### ✅ Smart Task Categorization
- Parse priority từ AI suggestions
- Tính toán dates dựa trên estimated days
- Tự động assign vào "To Do" column

### ✅ Robust Error Handling
- Comprehensive logging
- Graceful degradation
- Clear error messages

### ✅ Multi-language Support
- Vietnamese và English prompts
- Preserve task keys khi translate
- Consistent task tracking across languages

### ✅ Auto Board Setup
- Tự động tạo BoardLists nếu chưa có
- Default columns: To Do, In Progress, Done
- Proper positioning và ordering

## Testing

### Test Import Tasks:
```bash
# 1. Generate strategy
curl -X GET "https://localhost:5001/api/ai/stream-project-strategy?projectId=1"

# 2. Extract tasks
curl -X POST "https://localhost:5001/api/ai/extract-tasks" \
  -H "Content-Type: application/json" \
  -d '{"strategyText": "...markdown with json-tasks..."}'

# 3. Import tasks
curl -X POST "https://localhost:5001/api/ai/import-tasks" \
  -H "Content-Type: application/json" \
  -d '{
    "projectId": 1,
    "tasks": [
      {
        "key": "task_1",
        "title": "Test Task",
        "description": "Test Description",
        "priority": "High",
        "estimatedDays": 3,
        "category": "Testing"
      }
    ]
  }'
```

## Logging

Service ghi log đầy đủ:
- Task extraction success/failure
- Duplicate detection
- Board list creation
- Import results
- Error details

Check logs:
```bash
# Development
dotnet run

# Production
tail -f /var/log/dacs-timemanagement/app.log
```

## Future Enhancements

### Đề xuất cải tiến tiếp theo:

1. **Smart Category Mapping**
   - Map categories to specific BoardLists
   - "Planning" → "To Do"
   - "Development" → "In Progress"
   - "Testing" → "Review"

2. **Task Dependencies**
   - AI suggests task order
   - Automatic dependency linking
   - Critical path detection

3. **Effort Estimation**
   - AI estimates story points
   - Team velocity tracking
   - Sprint planning assistance

4. **Auto-Assignment**
   - AI suggests assignees based on skills
   - Workload balancing
   - Team member availability

5. **Progress Tracking**
   - AI monitors task completion
   - Suggests adjustments
   - Risk detection

## Troubleshooting

### Issue: Tasks not appearing in Kanban
**Solution**: Check BoardList creation in logs, ensure project has proper permissions

### Issue: Duplicate tasks created
**Solution**: Verify AITaskKey is properly set, check normalization logic

### Issue: AI returns empty json-tasks
**Solution**: Check prompt configuration, verify Gemini API response, review temperature settings

### Issue: Import fails with 500 error
**Solution**: Check logs for detailed error, verify database connection, ensure user has project access

## Kết Luận

Chức năng AI đã được cải thiện đáng kể với:
- ✅ Architecture tốt hơn (separation of concerns)
- ✅ Error handling robust
- ✅ Logging đầy đủ
- ✅ Validation chặt chẽ
- ✅ Multi-language support
- ✅ Smart task suggestions
- ✅ Seamless Kanban integration

Project giờ đây có thể tự động gợi ý và tạo tasks từ AI một cách mượt mà và đáng tin cậy.
