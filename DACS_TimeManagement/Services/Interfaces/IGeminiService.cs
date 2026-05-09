using System.Threading.Tasks;

namespace DACS_TimeManagement.Services.Interfaces
{
    public interface IGeminiService
    {
        Task<string> GenerateContent(string prompt);
        Task<string> GenerateContent(string prompt, CancellationToken cancellationToken);
        string BuildAdvancedPrompt(string context, string goal, string userInput);
    }
}

