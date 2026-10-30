using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using MongoTui.Domain.Users;
using MongoTui.Infrastructure.Mongo.Driver;

namespace MongoTui.Infrastructure.Tests.Mongo.Driver;

public sealed class MongoUserErrorMapperTests
{
    [Theory]
    [InlineData(13, "User already exists.", UserManagementError.InsufficientPrivileges)]
    [InlineData(51003, "Role not found.", UserManagementError.AlreadyExists)]
    [InlineData(11, "User already exists.", UserManagementError.NotFound)]
    [InlineData(0, "User ALREADY EXISTS.", UserManagementError.AlreadyExists)]
    [InlineData(0, "ROLE not found.", UserManagementError.RoleGrantFailed)]
    [InlineData(0, "User NOT FOUND.", UserManagementError.NotFound)]
    [InlineData(0, "Role update rejected.", UserManagementError.CommandFailed)]
    [InlineData(0, "Unrecognized server failure.", UserManagementError.CommandFailed)]
    public void Should_ClassifyFailureAndPreserveCause_WhenDriverCommandFails(int code, string message, UserManagementError expected)
    {
        var connection = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));
        var command = new BsonDocument { { "createUser", "alice" }, { "pwd", "secret" } };
        var response = new BsonDocument { { "ok", 0 }, { "code", code }, { "errmsg", message } };
        var cause = new MongoCommandException(connection, message, command, response);

        var error = MongoUserErrorMapper.Map(cause);

        error.Code.Should().Be(expected);
        error.InnerException.Should().BeSameAs(cause);
        error.Message.Should().NotContain("secret").And.NotContain("alice");
    }
}
