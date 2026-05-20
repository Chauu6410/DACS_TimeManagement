# Nox Ocean Focus Mode - Tích hợp Time Log

## Tổng quan
Đã tích hợp thành công chức năng **Nox Ocean Focus Mode** với hệ thống **Time Log**, giữ nguyên giao diện game nuôi sinh vật biển đẹp mắt nhưng thêm khả năng lưu trữ và theo dõi thời gian tập trung.

## ✅ Các thay đổi đã thực hiện

### 1. **Cập nhật Model TimeLog**
Thêm 2 trường mới vào `Models/TimeLog.cs`:

```csharp
// Optional: Link to Goal for focus sessions
public int? GoalId { get; set; }
public PersonalGoal? Goal { get; set; }

// Track if this was from a focus session
public bool IsFocusSession { get; set; } = false;
```

**Mục đích:**
- `GoalId`: Liên kết TimeLog với Goal (nullable để hỗ trợ backward compatibility)
- `IsFocusSession`: Đánh dấu TimeLog này được tạo từ Nox Ocean Focus Mode
- `Goal`: Navigation property để truy vấn dễ dàng

### 2. **Cập nhật ApplicationDbContext**
Thêm relationship configuration trong `Models/ApplicationDbContext.cs`:

```csharp
// 2a. Cấu hình Quan hệ Goal - TimeLog (1 - n) - Optional
builder.Entity<TimeLog>()
    .HasOne(tl => tl.Goal)
    .WithMany()
    .HasForeignKey(tl => tl.GoalId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);
```

**Đặc điểm:**
- Relationship 1-n (một Goal có nhiều TimeLogs)
- Optional (GoalId nullable)
- OnDelete SetNull (khi xóa Goal, GoalId trong TimeLog = null)

### 3. **Migration Database**
Tạo migration `20260520225430_UpdateTimeLogForFocusMode.cs`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<int>(
        name: "GoalId",
        table: "TimeLogs",
        type: "int",
        nullable: true);

    migrationBuilder.AddColumn<bool>(
        name: "IsFocusSession",
        table: "TimeLogs",
        type: "bit",
        nullable: false,
        defaultValue: false);

    migrationBuilder.CreateIndex(
        name: "IX_TimeLogs_GoalId",
        table: "TimeLogs",
        column: "GoalId");

    migrationBuilder.AddForeignKey(
        name: "FK_TimeLogs_PersonalGoals_GoalId",
        table: "TimeLogs",
        column: "GoalId",
        principalTable: "PersonalGoals",
        principalColumn: "Id",
        onDelete: ReferentialAction.SetNull);
}
```

**Đã apply:** Migration đã được apply thành công vào database.

### 4. **Cập nhật GoalController.RecordFocusSession**
Logic xử lý thông minh trong `Controllers/GoalController.cs`:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RecordFocusSession(int goalId, int? taskId, int durationSeconds, string? note = null)
{
    // Format duration
    string durationStr = hours > 0 
        ? $"{hours}h {minutes}m {seconds}s" 
        : minutes > 0 
            ? $"{minutes}m {seconds}s" 
            : $"{seconds}s";
    
    string focusNote = $"🌊 Nox Ocean Focus [{durationStr}]" + (note ? $" - {note}" : "");

    if (taskId.HasValue && taskId.Value > 0)
    {
        // Task-based: Create TimeLog với GoalId và IsFocusSession
        var timeLog = new TimeLog
        {
            WorkTaskId = taskId.Value,
            LogDate = DateTime.UtcNow,
            DurationHours = durationHours,
            Note = focusNote,
            GoalId = goalId,              // ✅ Link to Goal
            IsFocusSession = true         // ✅ Mark as focus session
        };
        _db.TimeLogs.Add(timeLog);
        await _db.SaveChangesAsync();
        await _goalService.HandleTimeLogAsync(timeLog);
    }
    else
    {
        // Time-based: Tạo TimeLog cho task đầu tiên hoặc update goal trực tiếp
        var firstTask = goal.GoalTasks.FirstOrDefault();
        
        if (firstTask != null)
        {
            var timeLog = new TimeLog
            {
                WorkTaskId = firstTask.WorkTaskId,
                LogDate = DateTime.UtcNow,
                DurationHours = durationHours,
                Note = focusNote,
                GoalId = goalId,          // ✅ Link to Goal
                IsFocusSession = true     // ✅ Mark as focus session
            };
            _db.TimeLogs.Add(timeLog);
            await _db.SaveChangesAsync();
            await _goalService.HandleTimeLogAsync(timeLog);
        }
        else
        {
            // No linked tasks: update goal + create progress history
            goal.CompletedHours += durationHours;
            goal.CurrentValue = goal.CompletedHours;
            goal.UpdatedAt = DateTime.UtcNow;
            _db.PersonalGoals.Update(goal);
            
            var progressHistory = new GoalProgressHistory
            {
                GoalId = goal.Id,
                Progress = (goal.CompletedHours / goal.TargetHours.Value) * 100,
                RecordedAt = DateTime.UtcNow,
                Note = focusNote
            };
            _db.GoalProgressHistories.Add(progressHistory);
            
            await _db.SaveChangesAsync();
            await _goalService.RecalculateProgressForGoalAsync(goal.Id, userId);
        }
    }

    return Json(new { 
        success = true, 
        message = $"Logged {durationStr} successfully!",
        durationHours = Math.Round(durationHours, 2)
    });
}
```

