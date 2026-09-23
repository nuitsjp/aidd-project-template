using NotesSample.Presentation.Http.Validation;
using Shouldly;
using Xunit;

namespace NotesSample.UnitTests.Presentation.Http.Validation;

public sealed class RuneMaxLengthAttributeTests
{
    public sealed class IsValid
    {
        [Fact]
        public void MaximumEmojiCharacters_ReturnsTrue()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------
            var value = string.Concat(Enumerable.Repeat("😀", 3));
            var attribute = new RuneMaxLengthAttribute(3);

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------
            var result = attribute.IsValid(value);

            // -------------------------------------------------------------
            // Assert
            // -------------------------------------------------------------
            result.ShouldBeTrue();
        }

        [Fact]
        public void MoreThanMaximumEmojiCharacters_ReturnsFalse()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------
            var value = string.Concat(Enumerable.Repeat("😀", 4));
            var attribute = new RuneMaxLengthAttribute(3);

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------
            var result = attribute.IsValid(value);

            // -------------------------------------------------------------
            // Assert
            // -------------------------------------------------------------
            result.ShouldBeFalse();
        }

        [Fact]
        public void EmptyString_ReturnsTrue()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------
            var value = string.Empty;
            var attribute = new RuneMaxLengthAttribute(3);

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------
            var result = attribute.IsValid(value);

            // -------------------------------------------------------------
            // Assert
            // -------------------------------------------------------------
            result.ShouldBeTrue();
        }

        [Fact]
        public void Null_ReturnsFalse()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------
            string? value = null;
            var attribute = new RuneMaxLengthAttribute(3);

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------
            var result = attribute.IsValid(value);

            // -------------------------------------------------------------
            // Assert
            // -------------------------------------------------------------
            result.ShouldBeFalse();
        }

        [Fact]
        public void NonStringValue_ReturnsFalse()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------
            object value = 123;
            var attribute = new RuneMaxLengthAttribute(3);

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------
            var result = attribute.IsValid(value);

            // -------------------------------------------------------------
            // Assert
            // -------------------------------------------------------------
            result.ShouldBeFalse();
        }
    }
}
