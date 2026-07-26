using Xunit;
using PolyglotCLI.Validation;

namespace PolyglotCLI.test.Validation
{
    public class NumericRangeValidatorTests
    {
        // ── ClampTimeout ───────────────────────────────────────

        [Theory]
        [InlineData(60)]
        [InlineData(300)]
        [InlineData(1)]
        [InlineData(3600)]
        public void ClampTimeout_AcceptsValidRange(int value)
        {
            Assert.Equal(value, NumericRangeValidator.ClampTimeout(value));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        [InlineData(3601)]
        [InlineData(int.MaxValue)]
        public void ClampTimeout_ReturnsDefaultForOutOfRange(int value)
        {
            Assert.Equal(NumericRangeValidator.DefaultTimeoutSeconds,
                NumericRangeValidator.ClampTimeout(value));
        }

        // ── ClampTemperature ───────────────────────────────────

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.3)]
        [InlineData(0.7)]
        [InlineData(1.0)]
        [InlineData(2.0)]
        public void ClampTemperature_AcceptsValidRange(double value)
        {
            Assert.Equal(value, NumericRangeValidator.ClampTemperature(value));
        }

        [Theory]
        [InlineData(-0.1)]
        [InlineData(-1.0)]
        [InlineData(2.1)]
        [InlineData(100.0)]
        public void ClampTemperature_ClampsToMinMax(double value)
        {
            double result = NumericRangeValidator.ClampTemperature(value);
            Assert.True(result >= NumericRangeValidator.MinTemperature);
            Assert.True(result <= NumericRangeValidator.MaxTemperature);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void ClampTemperature_ReturnsDefaultForSpecialValues(double value)
        {
            Assert.Equal(NumericRangeValidator.DefaultTemperature,
                NumericRangeValidator.ClampTemperature(value));
        }

        // ── ClampChunkSize ─────────────────────────────────────

        [Theory]
        [InlineData(100)]
        [InlineData(6000)]
        [InlineData(100_000)]
        public void ClampChunkSize_AcceptsValidRange(int value)
        {
            Assert.Equal(value, NumericRangeValidator.ClampChunkSize(value));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(99)]
        [InlineData(100_001)]
        [InlineData(int.MaxValue)]
        public void ClampChunkSize_ReturnsDefaultForOutOfRange(int value)
        {
            Assert.Equal(NumericRangeValidator.DefaultChunkSize,
                NumericRangeValidator.ClampChunkSize(value));
        }

        // ── ClampChunkOverlap ──────────────────────────────────

        [Fact]
        public void ClampChunkOverlap_AcceptsReasonable()
        {
            Assert.Equal(300, NumericRangeValidator.ClampChunkOverlap(300, 6000));
        }

        [Fact]
        public void ClampChunkOverlap_ClampsToHalfChunkSize()
        {
            // Para chunkSize=6000, maxOverlap es 3000.
            // value=5000 debería clampearse a 3000.
            Assert.Equal(3000, NumericRangeValidator.ClampChunkOverlap(5000, 6000));
        }

        [Fact]
        public void ClampChunkOverlap_AcceptsZero()
        {
            Assert.Equal(0, NumericRangeValidator.ClampChunkOverlap(0, 6000));
        }

        [Fact]
        public void ClampChunkOverlap_RejectsNegative()
        {
            Assert.Equal(0, NumericRangeValidator.ClampChunkOverlap(-100, 6000));
        }

        [Fact]
        public void ClampChunkOverlap_InvalidChunkSize_ReturnsDefault()
        {
            Assert.Equal(NumericRangeValidator.DefaultChunkOverlap,
                NumericRangeValidator.ClampChunkOverlap(500, 0));
            Assert.Equal(NumericRangeValidator.DefaultChunkOverlap,
                NumericRangeValidator.ClampChunkOverlap(500, -1));
        }
    }
}
