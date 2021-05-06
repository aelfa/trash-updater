using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Trash.Radarr.CustomFormat;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;
using Trash.Radarr.CustomFormat.Processors.Persistence;

/* Sample Custom Format response from Radarr API
{
  "id": 1,
  "name": "test",
  "includeCustomFormatWhenRenaming": false,
  "specifications": [
    {
      "name": "asdf",
      "implementation": "ReleaseTitleSpecification",
      "implementationName": "Release Title",
      "infoLink": "https://wiki.servarr.com/Radarr_Settings#Custom_Formats_2",
      "negate": false,
      "required": false,
      "fields": [
        {
          "order": 0,
          "name": "value",
          "label": "Regular Expression",
          "value": "asdf",
          "type": "textbox",
          "advanced": false
        }
      ]
    }
  ]
}
*/

namespace Trash.Tests.Radarr.CustomFormat.Processors.Persistence
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class JsonTransactionProcessorTest
    {
        [TestCase(1, "cf2")]
        [TestCase(2, "cf1")]
        [TestCase(null, "cf1")]
        public void Process_UpdateUsingIdOrName_CorrectResult(int? id, string guideCfName)
        {
            var radarrCfs = new List<JObject>
            {
                JObject.FromObject(new
                {
                    id = 1,
                    name = "cf1",
                    specifications = new[]
                    {
                        new
                        {
                            name = "spec1",
                            implementation = "ReleaseTitleSpec",
                            fields = new[]
                            {
                                new
                                {
                                    name = "value",
                                    value = "value1"
                                }
                            }
                        }
                    }
                })
            };

            var cacheEntry = id != null ? new TrashIdMapping {CustomFormatId = id.Value} : null;

            var guideCfs = new List<ProcessedCustomFormatData>
            {
                new()
                {
                    Name = guideCfName,
                    CacheEntry = cacheEntry,
                    Json = JsonConvert.SerializeObject(new
                    {
                        id = 1,
                        name = "cf1",
                        specifications = new[]
                        {
                            new
                            {
                                name = "spec1",
                                implementation = "ReleaseTitleSpec2",
                                fields = new
                                {
                                    value = "value2"
                                }
                            }
                        }
                    })
                }
            };

            var processor = new JsonTransactionProcessor();
            processor.Process(guideCfs, radarrCfs);

            var expectedJson = JObject.FromObject(new
            {
                id = 1,
                name = "cf2",
                specifications = new[]
                {
                    new
                    {
                        name = "spec1",
                        implementation = "ReleaseTitleSpec2",
                        fields = new[]
                        {
                            new
                            {
                                name = "value",
                                value = "value2"
                            }
                        }
                    }
                }
            });

            processor.ApiTransactions.Should().BeEquivalentTo(new List<CustomFormatTransaction>
            {
                new(ApiOperation.Update, expectedJson, guideCfs[0])
            });
        }

        [Test]
        public void Process_Create_CorrectResult()
        {
            const string radarrCfData = @"
{
  'id': 1,
  'name': 'cf1',
  'specifications': [{
    'name': 'spec1',
    'negate': false,
    'fields': [{
      'name': 'value',
      'value': 'value1'
    }]
  }]
}";
            const string guideCfData = @"
{
  'name': 'cf2',
  'specifications': [{
    'name': 'spec2',
    'fields': {
      'value': 'value2'
    }
  }]
}";
            var radarrCfs = new List<JObject> {JObject.Parse(radarrCfData)};

            var guideCfs = new List<ProcessedCustomFormatData>
            {
                new() {Name = "cf2", Json = guideCfData}
            };

            var processor = new JsonTransactionProcessor();
            processor.Process(guideCfs, radarrCfs);

            const string expectedJson = @"
{
  'name': 'cf2',
  'specifications': [{
    'name': 'spec2',
    'fields': [{
      'name': 'value',
      'value': 'value2'
    }]
  }]
}";

            processor.ApiTransactions.Should().BeEquivalentTo(new List<CustomFormatTransaction>
            {
                new(ApiOperation.Create, JObject.Parse(expectedJson), guideCfs[0])
            });
        }
    }
}
