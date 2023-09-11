using System.IO.Abstractions;
using JetBrains.Annotations;
using Recyclarr.TrashLib.Config.Parsing.ErrorHandling;
using Recyclarr.Yaml;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Recyclarr.TrashLib.Config.Parsing;

[UsedImplicitly]
public class ConfigParser
{
    private readonly ILogger _log;
    private readonly IDeserializer _deserializer;

    public ConfigParser(ILogger log, IYamlSerializerFactory yamlFactory)
    {
        _log = log;
        _deserializer = yamlFactory.CreateDeserializer();
    }

    public T? Load<T>(IFileInfo file) where T : class
    {
        _log.Debug("Loading config file: {File}", file);
        return Load<T>(file.OpenText);
    }

    public T? Load<T>(string yaml) where T : class
    {
        _log.Debug("Loading config from string data");
        return Load<T>(() => new StringReader(yaml));
    }

    public T? Load<T>(Func<TextReader> streamFactory) where T : class
    {
        try
        {
            using var stream = streamFactory();
            return _deserializer.Deserialize<T?>(stream);
        }
        catch (FeatureRemovalException e)
        {
            _log.Error(e, "Unsupported feature");
        }
        catch (YamlException e)
        {
            _log.Debug(e, "Exception while parsing config file");

            var line = e.Start.Line;
            switch (e.InnerException)
            {
                case InvalidCastException:
                    _log.Error("Incompatible value assigned/used at line {Line}: {Msg}", line,
                        e.InnerException.Message);
                    break;

                default:
                    var msg = ConfigContextualMessages.GetContextualErrorFromException(e) ??
                        e.InnerException?.Message ?? e.Message;
                    _log.Error("Exception at line {Line}: {Msg}", line, msg);
                    break;
            }
        }

        _log.Error("Due to previous exception, this config will be skipped");
        return default;
    }
}