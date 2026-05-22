namespace DACS_TimeManagement.Services.Interfaces
{
    public interface IOtpService
    {
        /// <summary>Sinh mã OTP 6 số, lưu DB và gửi qua email.</summary>
        Task GenerateAndSendOtpAsync(string userId, string email);

        /// <summary>Kiểm tra mã OTP có hợp lệ không (đúng, chưa dùng, chưa hết hạn).</summary>
        Task<bool> VerifyOtpAsync(string userId, string code);
    }
}