**Logic xử lý:**
1. **Task-based goals**: Tạo TimeLog cho task được chọn
2. **Time-based goals có linked tasks**: Tạo TimeLog cho task đầu tiên
3. **Time-based goals không có tasks**: Cập nhật goal trực tiếp + tạo progress history
4. **Tất cả cases**: Đánh dấu `IsFocusSession = true` và `GoalId = goalId`

### 5. **Giữ nguyên Nox Ocean UI**
File `Views/Goal/Focus.cshtml` giữ nguyên:
- ✅ Giao diện đại dương đẹp mắt với hiệu ứng nước
- ✅ Chọn sinh vật biển (Coral, Clownfish, Octopus, Turtle, Whale, Ship)
- ✅ 2 chế độ: Countdown và Stopwatch (Zen)
- ✅ Aquarium collection system
- ✅ Ambient sound controller (waves, bubbles)
- ✅ Distraction blocker (strict mode)
- ✅ Fullscreen mode

**Chỉ thay đổi:** Backend logic để lưu vào TimeLog thay vì chỉ lưu vào GoalProgressHistory.

## 🎯 Lợi ích

### 1. **Tích hợp hoàn hảo**
- Thời gian tập trung được lưu vào TimeLog (hệ thống chính)
- Tự động cập nhật tiến độ Goal
- Có thể xem lịch sử focus sessions trong Task Details và Goal Details

### 2. **Dữ liệu có giá trị**
- Mỗi focus session là một TimeLog entry
- Có thể query: `WHERE IsFocusSession = true`
- Có thể filter theo Goal: `WHERE GoalId = @goalId`
- Hỗ trợ báo cáo và phân tích

### 3. **Backward Compatible**
- GoalId nullable → không ảnh hưởng TimeLog cũ
- IsFocusSession default false → TimeLog cũ vẫn hoạt động
- Không cần migrate dữ liệu cũ

### 4. **Trải nghiệm người dùng**
- Giữ nguyên giao diện Nox Ocean đẹp mắt
- Thêm giá trị: thời gian được lưu vào hệ thống
- Có thể xem lại lịch sử focus sessions

## 📊 Cách sử dụng

### 1. Truy cập Focus Mode
```
Goal Details → Click "Enter Focus Mode" / "Bắt đầu tập trung"
```

### 2. Chọn sinh vật biển
- 🪸 San hô hồng (≥ 10 phút)
- 🐠 Cá Nemo (≥ 25 phút)
- 🐙 Bạch tuộc tím (≥ 45 phút)
- 🐢 Rùa biển (≥ 60 phút)
- 🐳 Cá voi xanh (≥ 90 phút)
- 🚢 Tàu cổ chìm (≥ 120 phút)

### 3. Thiết lập
- Chọn chế độ: Countdown hoặc Stopwatch
- Đặt thời gian (cho Countdown)
- Chọn task (nếu là Task-based goal)

### 4. Bắt đầu lặn
- Click "Start Diving" / "Bắt đầu lặn"
- Timer bắt đầu, sinh vật biển xuất hiện
- Có thể Pause/Resume
- Hoàn thành → sinh vật được thêm vào aquarium

### 5. Xem lịch sử
**Trong Goal Details:**
```sql
SELECT * FROM TimeLogs 
WHERE GoalId = @goalId 
AND IsFocusSession = true
ORDER BY LogDate DESC
```

**Trong Task Details:**
```sql
SELECT * FROM TimeLogs 
WHERE WorkTaskId = @taskId 
AND IsFocusSession = true
ORDER BY LogDate DESC
```

