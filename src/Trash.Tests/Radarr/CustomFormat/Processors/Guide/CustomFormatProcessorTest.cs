using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Trash.Radarr;
using Trash.Radarr.CustomFormat.Guide;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;
using Trash.Radarr.CustomFormat.Processors.Guide;

namespace Trash.Tests.Radarr.CustomFormat.Processors.Guide
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class CustomFormatProcessorTest
    {
        private class Context
        {
            public List<CustomFormatData> TestGuideData { get; } = new()
            {
                new CustomFormatData
                {
                    Score = 100,
                    Json = JsonConvert.SerializeObject(new
                    {
                        trash_id = "id1",
                        name = "name1"
                    }, Formatting.Indented)
                },
                new CustomFormatData
                {
                    Score = 200,
                    Json = JsonConvert.SerializeObject(new
                    {
                        trash_id = "id2",
                        name = "name2"
                    }, Formatting.Indented)
                },
                new CustomFormatData
                {
                    Json = JsonConvert.SerializeObject(new
                    {
                        trash_id = "id3",
                        name = "name3"
                    }, Formatting.Indented)
                }
            };
        }

        [TestCase("name1")]
        [TestCase("naME1")]
        [TestCase("DifferentName")]
        public void Process_CfInCacheWithVariousNames_CacheEntrySet(string cacheCfName)
        {
            var ctx = new Context();
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1"}}
            };

            var testCache = new CustomFormatCache
            {
                TrashIdMappings = new List<TrashIdMapping>
                {
                    new() {TrashId = "id1", CustomFormatName = cacheCfName}
                }
            };

            var processor = new CustomFormatProcessor();
            processor.Process(ctx.TestGuideData, testConfig, testCache);

            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
            {
                new()
                {
                    Json = JsonConvert.SerializeObject(new {name = "name1"}, Formatting.Indented),
                    Name = "name1",
                    Score = 100,
                    TrashId = "id1",
                    CacheEntry = testCache.TrashIdMappings[0]
                }
            }, op => op
                .Using<JToken>(jctx => jctx.Subject.Should().BeEquivalentTo(jctx.Expectation))
                .WhenTypeIs<JToken>());
        }

        [Test]
        public void Process_CfInCacheDifferentId_CacheEntryNotSet()
        {
            var ctx = new Context();
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1"}}
            };

            var testCache = new CustomFormatCache
            {
                TrashIdMappings = new List<TrashIdMapping>
                {
                    new() {TrashId = "id1000", CustomFormatName = "name1"}
                }
            };

            var processor = new CustomFormatProcessor();
            processor.Process(ctx.TestGuideData, testConfig, testCache);

            processor.DeletedCustomFormatsInCache.Should().BeEquivalentTo(new TrashIdMapping
            {
                TrashId = "id1000",
                CustomFormatName = "name1",
                CustomFormatId = 0
            });

            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
            {
                new()
                {
                    Json = JsonConvert.SerializeObject(new {name = "name1"}, Formatting.Indented),
                    Name = "name1",
                    Score = 100,
                    TrashId = "id1",
                    CacheEntry = null
                }
            }, op => op
                .Using<JToken>(jctx => jctx.Subject.Should().BeEquivalentTo(jctx.Expectation))
                .WhenTypeIs<JToken>());
        }

        [Test]
        public void Process_CfNamesInConfigDifferOnlyByCase_AreTreatedAsOne()
        {
            var ctx = new Context();
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1", "NAME1"}}
            };

            var processor = new CustomFormatProcessor();
            processor.Process(ctx.TestGuideData, testConfig, new CustomFormatCache());

            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
            {
                new()
                {
                    Json = JsonConvert.SerializeObject(new {name = "name1"}, Formatting.Indented),
                    Name = "name1",
                    Score = 100,
                    TrashId = "id1"
                }
            }, op => op
                .Using<JToken>(jctx => jctx.Subject.Should().BeEquivalentTo(jctx.Expectation))
                .WhenTypeIs<JToken>());
        }

        [Test]
        public void Process_CfsNotInConfig_AreSkipped()
        {
            var ctx = new Context();
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1", "name3"}}
            };

            var processor = new CustomFormatProcessor();
            processor.Process(ctx.TestGuideData, testConfig, new CustomFormatCache());

            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
            {
                new()
                {
                    Json = JsonConvert.SerializeObject(new {name = "name1"}, Formatting.Indented),
                    Name = "name1",
                    Score = 100,
                    TrashId = "id1"
                },
                new()
                {
                    Json = JsonConvert.SerializeObject(new {name = "name3"}, Formatting.Indented),
                    Name = "name3",
                    Score = null,
                    TrashId = "id3"
                }
            }, op => op
                .Using<JToken>(jctx => jctx.Subject.Should().BeEquivalentTo(jctx.Expectation))
                .WhenTypeIs<JToken>());
        }

        [Test]
        public void Process_ConfigCfsSpreadAcrossDifferentSections_AllAreProcessed()
        {
            var ctx = new Context();
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1", "name3"}},
                new() {Names = new List<string> {"name2"}}
            };

            var processor = new CustomFormatProcessor();
            processor.Process(ctx.TestGuideData, testConfig, new CustomFormatCache());

            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
            {
                new()
                {
                    Json = JsonConvert.SerializeObject(new {name = "name1"}, Formatting.Indented),
                    Name = "name1",
                    Score = 100,
                    TrashId = "id1"
                },
                new()
                {
                    Json = JsonConvert.SerializeObject(new {name = "name2"}, Formatting.Indented),
                    Name = "name2",
                    Score = 200,
                    TrashId = "id2"
                },
                new()
                {
                    Json = JsonConvert.SerializeObject(new {name = "name3"}, Formatting.Indented),
                    Name = "name3",
                    Score = null,
                    TrashId = "id3"
                }
            }, op => op
                .Using<JToken>(jctx => jctx.Subject.Should().BeEquivalentTo(jctx.Expectation))
                .WhenTypeIs<JToken>());
        }

        [Test]
        public void Process_ConfigUsesNonExistentCf_Skipped()
        {
            var ctx = new Context();
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"doesnt_exist"}}
            };

            var processor = new CustomFormatProcessor();
            processor.Process(ctx.TestGuideData, testConfig, new CustomFormatCache());

            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should().BeEmpty();
        }
    }
}
