using Autofac;
using Autofac.Extras.AggregateService;
using Recyclarr.Cli.Pipelines.QualityProfile.PipelinePhases;
using Recyclarr.ServarrApi.Services;

namespace Recyclarr.Cli.Pipelines.QualityProfile;

public class QualityProfileAutofacModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<QualityProfileStatCalculator>();

        builder.RegisterAggregateService<IQualityProfilePipelinePhases>();
        builder.RegisterType<QualityProfileConfigPhase>();
        builder.RegisterType<QualityProfileApiFetchPhase>();
        builder.RegisterType<QualityProfileTransactionPhase>();
        builder.RegisterType<QualityProfilePreviewPhase>();
        builder.RegisterType<QualityProfileApiPersistencePhase>();
        builder.RegisterType<QualityProfileNoticePhase>();
    }
}
