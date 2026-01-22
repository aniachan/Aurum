using System;

namespace Aurum.IntegrationTests;

public class MockPluginInterface
{
    public void SavePluginConfig(object config) { }
}

public static class MockPlugin
{
    public static MockPluginInterface PluginInterface { get; } = new MockPluginInterface();
}
