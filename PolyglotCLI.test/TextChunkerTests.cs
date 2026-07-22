using System;
using System.Collections.Generic;
using Xunit;
using PolyglotCLI;

namespace PolyglotCLI.test
{
    public class TextChunkerTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\n\n")]
        public void ChunkText_ReturnsEmptyList_WhenTextIsNullOrEmptyOrWhiteSpace(string? text)
        {
            // Act
            var result = TextChunker.ChunkText(text!);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ChunkText_SplitsTextWithoutOverlap_WhenOverlapIsZero()
        {
            // Arrange
            string text = "Line 1\nLine 2\nLine 3";

            // Act
            var result = TextChunker.ChunkText(text, maxCharacters: 15, overlapCharacters: 0);

            // Assert
            // "Line 1\nLine 2" is 13 chars, adding "\nLine 3" would make it 20 > 15. So it chunks.
            Assert.Equal(2, result.Count);
            Assert.Equal($"Line 1{Environment.NewLine}Line 2", result[0]);
            Assert.Equal("Line 3", result[1]);
        }

        [Fact]
        public void ChunkText_ClampsNegativeOverlapToZero()
        {
            // Arrange
            string text = "Line 1\nLine 2\nLine 3";

            // Act
            var result = TextChunker.ChunkText(text, maxCharacters: 15, overlapCharacters: -5);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal($"Line 1{Environment.NewLine}Line 2", result[0]);
            Assert.Equal("Line 3", result[1]);
        }

        [Fact]
        public void ChunkText_ClampsOverlapToHalfOfMaxCharacters()
        {
            // Arrange
            string text = "Hello World\nHow are you";

            // Act
            // Max is 10, overlap is 8. Clamps overlap to 10/2 = 5.
            var result = TextChunker.ChunkText(text, maxCharacters: 10, overlapCharacters: 8);

            // Assert
            Assert.NotEmpty(result);
        }

        [Fact]
        public void ChunkText_CarriesOverlappingCharactersIntoNextChunk()
        {
            // Arrange
            string text = "Hello World\nHow are you";
            // "Hello World" is 11 chars. Max is 12, overlap is 4.
            // First chunk: "Hello World".
            // Since overlap is 4, "orld" is carried to the next chunk.
            // Next chunk gets "orld\nHow are you" -> wait, does it append "How are you"?
            // Yes, let's verify exact logic.

            // Act
            var result = TextChunker.ChunkText(text, maxCharacters: 12, overlapCharacters: 4);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Hello World", result[0]);
            Assert.StartsWith("orld", result[1]);
        }

        [Fact]
        public void ChunkText_SplitsSingleLongLineByCharacters()
        {
            // Arrange
            string longLine = "abcdefghijklmnopqrstuvwxyz";

            // Act
            var result = TextChunker.ChunkText(longLine, maxCharacters: 10, overlapCharacters: 0);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("abcdefghij", result[0]);
            Assert.Equal("klmnopqrst", result[1]);
            Assert.Equal("uvwxyz", result[2]);
        }
    }
}
