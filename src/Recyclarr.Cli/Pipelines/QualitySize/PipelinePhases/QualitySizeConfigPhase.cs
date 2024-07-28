using Recyclarr.Cli.Pipelines.Generic;
using Recyclarr.Common.Extensions;
using Recyclarr.Config.Models;
using Recyclarr.TrashGuide.QualitySize;

namespace Recyclarr.Cli.Pipelines.QualitySize.PipelinePhases;

public class QualitySizeConfigPhase(ILogger log, IQualitySizeGuideService guide, IServiceConfiguration config)
    : IConfigPipelinePhase<QualitySizePipelineContext>
{
    public Task Execute(QualitySizePipelineContext context)
    {
        var configSizeData = config.QualityDefinition;
        if (configSizeData is null)
        {
            log.Debug("{Instance} has no quality definition", config.InstanceName);
            return Task.CompletedTask;
        }

        var guideSizeData = guide.GetQualitySizeData(config.ServiceType)
            .FirstOrDefault(x => x.Type.EqualsIgnoreCase(configSizeData.Type));

        if (guideSizeData == null)
        {
            context.ConfigError = $"The specified quality definition type does not exist: {configSizeData.Type}";
            return Task.CompletedTask;
        }

        AdjustPreferredRatio(configSizeData, guideSizeData);
        context.ConfigOutput = guideSizeData;
        return Task.CompletedTask;
    }

    private void AdjustPreferredRatio(QualityDefinitionConfig configSizeData, QualitySizeData guideSizeData)
    {
        if (configSizeData.PreferredRatio is null)
        {
            return;
        }

        log.Information("Using an explicit preferred ratio which will override values from the guide");

        // Fix an out of range ratio and warn the user
        if (configSizeData.PreferredRatio is < 0 or > 1)
        {
            var clampedRatio = Math.Clamp(configSizeData.PreferredRatio.Value, 0, 1);
            log.Warning("Your `preferred_ratio` of {CurrentRatio} is out of range. " +
                "It must be a decimal between 0.0 and 1.0. It has been clamped to {ClampedRatio}",
                configSizeData.PreferredRatio, clampedRatio);

            configSizeData.PreferredRatio = clampedRatio;
        }

        // Apply a calculated preferred size
        foreach (var quality in guideSizeData.Qualities)
        {
            quality.Preferred = quality.InterpolatedPreferred(configSizeData.PreferredRatio.Value);
        }
    }
}
