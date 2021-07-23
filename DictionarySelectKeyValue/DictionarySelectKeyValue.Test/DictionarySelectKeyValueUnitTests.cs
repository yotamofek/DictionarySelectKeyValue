using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = DictionarySelectKeyValue.Test.CSharpCodeFixVerifier<
    DictionarySelectKeyValue.DictionarySelectKeyValueAnalyzer,
    DictionarySelectKeyValue.DictionarySelectKeyValueCodeFixProvider>;

namespace DictionarySelectKeyValue.Test
{
    [TestClass]
    public class DictionarySelectKeyValueUnitTest
    {
        //No diagnostics expected to show up
        [TestMethod]
        public async Task TestMethod1()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public async Task TestMethod2()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace TestNamespace
    {
        class KV
        {
            public string Key;
            public string Value;
            public KV(string key, string value) {
                Key = key;
                Value = value;
            }
        }

        class DictionaryContext
        {
            public ConcurrentDictionary<string, string> Dictionary { get; } = new ConcurrentDictionary<string, string>();
            public IDictionary<string, string> GetDictionary() => Dictionary;
        }

        class DictionaryContextContext
        {
            private DictionaryContext _dictionaryContext = new DictionaryContext {};
            public DictionaryContext DictionaryContext => _dictionaryContext;
        }
        
        class TestClass
        {

            private void TestMethod<TKey, TValue>(IDictionary<TKey, TValue> d)
            {
                var a = new Dictionary<string, string>{ { ""key1"", ""value1"" } };
                
                _ = {|#0:a.Select(kv => kv.Key)|}.ToArray();
                _ = {|#1:d.Select(kv => { return kv.Value; })|}.ToArray();
                _ = a.Select(kv => kv.Key + kv.Value).ToArray();

                var b = new [] { new KV(""key1"", ""value1"") };
                _ = b.Select(kv => kv.Key).ToArray();

                var c = new DictionaryContextContext {};
                System.Console.WriteLine({|#2:c.DictionaryContext.GetDictionary().Select(a => a.Key)|}.ToArray());
            }
        }
    }";

            var fixedTest = @"
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace TestNamespace
    {
        class KV
        {
            public string Key;
            public string Value;
            public KV(string key, string value) {
                Key = key;
                Value = value;
            }
        }

        class DictionaryContext
        {
            public ConcurrentDictionary<string, string> Dictionary { get; } = new ConcurrentDictionary<string, string>();
            public IDictionary<string, string> GetDictionary() => Dictionary;
        }

        class DictionaryContextContext
        {
            private DictionaryContext _dictionaryContext = new DictionaryContext {};
            public DictionaryContext DictionaryContext => _dictionaryContext;
        }
        
        class TestClass
        {

            private void TestMethod<TKey, TValue>(IDictionary<TKey, TValue> d)
            {
                var a = new Dictionary<string, string>{ { ""key1"", ""value1"" } };
                
                _ = a.Keys.ToArray();
                _ = d.Values.ToArray();
                _ = a.Select(kv => kv.Key + kv.Value).ToArray();

                var b = new [] { new KV(""key1"", ""value1"") };
                _ = b.Select(kv => kv.Key).ToArray();

                var c = new DictionaryContextContext {};
                System.Console.WriteLine(c.DictionaryContext.GetDictionary().Keys.ToArray());
            }
        }
    }";

            await VerifyCS.VerifyCodeFixAsync(
                test,
                new[] {
                    VerifyCS.Diagnostic("DictionarySelectKeyValue").WithLocation(0).WithArguments("Key"),
                    VerifyCS.Diagnostic("DictionarySelectKeyValue").WithLocation(1).WithArguments("Value"),
                    VerifyCS.Diagnostic("DictionarySelectKeyValue").WithLocation(2).WithArguments("Key")
                },
                fixedTest);
        }
    }
}
