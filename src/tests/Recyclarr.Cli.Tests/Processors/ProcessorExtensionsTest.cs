using NSubstitute.ReturnsExtensions;
using Recyclarr.Cli.Console.Settings;
using Recyclarr.Cli.Processors;
using Recyclarr.TrashLib.Config;
using Recyclarr.TrashLib.Config.Services;

namespace Recyclarr.Cli.Tests.Processors;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ProcessorExtensionsTest
{
    [Test]
    public void Filter_invalid_instances()
    {
        var configs = new[]
        {
            new RadarrConfiguration
            {
                InstanceName = "valid_NAME" // Comparison should be case-insensitive
            }
        };

        var settings = Substitute.For<ISyncSettings>();
        settings.Instances.Returns(new[] {"valid_name", "invalid_name"});

        var invalidInstanceNames = settings.GetInvalidInstanceNames(configs);

        invalidInstanceNames.Should().BeEquivalentTo("invalid_name");
    }

    [Test]
    public void Filter_invalid_instances_when_null()
    {
        var configs = new[]
        {
            new RadarrConfiguration
            {
                InstanceName = "valid_NAME" // Comparison should be case-insensitive
            }
        };

        var settings = Substitute.For<ISyncSettings>();
        settings.Instances.ReturnsNull();

        var invalidInstanceNames = settings.GetInvalidInstanceNames(configs);

        invalidInstanceNames.Should().BeEmpty();
    }

    [Test]
    public void Get_configs_matching_service_type_and_instance_name()
    {
        var configs = new IServiceConfiguration[]
        {
            new RadarrConfiguration {InstanceName = "radarr1"},
            new RadarrConfiguration {InstanceName = "radarr2"},
            new RadarrConfiguration {InstanceName = "radarr3"},
            new RadarrConfiguration {InstanceName = "radarr4"},
            new SonarrConfiguration {InstanceName = "sonarr1"},
            new SonarrConfiguration {InstanceName = "sonarr2"},
            new SonarrConfiguration {InstanceName = "sonarr3"},
            new SonarrConfiguration {InstanceName = "sonarr4"}
        };

        var settings = Substitute.For<ISyncSettings>();
        settings.Service.Returns(SupportedServices.Radarr);
        settings.Instances.Returns(new[] {"radarr2", "radarr4", "radarr5", "sonarr2"});

        var result = configs.GetConfigsBasedOnSettings(settings);

        result.Select(x => x.InstanceName).Should().BeEquivalentTo("radarr2", "radarr4");
    }

    [Test]
    public void Get_configs_based_on_settings_with_empty_instances()
    {
        var configs = new IServiceConfiguration[]
        {
            new RadarrConfiguration {InstanceName = "radarr1"},
            new SonarrConfiguration {InstanceName = "sonarr1"}
        };

        var settings = Substitute.For<ISyncSettings>();
        settings.Instances.Returns(Array.Empty<string>());

        var result = configs.GetConfigsBasedOnSettings(settings);

        result.Select(x => x.InstanceName).Should().BeEquivalentTo("radarr1", "sonarr1");
    }

    [Test]
    public void Get_split_instance_names()
    {
        var configs = new IServiceConfiguration[]
        {
            new RadarrConfiguration {InstanceName = "radarr1", BaseUrl = new Uri("http://radarr1")},
            new RadarrConfiguration {InstanceName = "radarr2", BaseUrl = new Uri("http://radarr1")},
            new RadarrConfiguration {InstanceName = "radarr3", BaseUrl = new Uri("http://radarr3")},
            new RadarrConfiguration {InstanceName = "radarr4", BaseUrl = new Uri("http://radarr4")},
            new SonarrConfiguration {InstanceName = "sonarr1", BaseUrl = new Uri("http://sonarr1")},
            new SonarrConfiguration {InstanceName = "sonarr2", BaseUrl = new Uri("http://sonarr2")},
            new SonarrConfiguration {InstanceName = "sonarr3", BaseUrl = new Uri("http://sonarr2")},
            new SonarrConfiguration {InstanceName = "sonarr4", BaseUrl = new Uri("http://sonarr4")}
        };

        var result = configs.GetSplitInstances();

        result.Should().BeEquivalentTo("radarr1", "radarr2", "sonarr2", "sonarr3");
    }
}
