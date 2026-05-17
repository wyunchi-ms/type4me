using Type4Me.Models;

namespace Type4Me.LLM;

internal static class PromptContextBuilder
{
    public static string Build(string prompt, string text, LLMRequestContext? context)
    {
        var finalPrompt = prompt.Replace("{text}", text.Trim());
        if (context == null) return finalPrompt;

        finalPrompt = finalPrompt
            .Replace("{current application name}", context.CurrentApplicationName)
            .Replace("{current_application_name}", context.CurrentApplicationName)
            .Replace("{currentApplicationName}", context.CurrentApplicationName);

        var screenshotText = context.HasScreenshot
            ? "The current application screenshot is attached as an image."
            : "No current application screenshot is available.";

        finalPrompt = finalPrompt
            .Replace("{current application screenshot}", screenshotText)
            .Replace("{current_application_screenshot}", screenshotText)
            .Replace("{currentApplicationScreenshot}", screenshotText);

        return finalPrompt;
    }
}
