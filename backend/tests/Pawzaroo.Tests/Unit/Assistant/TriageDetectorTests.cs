using FluentAssertions;
using Pawzaroo.Infrastructure.Modules.Assistant;
using Xunit;

namespace Pawzaroo.Tests.Unit.Assistant;

public class TriageDetectorTests
{
    [Theory]
    [InlineData("My dog ate rat poison an hour ago", "poisoning")]
    [InlineData("I think my cat was poisoned", "poisoning")]
    [InlineData("he ate a whole bar of chocolate", "poisoning")]
    [InlineData("My puppy is having a seizure", "seizure")]
    [InlineData("she is convulsing on the floor", "seizure")]
    [InlineData("My dog is not breathing", "not breathing")]
    [InlineData("the cat is struggling to breathe", "not breathing")]
    [InlineData("There is heavy bleeding from his leg", "bleeding")]
    [InlineData("the wound won't stop bleeding", "bleeding")]
    [InlineData("His belly is swollen hard and he keeps retching but nothing comes up", "bloat")]
    [InlineData("I think she has bloat", "bloat")]
    [InlineData("My dog got heatstroke after the walk", "heatstroke")]
    [InlineData("The rabbit collapsed and is unresponsive", "collapse")]
    [InlineData("my dog was hit by a car", "trauma")]
    [InlineData("my male cat can't pee and keeps straining", "obstruction")]
    public void Emergency_phrases_are_detected(string text, string category)
    {
        var r = TriageDetector.Detect(text);
        r.IsEmergency.Should().BeTrue();
        r.Category.Should().Be(category);
    }

    [Theory]
    [InlineData("What vaccines does a puppy need?")]
    [InlineData("Which foods are poisonous to dogs?")]
    [InlineData("My cat has never had a seizure, just checking diet advice")]
    [InlineData("There is no bleeding, only a small scratch")]
    [InlineData("Recommend a good dog food for a senior labrador")]
    [InlineData("")]
    [InlineData(null)]
    public void Routine_questions_are_not_emergencies(string? text)
    {
        TriageDetector.Detect(text).IsEmergency.Should().BeFalse();
    }

    [Fact]
    public void Detection_is_case_insensitive_and_handles_curly_apostrophes()
    {
        TriageDetector.Detect("MY DOG IS NOT BREATHING").IsEmergency.Should().BeTrue();
        TriageDetector.Detect("he won’t stop bleeding").IsEmergency.Should().BeTrue();
    }
}
