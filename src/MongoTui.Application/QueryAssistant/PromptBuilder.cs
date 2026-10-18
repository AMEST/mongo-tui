using System.Text;
using MongoTui.Domain.Ai;

namespace MongoTui.Application.QueryAssistant;

/// <summary>Builds the prompt sent to the AI from a schema and the user request.</summary>
public sealed class PromptBuilder
{
    public string Build(QueryAssistantRequest request)
    {
        var builder = new StringBuilder();

        builder.AppendLine(request.Mode == QueryAssistantMode.Aggregate
            ? "Write a MongoDB aggregation pipeline as a JSON array of stages."
            : "Write a MongoDB find filter as a JSON object.");

        builder.AppendLine($"Database: {request.Schema.Collection.Database.Value}");

        if (request.Schema.Collections.Count > 0)
        {
            builder.AppendLine($"Collections: {string.Join(", ", request.Schema.Collections)}");
        }

        builder.AppendLine(
            $"Target collection: {request.Schema.Collection.Database.Value}.{request.Schema.Collection.Collection.Value}");

        builder.AppendLine("Fields:");
        foreach (var field in request.Schema.Fields)
        {
            builder.AppendLine($"- {field.Path}: {field.Type}");
        }

        if (!request.MaskValues)
        {
            builder.AppendLine("Sample values may be included in your reasoning.");
        }

        builder.AppendLine($"User request: {request.Prompt}");

        return builder.ToString();
    }
}
