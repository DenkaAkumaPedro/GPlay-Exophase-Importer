using System.Reflection;
using GPlayExophaseImporter.Exophase;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Playnite.SDK.Data;

namespace GPlayExophaseImporter.Tests
{
    public class PlaytimeTests
    {
        [Theory]
        [InlineData("117h 55m", 424500)]
        [InlineData("7m", 420)]
        [InlineData("24h", 86400)]
        [InlineData("1h 30m", 5400)]
        [InlineData("0h", 0)]
        [InlineData("", 0)]
        public void ParsePlaytimeString_ConvertsHumanReadableToSeconds(string text, ulong expected)
        {
            Assert.Equal(expected, ExophaseClient.ParsePlaytimeString(text));
        }

        [Fact]
        public void ParsePlaytimeString_NullReturnsZero()
        {
            Assert.Equal(0UL, ExophaseClient.ParsePlaytimeString(null));
        }

        [Fact]
        public void ComputePlaytimeSeconds_PrefersStructuredUnits()
        {
            var game = new ExophaseGame
            {
                Playtime = "99h", // must be ignored
                PlaytimeUnits = new ExophasePlaytimeUnits { Hours = 117, Minutes = 55 }
            };

            Assert.Equal(424500UL, ExophaseClient.ComputePlaytimeSeconds(game));
        }

        [Fact]
        public void ComputePlaytimeSeconds_FallsBackToPlaytimeString()
        {
            var game = new ExophaseGame { Playtime = "7m" };
            Assert.Equal(420UL, ExophaseClient.ComputePlaytimeSeconds(game));
        }

        [Fact]
        public void ComputePlaytimeSeconds_ZeroUnitsReturnsZero()
        {
            var game = new ExophaseGame
            {
                PlaytimeUnits = new ExophasePlaytimeUnits { Hours = 0, Minutes = 0 }
            };

            Assert.Equal(0UL, ExophaseClient.ComputePlaytimeSeconds(game));
        }

        [Fact]
        public void ComputePlaytimeSeconds_RandomTextReturnsZero()
        {
            var game = new ExophaseGame { Playtime = "not a duration" };
            Assert.Equal(0UL, ExophaseClient.ComputePlaytimeSeconds(game));
        }
    }

    public class GPlayDetectionTests
    {
        [Fact]
        public void IsGPlayGame_MatchesAndroidSlug()
        {
            var game = GameWithPlatforms(new ExophasePlatform { Name = "PC", Slug = "pc" },
                                         new ExophasePlatform { Name = "Android", Slug = "android" });
            Assert.True(ExophaseClient.IsGPlayGame(game));
        }

        [Fact]
        public void IsGPlayGame_MatchesGooglePlayDisplayName()
        {
            var game = GameWithPlatforms(new ExophasePlatform { Name = "Google Play Games", Slug = "googleplay" });
            Assert.True(ExophaseClient.IsGPlayGame(game));
        }

        [Fact]
        public void IsGPlayGame_MatchesAndroidDisplayName()
        {
            var game = GameWithPlatforms(new ExophasePlatform { Name = "Android", Slug = "x" });
            Assert.True(ExophaseClient.IsGPlayGame(game));
        }

        [Fact]
        public void IsGPlayGame_RejectsOtherPlatforms()
        {
            var game = GameWithPlatforms(new ExophasePlatform { Name = "PC", Slug = "pc" },
                                         new ExophasePlatform { Name = "PlayStation 5", Slug = "ps5" });
            Assert.False(ExophaseClient.IsGPlayGame(game));
        }

        [Fact]
        public void IsGPlayGame_NullPlatformsReturnsFalse()
        {
            Assert.False(ExophaseClient.IsGPlayGame(new ExophaseGame()));
        }

        [Fact]
        public void IsGPlayGame_NullGameReturnsFalse()
        {
            Assert.False(ExophaseClient.IsGPlayGame(null));
        }

        [Fact]
        public void IsGPlayGame_SkipsNullPlatformEntries()
        {
            var game = GameWithPlatforms(null, null);
            Assert.False(ExophaseClient.IsGPlayGame(game));
        }

        private static ExophaseGame GameWithPlatforms(params ExophasePlatform[] platforms)
        {
            return new ExophaseGame
            {
                Meta = new ExophaseMeta { Platforms = new List<ExophasePlatform>(platforms) }
            };
        }
    }

    public class ProfileUrlTests
    {
        [Fact]
        public void BuildProfileUrl_FormatsBareUsername()
        {
            Assert.Equal("https://www.exophase.com/user/player1/", ExophaseClient.BuildProfileUrl("player1"));
        }

        [Fact]
        public void BuildProfileUrl_EscapesUsername()
        {
            Assert.Equal("https://www.exophase.com/user/some%20user/", ExophaseClient.BuildProfileUrl("some user"));
        }

        [Fact]
        public void BuildProfileUrl_KeepsCompleteUrl()
        {
            const string url = "https://www.exophase.com/user/player1/";
            Assert.Equal(url, ExophaseClient.BuildProfileUrl(url));
        }

