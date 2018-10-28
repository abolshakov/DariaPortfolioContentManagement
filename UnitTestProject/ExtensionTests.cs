using System.Collections.Generic;
using ContentManagement;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace UnitTestProject
{
    [TestFixture]
    public class ExtensionTests
    {
        public static IEnumerable<TestCaseData> HyphenTestData()
        {
            yield return new TestCaseData("abc", "abc");
            yield return new TestCaseData("--abc--def--", "abc-def");
            yield return new TestCaseData("abc,def.ghi;jkl mno", "abc-def-ghi-jkl-mno");
            yield return new TestCaseData("ABC", "abc");
            yield return new TestCaseData("abcDefGhi", "abc-def-ghi");
            yield return new TestCaseData("abc1def2ghi", "abc-1-def-2-ghi");
            yield return new TestCaseData("abc123ghi456", "abc-123-ghi-456");
            yield return new TestCaseData("123abc456ghi", "123-abc-456-ghi");
            yield return new TestCaseData("abcDEFghiJKL", "abc-def-ghi-jkl");
            yield return new TestCaseData("ABCdefGHIjkl", "abc-def-ghi-jkl");
        }

        [TestCaseSource(nameof(HyphenTestData))]
        public void TestMethod1(string source, string expected)
        {
            var result = source.ToHyphenCase();
            Assert.AreEqual(expected, result);
        }
    }
}