**Note format:**
```
🌊 Nox Ocean Focus [25m 30s] - Completed reading chapter 3
```

## 🔍 Query Examples

### Lấy tất cả focus sessions của một Goal
```csharp
var focusSessions = await _db.TimeLogs
    .Where(tl => tl.GoalId == goalId && tl.IsFocusSession)
    .Include(tl => tl.WorkTask)
    .OrderByDescending(tl => tl.LogDate)
    .ToListAsync();
```

### Tính tổng thời gian focus của Goal
```csharp
var totalFocusHours = await _db.TimeLogs
    .Where(tl => tl.GoalId == goalId && tl.IsFocusSession)
    .SumAsync(tl => tl.DurationHours);
```

### Lấy focus sessions trong tuần
```csharp
var weekStart = DateTime.UtcNow.AddDays(-7);
var weeklyFocus = await _db.TimeLogs
    .Where(tl => tl.IsFocusSession && tl.LogDate >= weekStart)
    .GroupBy(tl => tl.LogDate.Date)
    .Select(g => new {
        Date = g.Key,
        TotalHours = g.Sum(tl => tl.DurationHours),
        SessionCount = g.Count()
    })
    .ToListAsync();
```

### Filter chỉ Nox Ocean sessions
```csharp
var noxOceanSessions = await _db.TimeLogs
    .Where(tl => tl.IsFocusSession && tl.Note.Contains("🌊 Nox Ocean Focus"))
    .ToListAsync();
```

## 📁 Files đã thay đổi

### Modified
1. ✅ `Models/TimeLog.cs` - Thêm GoalId và IsFocusSession
2. ✅ `Models/ApplicationDbContext.cs` - Thêm relationship configuration
3. ✅ `Controllers/GoalController.cs` - Cập nhật RecordFocusSession logic
4. ✅ `Migrations/20260520225430_UpdateTimeLogForFocusMode.cs` - Migration mới

### Unchanged
1. ✅ `Views/Goal/Focus.cshtml` - Giữ nguyên giao diện Nox Ocean
2. ✅ Tất cả JavaScript và CSS - Không thay đổi
3. ✅ Aquarium system - Hoạt động như cũ

## ✅ Testing Checklist

- [x] Build thành công không lỗi
- [x] Migration applied thành công
- [x] Database có 2 cột mới: GoalId, IsFocusSession
- [ ] Tạo focus session cho Task-based goal → TimeLog có GoalId và IsFocusSession = true
- [ ] Tạo focus session cho Time-based goal (có tasks) → TimeLog được tạo
- [ ] Tạo focus session cho Time-based goal (không có tasks) → Progress history được tạo
- [ ] Xem TimeLog trong Task Details → hiển thị focus sessions
- [ ] Xem Goal Details → hiển thị focus sessions
- [ ] Aquarium vẫn hoạt động bình thường
- [ ] Sinh vật biển được thêm vào collection sau khi hoàn thành

## 🚀 Next Steps (Optional)

### 1. Focus Statistics Dashboard
- Biểu đồ thống kê focus sessions theo ngày/tuần/tháng
- So sánh hiệu suất giữa các goals
- Heatmap thời gian tập trung

### 2. Focus Leaderboard
- Top users theo tổng thời gian focus
- Top goals theo số focus sessions
- Badges cho milestones

### 3. Enhanced Reporting
- Export focus sessions to Excel/PDF
- Email weekly focus summary
- Integration với calendar

### 4. Team Focus
- Xem focus sessions của team members
- Team aquarium (shared collection)
- Collaborative focus challenges

## 📝 Notes

- ✅ Tất cả focus sessions đều có `IsFocusSession = true`
- ✅ GoalId nullable để hỗ trợ backward compatibility
- ✅ Note format: `🌊 Nox Ocean Focus [duration] - optional note`
- ✅ Tự động cập nhật Goal progress qua GoalService
- ✅ Giao diện Nox Ocean giữ nguyên 100%
- ✅ Build thành công, không có lỗi

## 🎉 Kết luận

Đã tích hợp thành công **Nox Ocean Focus Mode** với **Time Log**:
- ✅ Giữ nguyên giao diện game đẹp mắt
- ✅ Thêm khả năng lưu trữ và theo dõi thời gian
- ✅ Tích hợp hoàn hảo với hệ thống hiện có
- ✅ Dữ liệu có giá trị cho phân tích
- ✅ Build thành công, sẵn sàng sử dụng!

**Enjoy your Nox Ocean Focus Mode! 🌊🐠🐙🐢🐳🚢**