        [Fact]
        public void BuildProfileUrl_AddsSchemeToBareDomain()
        {
            Assert.Equal("https://exophase.com/user/player1/", ExophaseClient.BuildProfileUrl("exophase.com/user/player1/"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void BuildProfileUrl_EmptyInputThrows(string input)
        {
            Assert.Throws<ExophaseException>(() => ExophaseClient.BuildProfileUrl(input));
        }

        [Fact]
        public void BuildProfileUrl_NullInputThrows()
        {
            Assert.Throws<ExophaseException>(() => ExophaseClient.BuildProfileUrl(null));
        }
    }

    public class ChallengeDetectionTests
    {
        [Theory]
        [InlineData("Just a moment...")]
        [InlineData("cf-browser-verification")]
        [InlineData("Checking your browser before accessing")]
        [InlineData("__cf_chl_jschl_tk")]
        public void LooksLikeChallenge_DetectsCloudflareMarkers(string source)
        {
            Assert.True(ExophaseClient.LooksLikeChallenge(source));
        }

        [Fact]
        public void LooksLikeChallenge_NormalContentIsFalse()
        {
            Assert.False(ExophaseClient.LooksLikeChallenge("<html><body>games</body></html>"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void LooksLikeChallenge_EmptyOrNullIsTrue(string source)
        {
            Assert.True(ExophaseClient.LooksLikeChallenge(source));
        }
    }

    public class ExtractJsonTests
    {
        [Fact]
        public void ExtractJson_ReturnsInnerObject()
        {
            const string page = "/*!/*.jsonp)(*&{\"success\":true}{\"extra\":1}";
            var json = ExophaseClient.ExtractJson(page);
            Assert.Contains("\"success\":true", json);
            Assert.Contains("\"extra\":1", json);
        }

        [Fact]
        public void ExtractJson_NoBracesReturnsNull()
        {
            Assert.Null(ExophaseClient.ExtractJson("plain text without json"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ExtractJson_EmptyOrNullReturnsNull(string input)
        {
            Assert.Null(ExophaseClient.ExtractJson(input));
        }
    }

    public class EntryKeyTests
    {
        [Fact]
        public void Key_UsesExophaseIdWhenPresent()
        {
            var entry = new GPlayGameEntry { ExophaseId = 42, Title = "Some Game" };
            Assert.Equal("exophase:42", entry.Key);
        }

        [Fact]
        public void Key_FallsBackToTrimmedTitle()
        {
            var entry = new GPlayGameEntry { ExophaseId = 0, Title = "Some Game" };
            Assert.Equal("exophase:some game", entry.Key);
        }
    }

    public class DeserializationTests
    {
        // Playnite's own Serialization.FromJson is backed by a serializer the Playnite
        // app injects at startup, so it can't run inside a plain test host. The plugin's
        // DTOs describe the real Exophase API using Playnite's SerializationPropertyName
        // attribute, so we exercise the same mapping with a resolver that honours it.
        private sealed class PlayniteContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                var attribute = member.GetCustomAttribute<SerializationPropertyNameAttribute>();
                if (attribute != null)
                {
                    property.PropertyName = attribute.PropertyName;
                }
                return property;
            }
        }

        private static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(
            json, new JsonSerializerSettings { ContractResolver = new PlayniteContractResolver() });

        [Fact]
        public void FromJson_MapsFullApiResponse()
        {
            const string json = @"
{
  ""success"": true,
  ""games"": [
    {
      ""master_id"": 123,
      ""playtime"": ""2h 30m"",
      ""playtimeUnits"": { ""hours"": 2, ""minutes"": 30 },
      ""lastplayed_utc"": 1726000000,
      ""percent"": 42.5,
      ""meta"": {
        ""title"": ""Google Play Game"",
        ""platforms"": [ { ""name"": ""Android"", ""slug"": ""android"" } ],
        ""canonical_url"": ""/game/google-play-game/"",
        ""image"": ""https://example.com/img.png""
      }
    }
  ]
}";

            var response = Deserialize<ExophaseGamesResponse>(json);

            Assert.True(response.Success);
            var game = Assert.Single(response.Games);
            Assert.Equal(123, game.Id);
            Assert.Equal("2h 30m", game.Playtime);
            Assert.Equal(2, game.PlaytimeUnits.Hours);
            Assert.Equal(30, game.PlaytimeUnits.Minutes);
            Assert.Equal(1726000000, game.LastPlayedUtc);
            Assert.Equal("Google Play Game", game.Meta.Title);
            var platform = Assert.Single(game.Meta.Platforms);
            Assert.Equal("android", platform.Slug);
            Assert.Equal("/game/google-play-game/", game.Meta.CanonicalUrl);
        }

        [Fact]
        public void FromJson_MissingOptionalFieldsAreSafe()
        {
            const string json = @"{ ""success"": true, ""games"": [ { ""master_id"": 1 } ] }";

            var response = Deserialize<ExophaseGamesResponse>(json);
            var game = Assert.Single(response.Games);
            Assert.Equal(1, game.Id);
            Assert.Null(game.Playtime);
            Assert.Null(game.PlaytimeUnits);
            Assert.Null(game.Meta);
        }
    }
}