﻿using System;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using Autofac;
using Autofac.Extras.AggregateService;
using CliFx;
using Serilog;
using Serilog.Core;
using Trash.Cache;
using Trash.Command;
using Trash.Config;
using Trash.Radarr.CustomFormat;
using Trash.Radarr.CustomFormat.Api;
using Trash.Radarr.CustomFormat.Guide;
using Trash.Radarr.CustomFormat.Processors;
using Trash.Radarr.CustomFormat.Processors.GuideSteps;
using Trash.Radarr.CustomFormat.Processors.PersistenceSteps;
using Trash.Radarr.QualityDefinition;
using Trash.Radarr.QualityDefinition.Api;
using Trash.Sonarr.Api;
using Trash.Sonarr.QualityDefinition;
using Trash.Sonarr.ReleaseProfile;
using YamlDotNet.Serialization;

namespace Trash
{
    public static class CompositionRoot
    {
        private static void SetupLogging(ContainerBuilder builder)
        {
            builder.RegisterType<LogJanitor>().As<ILogJanitor>();
            builder.RegisterType<LoggingLevelSwitch>().SingleInstance();
            builder.Register(c =>
                {
                    var logPath = Path.Combine(AppPaths.LogDirectory,
                        $"trash_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

                    const string consoleTemplate = "[{Level:u3}] {Message:lj}{NewLine}{Exception}";

                    return new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .WriteTo.Console(outputTemplate: consoleTemplate, levelSwitch: c.Resolve<LoggingLevelSwitch>())
                        .WriteTo.File(logPath)
                        .CreateLogger();
                })
                .As<ILogger>()
                .SingleInstance();
        }

        private static void SonarrRegistrations(ContainerBuilder builder)
        {
            builder.RegisterType<SonarrApi>().As<ISonarrApi>();

            // Release Profile Support
            builder.RegisterType<ReleaseProfileUpdater>();
            builder.RegisterType<ReleaseProfileGuideParser>().As<IReleaseProfileGuideParser>();

            // Quality Definition Support
            builder.RegisterType<SonarrQualityDefinitionUpdater>();
            builder.RegisterType<SonarrQualityDefinitionGuideParser>().As<ISonarrQualityDefinitionGuideParser>();
        }

        private static void RadarrRegistrations(ContainerBuilder builder)
        {
            // Services
            builder.RegisterType<QualityDefinitionService>().As<IQualityDefinitionService>();
            builder.RegisterType<CustomFormatService>().As<ICustomFormatService>();
            builder.RegisterType<QualityProfileService>().As<IQualityProfileService>();

            builder.Register(c =>
                {
                    var config = c.Resolve<IConfigurationProvider>().ActiveConfiguration;
                    return new ServerInfo(config.BaseUrl, config.ApiKey);
                })
                .As<IServerInfo>();

            // Quality Definition Support
            builder.RegisterType<RadarrQualityDefinitionUpdater>();
            builder.RegisterType<RadarrQualityDefinitionGuideParser>().As<IRadarrQualityDefinitionGuideParser>();

            // Custom Format Support
            builder.RegisterType<CustomFormatUpdater>().As<ICustomFormatUpdater>();
            builder.RegisterType<GithubCustomFormatJsonRequester>().As<IRadarrGuideService>();
            builder.RegisterType<CachePersister>().As<ICachePersister>();

            // Guide Processor
            builder.RegisterType<GuideProcessor>().As<IGuideProcessor>(); // todo: register as singleton to avoid parsing guide multiple times when using 2 or more instances in config
            builder.RegisterAggregateService<IGuideProcessorSteps>();
            builder.RegisterType<CustomFormatStep>().As<ICustomFormatStep>();
            builder.RegisterType<ConfigStep>().As<IConfigStep>();
            builder.RegisterType<QualityProfileStep>().As<IQualityProfileStep>();

            // Persistence Processor
            builder.RegisterType<PersistenceProcessor>().As<IPersistenceProcessor>();
            builder.RegisterAggregateService<IPersistenceProcessorSteps>();
            builder.RegisterType<JsonTransactionStep>().As<IJsonTransactionStep>();
            builder.RegisterType<CustomFormatApiPersistenceStep>().As<ICustomFormatApiPersistenceStep>();
            builder.RegisterType<QualityProfileApiPersistenceStep>().As<IQualityProfileApiPersistenceStep>();
        }

        private static void ConfigurationRegistrations(ContainerBuilder builder)
        {
            builder.RegisterType<ObjectFactory>()
                .As<IObjectFactory>();

            builder.RegisterGeneric(typeof(ConfigurationLoader<>))
                .As(typeof(IConfigurationLoader<>));

            builder.RegisterType<ConfigurationProvider>()
                .As<IConfigurationProvider>()
                .SingleInstance();

            // note: Do not allow consumers to resolve IServiceConfiguration directly; if this gets cached
            // they end up using the wrong configuration when multiple instances are used.
            // builder.Register(c => c.Resolve<IConfigurationProvider>().ActiveConfiguration)
            // .As<IServiceConfiguration>();
        }

        private static void CommandRegistrations(ContainerBuilder builder)
        {
            // Register all types deriving from CliFx's ICommand. These are all of our supported subcommands.
            builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
                .Where(t => t.IsAssignableTo(typeof(ICommand)));

            // Used to access the chosen command class. This is assigned from CliTypeActivator
            builder.RegisterType<ActiveServiceCommandProvider>()
                .As<IActiveServiceCommandProvider>()
                .SingleInstance();

            builder.Register(c => c.Resolve<IActiveServiceCommandProvider>().ActiveCommand)
                .As<IServiceCommand>();
        }

        public static IContainer Setup()
        {
            return Setup(new ContainerBuilder());
        }

        public static IContainer Setup(ContainerBuilder builder)
        {
            builder.RegisterType<FileSystem>().As<IFileSystem>();
            builder.RegisterType<ServiceCache>().As<IServiceCache>();
            builder.RegisterType<CacheStoragePath>().As<ICacheStoragePath>();

            ConfigurationRegistrations(builder);
            CommandRegistrations(builder);

            SetupLogging(builder);
            SonarrRegistrations(builder);
            RadarrRegistrations(builder);

            // builder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource());
            return builder.Build();
        }
    }
}
