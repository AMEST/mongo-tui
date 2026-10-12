using AwesomeAssertions;
using MongoTui.Application.QueryAssistant;
using MongoTui.Domain.Ai;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.QueryAssistant;

public sealed class PromptBuilderTests
{
    private readonly PromptBuilder _sut = new();

    [Fact]
    public void Should_ContainCollectionName_When_Built()
    {
        var request = CreateRequest(QueryAssistantMode.Find);

        var prompt = _sut.Build(request);

        prompt.Should().Contain("app.users");
    }

    [Fact]
    public void Should_AskForArray_When_AggregateMode()
    {
        var request = CreateRequest(QueryAssistantMode.Aggregate);

        var prompt = _sut.Build(request);

        prompt.Should().Contain("JSON array");
    }

    [Fact]
    public void Should_AskForObject_When_FindMode()
    {
        var request = CreateRequest(QueryAssistantMode.Find);

        var prompt = _sut.Build(request);

        prompt.Should().Contain("JSON object");
    }

    [Fact]
    public void Should_ListFields_When_SchemaHasFields()
    {
        var request = CreateRequest(QueryAssistantMode.Find);

        var prompt = _sut.Build(request);

        prompt.Should().Contain("- name: string");
    }

    private static QueryAssistantRequest CreateRequest(QueryAssistantMode mode) =>
        new()
        {
            Schema = new CollectionSchema
            {
                Collection = new CollectionRef(DatabaseName.Create("app"), CollectionName.Create("users")),
                Fields = [new SchemaField("name", "string"), new SchemaField("age", "int")],
            },
            Prompt = "find adults",
            Mode = mode,
        };
}
