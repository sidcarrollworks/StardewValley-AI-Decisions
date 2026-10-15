using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Entirely synthetic evidence. "Canon" here tests source standing, not Stardew content.</summary>
public sealed class LivesHistoryPackTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static HistoryPack Synthetic() => new(1,
        new[]
        {
            new HistorySource("source-z", "synthetic:Ann/account", HistorySourceKind.Canon),
            new HistorySource("source-a", "synthetic:Ann/present-reading", HistorySourceKind.Canon),
            new HistorySource("source-editor", "synthetic:editor/proposed-link", HistorySourceKind.Authored),
            new HistorySource("source-product", "synthetic:product/commitment", HistorySourceKind.ProductDecision),
        },
        new[] { new HistoryFact("fact-shared", HistoryKind.Practice, new[] { "source-z" }) },
        new[]
        {
            new HistoryAccount("account-ann", "fact-shared", "Ann", "I mend things at the workshop.",
                KnowledgeKind.Lived, new[] { "source-z" }, new[]
                {
                    new HistoryCue("place:Workshop", "source-z"), new HistoryCue("activity:Mending"),
                }),
        },
        new[]
        {
            new HistoryReading("reading-ann", "account-ann", new MemoryObject(MemoryObjectKind.Activity, "Mending"),
                ReadingStance.Warm, "I still enjoy mending things.", ReadingOrigin.CanonStated, AdmissionRule.R2,
                new HistoryReview(ReviewStatus.Reviewed, "Synthetic reviewer", "2026-10-15"),
                new[] { "source-z", "source-a" }, new[] { new HistoryCue("activity:Mending", "source-a") }),
        });

    private static HistoryPack Reading(HistoryPack pack, Func<HistoryReading, HistoryReading> change)
        => pack with { Readings = new[] { change(pack.Readings[0]) } };
    private static string Encode(HistoryPack pack) => JsonSerializer.Serialize(pack, Json);
    private static void Invalid(HistoryPack pack) => Assert.Throws<InvalidDataException>(() => pack.CopyValidated());
    private static void InvalidJson(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(Encode(Synthetic()))!.AsObject();
        change(root);
        Assert.Throws<InvalidDataException>(() => HistoryPack.Read(root.ToJsonString()));
    }

    [Fact]
    public void RoundTripRetainsSeparateFactsAccountsAndPrivateReadings()
    {
        HistoryPack pack = Synthetic();
        var bob = new HistoryAccount("account-bob", "fact-shared", "Bob", "Ann says she mends things there.",
            KnowledgeKind.Told, new[] { "source-z" }, Array.Empty<HistoryCue>(), "Ann");
        var bobsReading = pack.Readings[0] with
        {
            Id = "reading-bob", AccountId = bob.Id, Stance = ReadingStance.None,
            Text = "I have only heard about her workshop.",
        };
        pack = pack with { Accounts = pack.Accounts.Append(bob).ToArray(), Readings = pack.Readings.Append(bobsReading).ToArray() };

        HistoryPack copy = HistoryPack.Read(Encode(pack));

        Assert.Single(copy.Facts);
        Assert.Equal(2, copy.Accounts.Count);
        Assert.Equal("Ann", copy.Accounts.Single(a => a.Id == copy.Readings.Single(r => r.Id == "reading-ann").AccountId).Holder);
        Assert.Equal(KnowledgeKind.Told, copy.Accounts.Single(a => a.Holder == "Bob").How);
        Assert.Equal("Ann", copy.Accounts.Single(a => a.Holder == "Bob").Teller);
        Assert.Equal(ReadingStance.None, copy.Readings.Single(r => r.Id == "reading-bob").Stance);
    }

    [Fact]
    public void CopyDetachesAllCollectionsAndSortsOrdinally()
    {
        HistoryPack pack = Synthetic();
        HistoryPack copy = pack.CopyValidated();
        ((HistorySource[])pack.Sources)[0] = pack.Sources[0] with { Key = "changed" };
        ((string[])pack.Facts[0].Sources)[0] = "changed";
        ((string[])pack.Accounts[0].Sources)[0] = "changed";
        ((HistoryCue[])pack.Accounts[0].Cues)[0] = new HistoryCue("changed");
        ((string[])pack.Readings[0].Sources)[0] = "changed";
        ((HistoryCue[])pack.Readings[0].Cues)[0] = new HistoryCue("changed");

        Assert.Equal("synthetic:Ann/account", copy.Sources.Single(s => s.Id == "source-z").Key);
        Assert.Equal("source-z", Assert.Single(copy.Facts[0].Sources));
        Assert.Equal("source-z", Assert.Single(copy.Accounts[0].Sources));
        Assert.Equal(new[] { "activity:Mending", "place:Workshop" }, copy.Accounts[0].Cues.Select(c => c.Key));
        Assert.Equal(new[] { "source-a", "source-z" }, copy.Readings[0].Sources);
        Assert.Equal("activity:Mending", Assert.Single(copy.Readings[0].Cues).Key);
        Assert.Throws<NotSupportedException>(() => ((IList<HistorySource>)copy.Sources).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)copy.Facts[0].Sources).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HistoryCue>)copy.Accounts[0].Cues).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)copy.Readings[0].Sources).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HistoryCue>)copy.Readings[0].Cues).Clear());
    }

    [Fact]
    public void InputOrderDoesNotChangeCanonicalPack()
    {
        HistoryPack original = Synthetic();
        HistoryPack reversed = original with
        {
            Sources = original.Sources.Reverse().ToArray(),
            Accounts = original.Accounts.Select(a => a with { Cues = a.Cues.Reverse().ToArray() }).ToArray(),
            Readings = original.Readings.Select(r => r with { Sources = r.Sources.Reverse().ToArray() }).ToArray(),
        };
        Assert.Equal(Encode(original.CopyValidated()), Encode(reversed.CopyValidated()));
    }

    [Fact]
    public void WarmAndWaryTowardSameObjectRemainDistinct()
    {
        HistoryPack pack = Synthetic();
        pack = pack with { Readings = pack.Readings.Append(pack.Readings[0] with
        { Id = "reading-wary", Stance = ReadingStance.Wary, Text = "The work also worries me." }).ToArray() };
        Assert.Equal(2, pack.CopyValidated().Readings.Count);
    }

    [Theory]
    [InlineData(ReviewStatus.Proposed)]
    [InlineData(ReviewStatus.Rejected)]
    public void AuditReadingsAreRetainedWithoutBeingPromoted(ReviewStatus status)
    {
        HistoryPack pack = Synthetic();
        pack = pack with { Readings = pack.Readings.Append(pack.Readings[0] with
        { Id = "reading-audit", Review = new HistoryReview(status) }).ToArray() };
        Assert.Equal(status, pack.CopyValidated().Readings.Single(r => r.Id == "reading-audit").Review.Status);
    }

    [Fact]
    public void CompletedPastCannotSeedAnAttitudeWithoutItsHoldersStatedPresentLink()
    {
        HistoryPack pack = Synthetic();
        pack = pack with { Facts = new[] { pack.Facts[0] with { Kind = HistoryKind.Past } } };
        Invalid(Reading(pack, r => r with { Admission = AdmissionRule.R1 }));
        Invalid(Reading(pack, r => r with { PresentLinkSource = "source-a" }));
        HistoryPack linked = Reading(pack, r => r with { Admission = AdmissionRule.R1, PresentLinkSource = "source-a" });
        Assert.Equal("source-a", Assert.Single(linked.CopyValidated().Readings).PresentLinkSource);
        // The past is still recallable without an emotional claim.
        Assert.Equal(ReadingStance.None, Assert.Single(Reading(pack, r => r with
        { Admission = AdmissionRule.R1, Stance = ReadingStance.None }).CopyValidated().Readings).Stance);
    }

    [Fact]
    public void AuthoredPastInterpretationIsExplicitlyOursEvenWhenItCarriesAStance()
    {
        HistoryPack pack = Synthetic();
        pack = pack with { Facts = new[] { pack.Facts[0] with { Kind = HistoryKind.Past } } };
        pack = Reading(pack, r => r with
        {
            Origin = ReadingOrigin.Authored, Admission = null,
            Sources = new[] { "source-editor" }, Cues = Array.Empty<HistoryCue>(),
        });
        HistoryReading copy = Assert.Single(pack.CopyValidated().Readings);
        Assert.Equal(ReadingOrigin.Authored, copy.Origin);
        Assert.Null(copy.Admission);
        Assert.Null(copy.PresentLinkSource);
        Assert.Null(Assert.Single(HistoryPack.Read(Encode(pack)).Readings).Admission);
    }

    [Fact]
    public void CanonAdmissionAndAuthoredInterpretationHaveDifferentExplicitMetadata()
    {
        Invalid(Reading(Synthetic(), r => r with { Admission = null }));
        Invalid(Reading(Synthetic(), r => r with { Origin = ReadingOrigin.Authored }));
        InvalidJson(o => o["readings"]![0]!["admission"] = null);
        InvalidJson(o => o["readings"]![0]!["origin"] = "Authored");
        InvalidJson(o =>
        {
            o["readings"]![0]!["origin"] = "Authored";
            o["readings"]![0]!.AsObject().Remove("admission");
        });
    }

    [Fact]
    public void MalformedUtf16CannotCollapseDistinctIdsDuringEncoding()
    {
        Invalid(Reading(Synthetic(), r => r with { Id = "reading-\ud800" }));
        Invalid(Reading(Synthetic(), r => r with { Id = "reading-\udc00" }));
        Invalid(Reading(Synthetic(), r => r with { Object = new(MemoryObjectKind.Activity, "activity-\ud800") }));
        HistoryPack valid = Reading(Synthetic(), r => r with { Id = "reading-\U0001F331" });
        Assert.Equal("reading-\U0001F331", Assert.Single(HistoryPack.Read(Encode(valid)).Readings).Id);
    }

    [Theory]
    [InlineData("source-editor")]
    [InlineData("source-product")]
    [InlineData("unknown")]
    public void AuthoredProductAndUnknownSourcesCannotPretendToBeCanonPresentLinks(string source)
    {
        Invalid(Reading(Synthetic(), r => r with { PresentLinkSource = source }));
    }

    [Fact]
    public void PresentLinkMustBelongToTheReadingNotJustExistElsewhere()
    {
        Invalid(Reading(Synthetic(), r => r with { Sources = new[] { "source-z" }, PresentLinkSource = "source-a" }));
    }

    [Fact]
    public void OnlyCanonCitationsBelongingToARecordCanGiveCuesSourcedWeight()
    {
        HistoryPack pack = Synthetic();
        Invalid(Reading(pack, r => r with { Cues = new[] { new HistoryCue("place:Workshop", "source-editor") } }));
        Invalid(Reading(pack, r => r with { Sources = new[] { "source-z" }, Cues = new[] { new HistoryCue("place:Workshop", "source-a") } }));
        Invalid(pack with { Accounts = new[] { pack.Accounts[0] with
        { Cues = new[] { new HistoryCue("place:Workshop", "source-a") } } } });
        // A shared fact's canon source can establish the cue without copying another holder's reading.
        var account = pack.Accounts[0] with { Sources = new[] { "source-a" } };
        Assert.Equal("source-z", (pack with { Accounts = new[] { account } }).CopyValidated().Accounts[0].Cues[1].SourceId);
    }

    [Fact]
    public void RepeatedCitationsAndCueKeysCannotMultiplyEvidence()
    {
        Invalid(Reading(Synthetic(), r => r with { Sources = new[] { "source-a", "source-a" } }));
        Invalid(Reading(Synthetic(), r => r with { Cues = new[] { new HistoryCue("same"), new HistoryCue("same", "source-a") } }));
    }

    [Fact]
    public void DuplicateSeedsAcrossAccountsCannotMultiplyAHolderAssociation()
    {
        HistoryPack pack = Synthetic();
        pack = pack with
        {
            Facts = pack.Facts.Append(pack.Facts[0] with { Id = "fact-other" }).ToArray(),
            Accounts = pack.Accounts.Append(pack.Accounts[0] with { Id = "account-other", FactId = "fact-other" }).ToArray(),
            Readings = pack.Readings.Append(pack.Readings[0] with { Id = "reading-other", AccountId = "account-other" }).ToArray(),
        };
        Invalid(pack);
    }

    [Fact]
    public void DuplicateExpectationsCannotBeHiddenBehindDifferentObjectsOrOppositeOutcomes()
    {
        HistoryPack pack = Reading(Synthetic(), r => r with
        {
            Stance = ReadingStance.Expects, Expectation = new(ExpectationKind.Activity, "Ann", "mending"), ExpectedSuccess = true,
        });
        pack = pack with { Readings = pack.Readings.Append(pack.Readings[0] with
        {
            Id = "other-expectation", Object = new(MemoryObjectKind.Place, "Workshop"), ExpectedSuccess = false,
        }).ToArray() };
        Invalid(pack);
    }

    [Fact]
    public void SameFactCanHaveDifferentHoldersButNotAmbiguousDuplicateAccountsForOneHolder()
    {
        HistoryPack pack = Synthetic();
        Invalid(pack with { Accounts = pack.Accounts.Append(pack.Accounts[0] with { Id = "account-copy" }).ToArray() });
        Assert.Equal(2, (pack with { Accounts = pack.Accounts.Append(pack.Accounts[0] with
        { Id = "account-bob", Holder = "Bob" }).ToArray() }).CopyValidated().Accounts.Count);
    }

    [Theory]
    [InlineData(null, "2026-10-15")]
    [InlineData("Reviewer", null)]
    [InlineData("Reviewer", "2026-02-30")]
    [InlineData("Reviewer", "October 15")]
    public void ReviewedRequiresIdentifiableReviewerAndActualDate(string? by, string? date)
    {
        Invalid(Reading(Synthetic(), r => r with { Review = new(ReviewStatus.Reviewed, by, date) }));
    }

    [Fact]
    public void ToldAccountsRequireTellerAndOtherAcquisitionKindsCannotImplyOne()
    {
        HistoryPack pack = Synthetic();
        Invalid(pack with { Accounts = new[] { pack.Accounts[0] with { How = KnowledgeKind.Told } } });
        Invalid(pack with { Accounts = new[] { pack.Accounts[0] with { Teller = "Bob" } } });
    }

    [Fact]
    public void AnInterpretationCannotBeStoredAsAnAccountAcquisition()
    {
        HistoryPack pack = Synthetic();
        Invalid(pack with { Accounts = new[] { pack.Accounts[0] with { How = KnowledgeKind.Reading } } });
        InvalidJson(o => o["accounts"]![0]!["how"] = "Reading");
        Assert.Equal(KnowledgeKind.Inferred, Assert.Single((pack with
        { Accounts = new[] { pack.Accounts[0] with { How = KnowledgeKind.Inferred } } }).CopyValidated().Accounts).How);
    }

    [Theory]
    [InlineData("association:synthetic")]
    [InlineData("expectation:Activity:Ann:mending")]
    public void RecallRecordsCannotCollideWithGeneratedCandidateIds(string reservedId)
    {
        HistoryPack pack = Synthetic();
        Invalid(Reading(pack, r => r with { Id = reservedId }));
        Invalid(pack with
        {
            Accounts = new[] { pack.Accounts[0] with { Id = reservedId } },
            Readings = new[] { pack.Readings[0] with { AccountId = reservedId } },
        });
        // Source and fact IDs never become recall candidate IDs and need no prefix restriction.
        HistoryPack fact = pack with
        {
            Facts = new[] { pack.Facts[0] with { Id = reservedId } },
            Accounts = new[] { pack.Accounts[0] with { FactId = reservedId } },
        };
        Assert.Equal(reservedId, Assert.Single(fact.CopyValidated().Facts).Id);
        Assert.Contains((pack with { Sources = pack.Sources.Append(new HistorySource(reservedId,
            "synthetic:unused-source", HistorySourceKind.Authored)).ToArray() }).CopyValidated().Sources, s => s.Id == reservedId);
    }

    [Fact]
    public void ExpectsRequiresCompleteKeyAndOutcomeAndOtherStancesCannotCarryThem()
    {
        HistoryPack pack = Synthetic();
        Invalid(Reading(pack, r => r with { Stance = ReadingStance.Expects }));
        Invalid(Reading(pack, r => r with { Stance = ReadingStance.Expects, Expectation = new(ExpectationKind.Answers, "Bob", "invite") }));
        Invalid(Reading(pack, r => r with { ExpectedSuccess = false }));
        Invalid(Reading(pack, r => r with { Expectation = new(ExpectationKind.Answers, "Bob", "invite") }));
        HistoryReading valid = Assert.Single(Reading(pack, r => r with
        {
            Stance = ReadingStance.Expects, Expectation = new(ExpectationKind.Answers, "Bob", "invite"), ExpectedSuccess = false,
        }).CopyValidated().Readings);
        Assert.False(valid.ExpectedSuccess);
    }

    [Fact]
    public void IdsAreGlobalAndEveryReferenceHasTheCorrectTargetKind()
    {
        HistoryPack pack = Synthetic();
        Invalid(pack with { Facts = new[] { pack.Facts[0] with { Id = "source-z" } } });
        Invalid(pack with { Accounts = new[] { pack.Accounts[0] with { FactId = "source-z" } } });
        Invalid(Reading(pack, r => r with { AccountId = "fact-shared" }));
        Invalid(Reading(pack, r => r with { Sources = new[] { "account-ann" } }));
        Invalid(Reading(pack, r => r with { Sources = Array.Empty<string>() }));
        Invalid(Reading(pack, r => r with { Sources = new[] { "source-editor" }, Cues = Array.Empty<HistoryCue>() }));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("sources")]
    [InlineData("facts")]
    [InlineData("accounts")]
    [InlineData("readings")]
    public void MissingTopLevelFieldsCannotDefaultToAnEmptyPack(string property) => InvalidJson(o => o.Remove(property));

    [Fact]
    public void MissingNestedEnumsOrReviewCannotSilentlyAssumeFirstEnumValue()
    {
        InvalidJson(o => o["facts"]![0]!.AsObject().Remove("kind"));
        InvalidJson(o => o["accounts"]![0]!.AsObject().Remove("how"));
        InvalidJson(o => o["readings"]![0]!.AsObject().Remove("stance"));
        InvalidJson(o => o["readings"]![0]!.AsObject().Remove("origin"));
        InvalidJson(o => o["readings"]![0]!.AsObject().Remove("admission"));
        InvalidJson(o => o["readings"]![0]!.AsObject().Remove("review"));
        InvalidJson(o => o["readings"]![0]!["review"]!.AsObject().Remove("status"));
    }

    [Fact]
    public void IntegerNumericStringAndUnknownEnumsAreRejectedInJsonAndDirectInput()
    {
        InvalidJson(o => o["facts"]![0]!["kind"] = 0);
        InvalidJson(o => o["facts"]![0]!["kind"] = "0");
        InvalidJson(o => o["facts"]![0]!["kind"] = "NeverHappened");
        Invalid(Reading(Synthetic(), r => r with { Stance = (ReadingStance)999 }));
    }

    [Fact]
    public void NullCollectionsEntriesAndNestedObjectsAreRejected()
    {
        InvalidJson(o => o["accounts"] = null);
        InvalidJson(o => o["accounts"]![0] = null);
        InvalidJson(o => o["accounts"]![0]!["cues"] = null);
        InvalidJson(o => o["readings"]![0]!["object"] = null);
        InvalidJson(o => o["readings"]![0]!["review"] = null);
        InvalidJson(o => o["readings"]![0]!["sources"]![0] = null);
        InvalidJson(o => o["readings"]![0]!["cues"]![0] = null);
    }

    [Fact]
    public void UnsupportedVersionUnknownFieldsDuplicatePropertiesAndOversizeContentAreRejected()
    {
        Invalid(Synthetic() with { Version = 2 });
        InvalidJson(o => o["readings"]![0]!["inventedStrength"] = 100);
        string json = Encode(Synthetic());
        Assert.Throws<InvalidDataException>(() => HistoryPack.Read(json.Replace("\"version\":1", "\"version\":1,\"Version\":1", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => HistoryPack.Read(new string(' ', 8 * 1024 * 1024 + 1)));
        Invalid(Reading(Synthetic(), r => r with { Text = new string('x', 2001) }));
        Invalid(Reading(Synthetic(), r => r with { Id = new string('x', 201) }));
        Invalid(Reading(Synthetic(), r => r with { Text = "Hidden\u0000text" }));
    }
}
