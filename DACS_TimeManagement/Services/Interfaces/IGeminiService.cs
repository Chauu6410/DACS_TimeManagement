using System.Threading.Tasks;

namespace DACS_TimeManagement.Services.Interfaces
{
    public interface IGeminiService
    {
        Task<string> GenerateContent(string prompt);
        Task<string> GenerateContent(string prompt, CancellationToken cancellationToken);
        IAsyncEnumerable<string> StreamGenerateContent(string prompt, double temperature = 0.2, CancellationToken cancellationToken = default);
        string BuildAdvancedPrompt(string context, string goal, string userInput);
    }
}

