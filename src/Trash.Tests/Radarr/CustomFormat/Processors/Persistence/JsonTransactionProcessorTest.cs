using System.Collections.Generic;
using FluentAssertions;
using FluentAssertions.Json;
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
            const string radarrCfData = @"{
  'id': 1,
  'name': 'cf1',
  'specifications': [{
    'name': 'spec1',
    'fields': [{
      'name': 'value',
      'value': 'value1'
    }]
  }]
}";
            const string guideCfData = @"{
  'name': 'cf1',
  'specifications': [{
    'name': 'spec1',
    'new': 'valuenew',
    'fields': {
      'value': 'value2'
    }
  }]
}";
            var cacheEntry = id != null ? new TrashIdMapping {CustomFormatId = id.Value} : null;

            var guideCfs = new List<ProcessedCustomFormatData>
            {
                new()
                {
                    Name = guideCfName,
                    CacheEntry = cacheEntry,
                    Json = guideCfData
                }
            };

            var processor = new JsonTransactionProcessor();
            processor.Process(guideCfs, new[] {JObject.Parse(radarrCfData)});

            const string expectedJsonData = @"{
  'id': 1,
  'name': 'cf1',
  'specifications': [{
    'name': 'spec1',
    'new': 'valuenew',
    'fields': [{
      'name': 'value',
      'value': 'value2'
    }]
  }]
}";
            processor.ApiTransactions.Should().BeEquivalentTo(new List<CustomFormatTransaction>
            {
                new(ApiOperation.Update, JObject.Parse(expectedJsonData), guideCfs[0])
            }, op => op
                .Using<JToken>(ctx => ctx.Subject.Should().BeEquivalentTo(ctx.Expectation))
                .WhenTypeIs<JToken>());
        }

        [Test]
        public void Process_CreateUpdateNoChangeNoDelete_CorrectResult()
        {
            const string radarrCfData = @"[{
  'id': 1,
  'name': 'user_defined',
  'specifications': [{
    'name': 'spec1',
    'negate': false,
    'fields': [{
      'name': 'value',
      'value': 'value1'
    }]
  }]
}, {
  'id': 2,
  'name': 'updated',
  'specifications': [{
    'name': 'spec2',
    'negate': false,
    'fields': [{
      'name': 'value',
      'untouchable': 'field',
      'value': 'value1'
    }]
  }]
}, {
  'id': 3,
  'name': 'no_change',
  'specifications': [{
    'name': 'spec4',
    'negate': false,
    'fields': [{
      'name': 'value',
      'value': 'value1'
    }]
  }]
}]";
            var guideCfData = new[]
            {
                @"{
  'name': 'created',
  'specifications': [{
    'name': 'spec5',
    'fields': {
      'value': 'value2'
    }
  }]
}",
                @"{
  'name': 'updated_different_name',
  'specifications': [{
    'name': 'spec2',
    'negate': true,
    'new_spec_field': 'new_spec_value',
    'fields': {
      'value': 'value2',
      'new_field': 'new_value'
    }
  }, {
    'name': 'new_spec',
    'fields': {
      'value': 'value3'
    }
  }]
}",
                @"{
  'name': 'no_change',
  'specifications': [{
    'name': 'spec4',
    'negate': false,
    'fields': {
      'value': 'value1'
    }
  }]
}"
            };

            var radarrCfs = JsonConvert.DeserializeObject<List<JObject>>(radarrCfData);
            var guideCfs = new List<ProcessedCustomFormatData>
            {
                new() {Name = "created", Json = guideCfData[0]},
                new()
                {
                    Name = "updated_different_name",
                    Json = guideCfData[1],
                    CacheEntry = new TrashIdMapping {CustomFormatId = 2}
                },
                new() {Name = "no_change", Json = guideCfData[2]}
            };

            var processor = new JsonTransactionProcessor();
            processor.Process(guideCfs, radarrCfs);

            var expectedJson = new[]
            {
                @"{
  'name': 'created',
  'specifications': [{
    'name': 'spec5',
    'fields': [{
      'name': 'value',
      'value': 'value2'
    }]
  }]
}",
                @"{
  'id': 2,
  'name': 'updated_different_name',
  'specifications': [{
    'name': 'spec2',
    'negate': true,
    'new_spec_field': 'new_spec_value',
    'fields': [{
      'name': 'value',
      'untouchable': 'field',
      'value': 'value2',
      'new_field': 'new_value'
    }]
  }, {
    'name': 'new_spec',
    'fields': [{
      'name': 'value',
      'value': 'value3'
    }]
  }]
}",
                @"{
  'id': 3,
  'name': 'no_change',
  'specifications': [{
    'name': 'spec4',
    'negate': false,
    'fields': [{
      'name': 'value',
      'value': 'value1'
    }]
  }]
}"
            };

            processor.ApiTransactions.Should().BeEquivalentTo(new List<CustomFormatTransaction>
            {
                new(ApiOperation.Create, JObject.Parse(expectedJson[0]), guideCfs[0]),
                new(ApiOperation.Update, JObject.Parse(expectedJson[1]), guideCfs[1]),
                new(ApiOperation.NoChange, JObject.Parse(expectedJson[2]), guideCfs[2])
            }, op => op
                .Using<JToken>(ctx => ctx.Subject.Should().BeEquivalentTo(ctx.Expectation))
                .WhenTypeIs<JToken>());
        }
    }
}
