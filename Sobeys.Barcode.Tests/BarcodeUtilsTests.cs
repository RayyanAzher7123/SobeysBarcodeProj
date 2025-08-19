using Sobeys.Barcode;
using Xunit;

namespace Sobeys.Barcode.Tests
{
    public class BarcodeUtilsTests
    {
        [Theory]
        [InlineData("036000291452", BarcodeType.UPCA)]
        [InlineData("01234565", BarcodeType.UPCE)]
        [InlineData("4006381333931", BarcodeType.EAN13)]
        [InlineData("123456", BarcodeType.UPCE)]
        public void Identify_Works(string code, BarcodeType expected)
            => Assert.Equal(expected, BarcodeUtils.Identify(code));

        [Theory]
        [InlineData("036000291452", true)]
        [InlineData("4006381333931", true)]
        [InlineData("01234565", true)]
        [InlineData("036000291453", false)]
        public void ValidateCheckDigit_Works(string code, bool expected)
            => Assert.Equal(expected, BarcodeUtils.ValidateCheckDigit(code));

        [Theory]
        [InlineData("03600029145", "036000291452")]
        [InlineData("400638133393", "4006381333931")]
        [InlineData("0123456", "01234565")]
        public void AddCheckDigit_Works(string body, string expected)
            => Assert.Equal(expected, BarcodeUtils.AddCheckDigit(body));

        [Fact]
        public void Expand_UPCE_To_UPCA_Works()
        {
            var upce6 = "123450";
            Assert.True(BarcodeUtils.TryExpandUPCE(upce6, assumeNumberSystem: 0, out var upca));
            Assert.True(BarcodeUtils.ValidateCheckDigit(upca));
            Assert.Equal(12, upca.Length);
        }

        [Fact]
        public void Compress_UPCA_To_UPCE_When_Possible()
        {
            var body11 = "01234500005";
            var check = BarcodeUtils.CalculateCheckDigit(body11);
            var upca = body11 + check;

            Assert.True(BarcodeUtils.TryCompressUPCAtoUPCE(upca, out var upce8));
            Assert.Equal(8, upce8.Length);

            Assert.True(BarcodeUtils.TryExpandUPCE(upce8, assumeNumberSystem: 0, out var upca2));
            Assert.Equal(upca, upca2);
        }

        [Fact]
        public void TryConvert_Switches_Types()
        {
            var body11 = "01234500005";
            var check = BarcodeUtils.CalculateCheckDigit(body11);
            var upca = body11 + check;

            Assert.True(BarcodeUtils.TryConvert(upca, out var upce8));
            Assert.Equal(BarcodeType.UPCE, BarcodeUtils.Identify(upce8));

            Assert.True(BarcodeUtils.TryConvert(upce8, out var upca12));
            Assert.Equal(BarcodeType.UPCA, BarcodeUtils.Identify(upca12));
        }

        [Fact]
        public void NonCompressible_UPCA_ReturnsFalse()
        {
            var upca = "036000291452";
            Assert.True(BarcodeUtils.ValidateCheckDigit(upca));
            Assert.False(BarcodeUtils.TryCompressUPCAtoUPCE(upca, out _));
        }
    }
}
