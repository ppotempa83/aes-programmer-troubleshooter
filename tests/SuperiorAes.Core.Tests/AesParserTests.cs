using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Core.Tests;

public sealed class AesParserTests
{
    private const string Sample = """
        SUB [7.8] 7788F
        ID#:1A2B (C) 2026 AES
        RT1:AA11 LEVEL:002
        STAT:084 NETCON:6
        3.3C10,L:02,N:4,Q:02
        2.BB12,L:01,N:1,Q:03
        1.AA11,L:01,N:0,Q:03
        Zx12,Z1-8:0100-1000
        """;

    [Fact]
    public void ParsesLocalStatus()
    {
        var status = AesParsers.ParseLocalStatus(Sample);

        Assert.NotNull(status);
        Assert.Equal("7788F", status.Model);
        Assert.Equal("7.8", status.Firmware);
        Assert.Equal("1A2B", status.SubscriberId);
        Assert.Equal("AA11", status.RouteOne);
        Assert.Equal(2, status.Level);
        Assert.Equal("084", status.StatCode);
        Assert.Equal(6, status.NetCon);
    }

    [Fact]
    public void ParsesRoutesInPreferenceOrder()
    {
        var routes = AesParsers.ParseRoutes(Sample);

        Assert.Equal(3, routes.Count);
        Assert.Equal("AA11", routes[0].Id);
        Assert.Equal("03", routes[0].Quality);
        Assert.Equal("Best", routes[0].QualityLabel);
        Assert.Equal("3C10", routes[2].Id);
    }

    [Fact]
    public void ParsesAllEightZoneStates()
    {
        var zones = AesParsers.ParseZones(Sample);

        Assert.Equal(8, zones.Count);
        Assert.Equal('1', zones[1].State);
        Assert.Equal('1', zones[4].State);
        Assert.Equal("Alarm / Fault", zones[4].Label);
    }

    [Fact]
    public void DecodesAdditiveStatCode()
    {
        var faults = AesParsers.DecodeStat("084");

        Assert.Contains(faults, fault => fault.Code == "080");
        Assert.Contains(faults, fault => fault.Code == "004");
        Assert.Equal(2, faults.Count);
    }

    [Fact]
    public void UsesMostRecentCompleteStatus()
    {
        var latest = AesParsers.ParseLocalStatus(
            Sample + """

                SUB [7.9] 7788F
                ID#:C0DE (C) 2026 AES
                RT1:0ACE LEVEL:001
                STAT:000 NETCON:2
                """);

        Assert.NotNull(latest);
        Assert.Equal("C0DE", latest.SubscriberId);
        Assert.Equal("0ACE", latest.RouteOne);
        Assert.Equal(2, latest.NetCon);
    }
}
