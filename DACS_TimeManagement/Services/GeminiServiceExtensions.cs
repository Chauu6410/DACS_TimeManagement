using System.Threading;
using System.Threading.Tasks;
using DACS_TimeManagement.Services.Interfaces;

namespace DACS_TimeManagement.Services
{
    public static class GeminiServiceExtensions
    {
        // Attempts to use temperature-capable method on concrete GeminiService when available.
        public static Task<string> GenerateContent(this IGeminiService svc, string prompt, double temperature, CancellationToken cancellationToken = default)
        {
            if (svc is GeminiService concrete)
            {
                return concrete.GenerateContentWithTemperature(prompt, temperature, cancellationToken);
            }

            // Fallback: call existing method (temperature not supported)
            return svc.GenerateContent(prompt, cancellationToken);
        }
    }
}
