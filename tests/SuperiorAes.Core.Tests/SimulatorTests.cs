using System.Text;
using SuperiorAes.Core.Connections;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Core.Tests;

public sealed class SimulatorTests
{
    [Fact]
    public async Task ReturnsParseableStatusAndRoutes()
    {
        await using var connection = new SimulatedAesConnection(AesModel.Aes7788F);
        var output = new StringBuilder();
        connection.DataReceived += (_, args) => output.Append(args.Text);

        await connection.ConnectAsync();
        await connection.SendAsync(AesCommands.GetBytes(AesCommand.LocalStatus));
        await connection.SendAsync(AesCommands.GetBytes(AesCommand.RoutingTable));

        Assert.NotNull(AesParsers.ParseLocalStatus(output.ToString()));
        Assert.Equal(4, AesParsers.ParseRoutes(output.ToString()).Count);
    }

    [Fact]
    public async Task CompletesGuidedIdentityConversation()
    {
        await using var connection = new SimulatedAesConnection(AesModel.Aes7788F);
        await using var client = new AesProtocolClient(connection);
        var output = new StringBuilder();
        client.DataReceived += (_, args) => output.Append(args.Text);

        await client.ConnectAsync();
        await client.RunConversationAsync(
            AesCommand.ProgramIdCipher,
            ["BEEF", "A55A"],
            TimeSpan.FromMilliseconds(5));

        Assert.Contains("CPHR CODE", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("OK", output.ToString(), StringComparison.Ordinal);
    }
}
